using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoNai3Tools.Controllers;
using AutoNai3Tools.Services;
using AutoNai3Tools.utils;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json.Linq;

namespace AutoNai3Tools {
    /// <summary>
    /// 主窗口：整个界面已经换成 WebView2 承载的 HTML/CSS/JS，
    /// 这个类只负责「窗口外壳 + 业务逻辑 + 与前端通信」。
    /// </summary>
    public partial class Form1 : Form {
        private const int WM_NCHITTEST = 0x84;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int ResizeAreaSize = 6;
        private const int MaxBufferedLogLines = 500;

        public int runNum = 1;
        public PicProperty picProps = new PicProperty();
        public SettingProperty settingProps = new SettingProperty();

        internal readonly GenerationController generationController;
        internal readonly DirectorToolController directorToolController;
        internal readonly IConfigService configService;
        internal readonly IWildcardService wildcardService;

        private readonly GenerationUiDataProvider generationDataProvider;
        private readonly WebUiLogSink logSink;
        private readonly List<JObject> bufferedLogs = new List<JObject>();
        private readonly object logLock = new object();

        private TagDatabase tagDatabase;
        private WebView2 webView;
        private bool webUiReady;
        private string lastPicInfo = string.Empty;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public Form1() {
            InitializeComponent();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 720);
            Size = new Size(1440, 900);
            BackColor = Color.FromArgb(13, 15, 19);
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer,
                true);
            UpdateStyles();

            logSink = new WebUiLogSink(this);
            Logger.Initialize(logSink);

            configService = new ConfigService();
            wildcardService = new WildcardService();

            SyncWindowTitleVersion();

            try {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tag_dictionary.sqlite");
                tagDatabase = new TagDatabase(dbPath);
            }
            catch (Exception ex) {
                Logger.Warn($"标签库加载失败：{ex.Message}");
            }

            LoadConfigs();

            generationDataProvider = new GenerationUiDataProvider(
                picProps,
                settingProps,
                this,
                () => PromptText,
                () => NegativePromptText,
                BuildVibeSelections,
                CaptureImg2ImgOptions);
            generationController = new GenerationController(generationDataProvider);
            AttachGenerationControllerEvents();

            directorToolController = new DirectorToolController(new DirectorToolProcessor(), picProps, settingProps);
            AttachDirectorToolEvents();
        }

        private void InitializeComponent() {
            SuspendLayout();
            Name = "Form1";
            Text = Properties.Resources.AppTitle;
            ResumeLayout(false);
        }

        private void SyncWindowTitleVersion() {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string displayVersion;
            if (version == null) {
                displayVersion = Properties.Resources.AppVersionUnknown;
            }
            else if (version.Revision <= 0) {
                displayVersion = $"{version.Major}.{version.Minor}.{version.Build}";
            }
            else {
                displayVersion = version.ToString();
            }

            string baseTitle = Properties.Resources.AppTitle;
            Text = string.IsNullOrWhiteSpace(baseTitle) ? $"v{displayVersion}" : $"{baseTitle} v{displayVersion}";
        }

        protected override async void OnLoad(EventArgs e) {
            base.OnLoad(e);
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync() {
            try {
                string root = AppPaths.WebUiRoot;
                if (!Directory.Exists(root)) {
                    throw new DirectoryNotFoundException(
                        $"未找到前端资源目录：{root}\n请确认 webui 文件夹已随程序一起复制。");
                }

                Directory.CreateDirectory(AppPaths.PreviewRoot);
                CleanupPreviewFolder();

                Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", AppPaths.WebViewUserDataRoot);

                webView = new WebView2 {
                    Dock = DockStyle.Fill,
                    DefaultBackgroundColor = BackColor
                };
                Controls.Add(webView);

                // 设置 AUTONAI_WEBVIEW_DEBUG_PORT 后可以用 Edge/Chrome 的 DevTools 连接界面调试。
                var environmentOptions = new CoreWebView2EnvironmentOptions();
                string debugPort = Environment.GetEnvironmentVariable("AUTONAI_WEBVIEW_DEBUG_PORT");
                if (!string.IsNullOrWhiteSpace(debugPort))
                    environmentOptions.AdditionalBrowserArguments = "--remote-debugging-port=" + debugPort;

                var environment = await CoreWebView2Environment
                    .CreateAsync(null, AppPaths.WebViewUserDataRoot, environmentOptions)
                    .ConfigureAwait(true);
                await webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

                var core = webView.CoreWebView2;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.IsSwipeNavigationEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;

                core.SetVirtualHostNameToFolderMapping("app.local", root,
                    CoreWebView2HostResourceAccessKind.DenyCors);
                core.SetVirtualHostNameToFolderMapping("preview.local", AppPaths.PreviewRoot,
                    CoreWebView2HostResourceAccessKind.Allow);

                core.WebMessageReceived += HandleWebMessageReceived;
                core.NewWindowRequested += (sender, args) => {
                    args.Handled = true;
                    OpenExternal(args.Uri);
                };
                core.NavigationCompleted += (sender, args) => {
                    if (!args.IsSuccess) {
                        Logger.Error($"前端加载失败：{args.WebErrorStatus}");
                    }
                };
                core.ProcessFailed += (sender, args) => {
                    Logger.Error($"前端进程异常退出：{args.ProcessFailedKind}");
                };

                core.Navigate("https://app.local/index.html");
            }
            catch (Exception ex) {
                Logger.Error("初始化 WebView2 失败", exception: ex);
                MessageBox.Show(
                    "初始化界面失败，请确认已安装 WebView2 运行时（Microsoft Edge WebView2 Runtime）。\n\n" +
                    ex.Message,
                    Properties.Resources.Title_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CleanupPreviewFolder() {
            try {
                var limit = DateTime.UtcNow.AddDays(-1);
                foreach (var file in Directory.GetFiles(AppPaths.PreviewRoot, "*.png")) {
                    if (File.GetLastWriteTimeUtc(file) < limit)
                        File.Delete(file);
                }
            }
            catch {
                // 预览缓存清理失败不影响主流程
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            base.OnFormClosed(e);
            SaveConfigs();
            tagDatabase?.Dispose();
        }

        internal void OpenExternal(string url) {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return;

            try {
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex) {
                Logger.Warn("无法打开链接",
                    context: Logger.Context(("url", url), ("reason", ex.Message)));
            }
        }

        #region 生成流程事件

        private void AttachGenerationControllerEvents() {
            generationController.Started += HandleGenerationStarted;
            generationController.Stopped += HandleGenerationStopped;
            generationController.IterationStarted += HandleGenerationIterationStarted;
            generationController.ImageReady += HandleGenerationImageReady;
            generationController.Completed += HandleGenerationCompleted;
            generationController.Cancelled += HandleGenerationCancelled;
            generationController.Failed += HandleGenerationFailed;
        }

        private void AttachDirectorToolEvents() {
            directorToolController.BusyStateChanged += busy => Post("director-busy", new JObject { ["busy"] = busy });
            directorToolController.PreviewUpdated += image => {
                string url = SavePreviewImage(image, "director-in");
                if (url != null)
                    Post("director-preview", new JObject { ["url"] = url });
            };
            directorToolController.OutputUpdated += image => {
                string url = SavePreviewImage(image, "director-out");
                if (url != null)
                    Post("director-output", new JObject { ["url"] = url });
            };
            directorToolController.Completed += () => Post("director-done", new JObject { ["ok"] = true });
            directorToolController.Failed += ex => {
                if (ex is OperationCanceledException)
                    return;
                Post("director-done", new JObject { ["ok"] = false, ["message"] = ex?.Message });
            };
        }

        private void HandleGenerationStarted() {
            isGenerating = true;
            Post("started", new JObject());
            PushAnlasEstimate();
        }

        private void HandleGenerationIterationStarted(int iteration) {
            PushPicProps();
            Post("iteration", new JObject { ["index"] = iteration, ["total"] = Math.Max(1, picProps.RunNum) });
            PushAnlasEstimate();
        }

        private void HandleGenerationImageReady(int iteration, Bitmap bitmap) {
            if (bitmap == null)
                return;

            if (settingProps.ClosePicPreview) {
                bitmap.Dispose();
                return;
            }

            string url = SavePreviewImage(bitmap, "gen");
            bitmap.Dispose();
            if (url == null)
                return;

            Post("image-ready", new JObject {
                ["url"] = url,
                ["index"] = iteration,
                ["seed"] = picProps.Seeds,
                ["width"] = picProps.Width,
                ["height"] = picProps.Height,
                ["steps"] = picProps.Steps
            });
            PushAnlasEstimate();
        }

        private void HandleGenerationStopped() {
            isGenerating = false;
            Post("idle", new JObject());
        }
        private void HandleGenerationCompleted() {
            isGenerating = false;
            Post("finished", new JObject());
            PushAnlasEstimate();
        }

        private void HandleGenerationCancelled() {
            isGenerating = false;
            Post("stopped", new JObject());
            PushAnlasEstimate();
        }

        private void HandleGenerationFailed(Exception ex) {
            isGenerating = false;
            Logger.Error("生成任务发生未处理异常", exception: ex,
                context: Logger.Context(("stage", "pipeline")));
            Post("failed", new JObject { ["message"] = ex?.Message ?? "未知错误" });
            PushAnlasEstimate();
        }

        #endregion

        #region 前端通信

        internal void Post(string type, JObject data) {
            PostRaw(new JObject {
                ["type"] = type,
                ["data"] = data ?? new JObject()
            });
        }

        internal void PostRaw(JObject message) {
            if (message == null)
                return;

            // 生成流程运行在后台线程，而 CoreWebView2 只允许在 UI 线程访问，
            // 所以必须先把消息切回 UI 线程，再决定是直发还是缓冲。
            if (!IsDisposed && !Disposing && IsHandleCreated && InvokeRequired) {
                try {
                    BeginInvoke(new Action<JObject>(PostRaw), message);
                }
                catch (Exception) {
                    // 窗口正在关闭，直接丢弃
                }
                return;
            }

            if (webUiReady && webView?.CoreWebView2 != null) {
                try {
                    webView.CoreWebView2.PostWebMessageAsJson(message.ToString(Newtonsoft.Json.Formatting.None));
                    return;
                }
                catch (Exception) {
                    // 前端正在重载，退回缓冲
                }
            }

            string kind = message["type"]?.ToString();
            if (kind == "log" || kind == "pic-info" || kind == "state") {
                lock (logLock) {
                    bufferedLogs.Add(message);
                    if (bufferedLogs.Count > MaxBufferedLogLines)
                        bufferedLogs.RemoveRange(0, bufferedLogs.Count - MaxBufferedLogLines);
                }
            }
        }

        internal void MarkWebUiReady() {
            webUiReady = true;

            List<JObject> pending;
            lock (logLock) {
                pending = bufferedLogs.ToList();
                bufferedLogs.Clear();
            }

            foreach (var message in pending) {
                if (message["type"]?.ToString() == "state")
                    continue;
                PostRaw(message);
            }
        }

        internal void RecordPicInfo(string message) {
            lastPicInfo = message ?? string.Empty;
            Post("pic-info", new JObject { ["text"] = lastPicInfo });
        }

        internal void RecordLog(LogEntry entry) {
            var text = new System.Text.StringBuilder(entry.Message ?? string.Empty);
            if (entry.Context != null && entry.Context.Count > 0)
                text.Append(" | ").Append(string.Join(", ", entry.Context.Select(kv => kv.Key + "=" + kv.Value)));
            if (entry.Exception != null)
                text.Append(" | ").Append(entry.Exception);

            Post("log", new JObject {
                ["time"] = entry.Timestamp.ToString("HH:mm:ss"),
                ["level"] = entry.Level.ToString().ToLowerInvariant(),
                ["category"] = entry.Category ?? string.Empty,
                ["text"] = text.ToString()
            });
        }

        /// <summary>把 C# 端主动修改的文本回推给前端（例如导入 PNG 源数据后）。</summary>
        internal void PushTextValues(params string[] fields) {
            var data = new JObject();
            foreach (var field in fields) {
                switch (field) {
                    case "prompt":
                        data["prompt"] = PromptText;
                        break;
                    case "negativePrompt":
                        data["negativePrompt"] = NegativePromptText;
                        break;
                    case "artistFixed":
                        data["artistFixed"] = ArtistFixedText;
                        break;
                    case "artistRandom":
                        data["artistRandom"] = ArtistRandomText;
                        break;
                }
            }

            Post("text", data);
        }

        #endregion

        #region 图片预览缓存

        internal string SavePreviewImage(Image image, string prefix) {
            if (image == null)
                return null;

            try {
                Directory.CreateDirectory(AppPaths.PreviewRoot);
                string name = $"{prefix}_{DateTime.Now:HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
                string path = Path.Combine(AppPaths.PreviewRoot, name);
                image.Save(path, ImageFormat.Png);
                return "https://preview.local/" + name;
            }
            catch (Exception ex) {
                Logger.Warn("保存预览图片失败",
                    context: Logger.Context(("reason", ex.Message)));
                return null;
            }
        }

        #endregion

        #region 日志接收器

        private sealed class WebUiLogSink : ILogSink, ILogSpacerSink, IPicInfoSink {
            private readonly Form1 form;

            public WebUiLogSink(Form1 form) {
                this.form = form ?? throw new ArgumentNullException(nameof(form));
            }

            public LogSinkCapabilities Capabilities => LogSinkCapabilities.Ui;

            public bool IsEnabled(LogLevel level) => !form.IsDisposed;

            public void Write(LogEntry entry) {
                if (form.IsDisposed || form.Disposing)
                    return;

                if (form.InvokeRequired) {
                    try {
                        form.BeginInvoke(new Action<LogEntry>(Write), entry);
                    }
                    catch (InvalidOperationException) {
                        // 窗口正在关闭
                    }
                    return;
                }

                form.RecordLog(entry);
            }

            public void InsertSpacer() { }

            public void UpdatePicInfo(string message) {
                if (form.IsDisposed || form.Disposing)
                    return;

                if (form.InvokeRequired) {
                    try {
                        form.BeginInvoke(new Action<string>(UpdatePicInfo), message);
                    }
                    catch (InvalidOperationException) {
                    }
                    return;
                }

                form.RecordPicInfo(message);
            }
        }

        #endregion

        #region 窗口边框（无边框窗口的拖动与缩放）

        internal void BeginWindowDrag() {
            try {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
            catch {
                // 拖动失败时忽略
            }
        }

        internal void ToggleMaximize() {
            if (WindowState == FormWindowState.Maximized) {
                WindowState = FormWindowState.Normal;
                return;
            }

            MaximizedBounds = Screen.FromControl(this).WorkingArea;
            WindowState = FormWindowState.Maximized;
        }

        internal void MinimizeWindow() {
            WindowState = FormWindowState.Minimized;
        }

        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);

            if (m.Msg != WM_NCHITTEST || WindowState == FormWindowState.Maximized)
                return;

            int x = m.LParam.ToInt32() & 0xFFFF;
            int y = (m.LParam.ToInt32() >> 16) & 0xFFFF;
            var clientPos = PointToClient(new Point(x, y));

            bool left = clientPos.X <= ResizeAreaSize;
            bool right = clientPos.X >= ClientSize.Width - ResizeAreaSize;
            bool top = clientPos.Y <= ResizeAreaSize;
            bool bottom = clientPos.Y >= ClientSize.Height - ResizeAreaSize;

            if (left && top)
                m.Result = (IntPtr)HTTOPLEFT;
            else if (right && top)
                m.Result = (IntPtr)HTTOPRIGHT;
            else if (left && bottom)
                m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (right && bottom)
                m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (top)
                m.Result = (IntPtr)HTTOP;
            else if (bottom)
                m.Result = (IntPtr)HTBOTTOM;
            else if (left)
                m.Result = (IntPtr)HTLEFT;
            else if (right)
                m.Result = (IntPtr)HTRIGHT;
            else
                m.Result = (IntPtr)HTCLIENT;
        }

        #endregion
    }
}
