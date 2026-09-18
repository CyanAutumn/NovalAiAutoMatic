using AutoNai3Tools.Services;
using AutoNai3Tools.utils;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using AutoNai3Tools.Controllers;

namespace AutoNai3Tools {
    public partial class Form1 : Form {
        public int runNum;
        public PicProperty picProps = new PicProperty();
        public SettingProperty settingProps = new SettingProperty();
        private readonly GenerationController generationController;
        private readonly IGenerationDataProvider generationDataProvider;
        private readonly DirectorToolController directorToolController;
        private readonly IConfigService configService;
        private readonly IWildcardService wildcardService;
        private TagDatabase tagDatabase;
        private AutoCompleteHelper autoCompleteHelper;

        public Form1() {
            InitializeComponent();
            InitializeMetadataDragDrop();
            ApplyLocalization();
            SyncWindowTitleVersion();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer,
                true);
            UpdateStyles();
            EnableDoubleBuffer(this);
            Control.CheckForIllegalCrossThreadCalls = false;

            configService = new ConfigService();
            wildcardService = new WildcardService();

            RefreshConfig();
            InitGrpEventArgs();
            cmbColorizeDerfy.SelectedIndex = 0;
            cmbEmotionEmotion.SelectedIndex = 0;
            cmbEmotionDefry.SelectedIndex = 0;
            Logger.Initialize(this);
            tabControl2.TabPages.Remove(tabPage15);
            tabControl2.TabPages.Remove(tabPage18);
            propertyGrid1.SelectedObject = picProps;
            propertyGridSettings.SelectedObject = settingProps;
            propertyGridSettings.PropertyValueChanged += HandleSettingsPropertyValueChanged;

            generationDataProvider = new GenerationUiDataProvider(
                picProps,
                settingProps,
                this,
                () => txtPrompt.Text,
                () => txtNegativePrompt.Text,
                dgvVibe,
                CaptureImg2ImgOptions);
            generationController = new GenerationController(generationDataProvider);
            AttachGenerationControllerEvents();
            InitializeAnlasDisplay();

            var directorProcessor = new DirectorToolProcessor();
            directorToolController = new DirectorToolController(directorProcessor, picProps, settingProps);
            AttachDirectorToolEvents();
            InitializeAutoComplete();
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

            string baseTitle = dreamForm1?.Text;
            if (string.IsNullOrWhiteSpace(baseTitle))
                baseTitle = Properties.Resources.AppTitle;

            string fullTitle = $"{baseTitle} v{displayVersion}";
            if (dreamForm1 != null)
                dreamForm1.Text = fullTitle;

            Text = fullTitle;
        }

        private void InitializeAutoComplete() {
            try {
                string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tag_dictionary.sqlite");
                tagDatabase = new TagDatabase(dbPath);

                autoCompleteHelper = new AutoCompleteHelper(txtPrompt, tagDatabase, () => {
                    var list = new System.Collections.Generic.List<string>();
                    if (dgvTagSnippet != null) {
                        foreach (DataGridViewRow row in dgvTagSnippet.Rows) {
                            if (row.Cells[0].Value != null) {
                                string name = row.Cells[0].Value.ToString();
                                if (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                                    name = name.Substring(0, name.Length - 4);
                                list.Add("<" + name + ">");
                            }
                        }
                    }
                    return list;
                });
            } catch (Exception ex) {
                Logger.Warn($"Failed to init AutoComplete: {ex.Message}");
            }
        }

        private void EnableDoubleBuffer(Control control) {
            if (control == null)
                return;

            var doubleBufferProperty = typeof(Control).GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            doubleBufferProperty?.SetValue(control, true, null);

            foreach (Control child in control.Controls) {
                EnableDoubleBuffer(child);
            }
        }

        private void ApplyLocalization() {
            var resources = new ComponentResourceManager(typeof(Form1));
            ApplyResourcesRecursive(resources, this);
            ApplyLocalizationToDataGridViewColumns(resources);
        }

        private static void ApplyResourcesRecursive(ComponentResourceManager resources, Control control) {
            resources.ApplyResources(control, control.Name);
            foreach (Control child in control.Controls) {
                ApplyResourcesRecursive(resources, child);
            }
        }

        private void ApplyLocalizationToDataGridViewColumns(ComponentResourceManager resources) {
            ApplyHeaderText(resources, dataGridViewTextBoxColumn1, "dataGridViewTextBoxColumn1");
            ApplyHeaderText(resources, dataGridViewTextBoxColumn2, "dataGridViewTextBoxColumn2");
            ApplyHeaderText(resources, Column1, "Column1");
            ApplyHeaderText(resources, Column2, "Column2");
            ApplyHeaderText(resources, Column3, "Column3");
        }

        private static void ApplyHeaderText(ComponentResourceManager resources, DataGridViewColumn column, string name) {
            if (column == null)
                return;

            var headerText = resources.GetString($"{name}.HeaderText");
            if (!string.IsNullOrWhiteSpace(headerText)) {
                column.HeaderText = headerText;
            }
        }

        private void HandleSettingsPropertyValueChanged(object sender, PropertyValueChangedEventArgs e) {
            if (e?.ChangedItem?.PropertyDescriptor?.Name == nameof(SettingProperty.AnlasTracking)) {
                OnAnlasTrackingChanged();
                return;
            }

            if (e?.ChangedItem?.PropertyDescriptor?.Name != nameof(SettingProperty.UiLanguage))
                return;

            var result = MessageBox.Show(Properties.Resources.Msg_LanguageRestartPrompt,
                Properties.Resources.Title_Prompt, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;

            try {
                configService.SaveSystemConfig(CaptureSystemConfig());
            }
            catch (Exception ex) {
                Logger.Warn("保存系统配置失败",
                    context: Logger.Context(("config", "system"), ("reason", ex.Message)));
            }

            Application.Restart();
            Environment.Exit(0);
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            base.OnFormClosed(e);
            autoCompleteHelper?.Dispose();
            tagDatabase?.Dispose();
        }

        #region 固定画师，随机画师，随机提示词快速插入

        private void InitGrpEventArgs() {
            grpArtistFixed.MouseHover += EventGRBMouseHover;
            grpArtistFixed.MouseLeave += EventGRBMouseLeave;
            grpArtistFixed.MouseClick += EventGRBMouseClick;
            grpArtistRandom.MouseHover += EventGRBMouseHover;
            grpArtistRandom.MouseLeave += EventGRBMouseLeave;
            grpArtistRandom.MouseClick += EventGRBMouseClick;
        }

        private void EventGRBMouseHover(object sender, EventArgs e) {
            GroupBox groupBox = sender as GroupBox;
            if (groupBox == grpArtistFixed) {
                groupBox.Text = Properties.Resources.Hover_InsertRemoveFixedArtist;
                return;
            }
            else if (groupBox == grpArtistRandom) {
                groupBox.Text = Properties.Resources.Hover_InsertRemoveRandomArtist;
                return;
            }

        }

        private void EventGRBMouseLeave(object sender, EventArgs e) {
            GroupBox groupBox = sender as GroupBox;
            if (groupBox == grpArtistFixed) {
                groupBox.Text = Properties.Resources.GroupBox_FixedArtist;
                return;
            }
            else if (groupBox == grpArtistRandom) {
                groupBox.Text = Properties.Resources.GroupBox_RandomArtist;
                return;
            }

        }

        private void EventGRBMouseClick(object sender, EventArgs e) {
            string insertPrompt = null;
            GroupBox groupBox = sender as GroupBox;
            if (groupBox == grpArtistFixed) {
                insertPrompt = "<固定画师>";
            }
            else if (groupBox == grpArtistRandom) {
                insertPrompt = "<随机画师>";
            }

            if (insertPrompt != null)
                Tools.InsertTextToTextBox(txtPrompt, insertPrompt);
        }

        private void AttachGenerationControllerEvents() {
            generationController.IterationStarted += HandleGenerationIterationStarted;
            generationController.ImageReady += HandleGenerationImageReady;
            generationController.DelayPlanned += HandleGenerationDelayPlanned;
            generationController.Completed += HandleGenerationCompleted;
            generationController.Cancelled += HandleGenerationCancelled;
            generationController.Failed += HandleGenerationFailed;
            generationController.Started += HandleGenerationStarted;
            generationController.Stopped += HandleGenerationStopped;
        }

        private void AttachDirectorToolEvents() {
            directorToolController.BusyStateChanged += HandleDirectorToolBusyStateChanged;
            directorToolController.PreviewUpdated += HandleDirectorToolPreviewUpdated;
            directorToolController.OutputUpdated += HandleDirectorToolOutputUpdated;
            directorToolController.Completed += HandleDirectorToolCompleted;
            directorToolController.Failed += HandleDirectorToolFailed;
        }

        private void HandleGenerationStarted() {
            if (InvokeRequired) {
                BeginInvoke(new Action(HandleGenerationStarted));
                return;
            }

            SetGenerateButtonRunning(true);
            btnGenerate.Enabled = true;
        }

        private void HandleGenerationStopped() {
            if (InvokeRequired) {
                BeginInvoke(new Action(HandleGenerationStopped));
                return;
            }

            ResetGenerationState();
        }

        private void HandleDirectorToolBusyStateChanged(bool isBusy) {
            if (InvokeRequired) {
                BeginInvoke(new Action<bool>(HandleDirectorToolBusyStateChanged), isBusy);
                return;
            }

            btnDirectorToolsRemoveBGRun.Text = isBusy
                ? Properties.Resources.Button_Running
                : Properties.Resources.Button_Run;
            btnDirectorToolsRemoveBGRun.Enabled = !isBusy;
        }

        private void HandleDirectorToolPreviewUpdated(Image preview) {
            if (InvokeRequired) {
                BeginInvoke(new Action<Image>(HandleDirectorToolPreviewUpdated), preview);
                return;
            }

            ReplacePictureBoxImage(picDirectorToolsInput, preview);
        }

        private void HandleDirectorToolOutputUpdated(Image image) {
            if (InvokeRequired) {
                BeginInvoke(new Action<Image>(HandleDirectorToolOutputUpdated), image);
                return;
            }

            ReplacePictureBoxImage(picDirectorToolsOutput, image);
        }

        private void HandleDirectorToolCompleted() {
            if (InvokeRequired) {
                BeginInvoke(new Action(HandleDirectorToolCompleted));
                return;
            }

            Logger.Info("导演工具任务完成");
        }

        private void HandleDirectorToolFailed(Exception exception) {
            if (InvokeRequired) {
                BeginInvoke(new Action<Exception>(HandleDirectorToolFailed), exception);
                return;
            }

            if (exception == null)
                return;

            if (exception is OperationCanceledException)
                return;

            MessageBox.Show(Properties.Resources.Msg_DirectorToolFailed, Properties.Resources.Title_Error,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ResetGenerationState() {
            SetGenerateButtonRunning(false);
            btnGenerate.Enabled = true;
        }

        private void btnGenerate_Click(object sender, EventArgs e) {
            if (generationController.IsGenerating) {
                RequestStopGeneration();
                return;
            }

            try {
                generationController.StartGeneration();
            }
            catch (Exception ex) {
                Logger.Error("构建生成参数失败", exception: ex,
                    context: Logger.Context(("action", "StartGeneration")));
                MessageBox.Show(Properties.Resources.Msg_InvalidGenerationParams, Properties.Resources.Title_Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void picView_Click(object sender, EventArgs e) {
            try {
                System.Diagnostics.Process.Start(picProps.OutputPath);
            }
            catch (Exception ex) {
                Logger.Warn("无法打开输出目录",
                    context: Logger.Context(("path", picProps.OutputPath), ("reason", ex.Message)));
            }
        }

        private void RequestStopGeneration() {
            btnGenerate.Enabled = false;
            RefreshAnlasButtonText();
            generationController.RequestStopGeneration();
        }

        private void HandleGenerationIterationStarted(int iteration) {
            if (InvokeRequired) {
                BeginInvoke(new Action<int>(HandleGenerationIterationStarted), iteration);
                return;
            }

            propertyGrid1.Refresh();
        }

        private void HandleGenerationImageReady(int iteration, Bitmap bitmap) {
            if (bitmap == null)
                return;

            if (InvokeRequired) {
                BeginInvoke(new Action<int, Bitmap>(HandleGenerationImageReady), iteration, bitmap);
                return;
            }

            if (settingProps.ClosePicPreview) {
                bitmap.Dispose();
                return;
            }

            if (picView.Image != null) {
                picView.Image.Dispose();
                picView.Image = null;
            }

            picView.Image = bitmap;
        }

        private void HandleGenerationDelayPlanned(int iteration, DelayInfo delayInfo, string prompt) {
            // 保留扩展接口，当前无需额外 UI 行为
        }

        private void HandleGenerationCompleted() {
            if (InvokeRequired) {
                BeginInvoke(new Action(HandleGenerationCompleted));
                return;
            }

            ResetGenerationState();
        }

        private void HandleGenerationCancelled() {
            if (InvokeRequired) {
                BeginInvoke(new Action(HandleGenerationCancelled));
                return;
            }

            ResetGenerationState();
        }

        private void HandleGenerationFailed(Exception ex) {
            if (InvokeRequired) {
                BeginInvoke(new Action<Exception>(HandleGenerationFailed), ex);
                return;
            }

            Logger.Error("生成任务发生未处理异常", exception: ex,
                context: Logger.Context(("stage", "pipeline")));
            ResetGenerationState();
        }

        #endregion

        #region 详情页

        private void btnGetMorePrompt_Click(object sender, EventArgs e) {
            System.Diagnostics.Process.Start("https://pan.baidu.com/s/1CTFTVIo7vKzDRy62LNxMMw?pwd=ktur");
        }

        private void btnGetRollDoc_Click(object sender, EventArgs e) {
            System.Diagnostics.Process.Start("https://docs.qq.com/sheet/DRFdBdGxZaXdkc3pP?tab=7mb6q1");
        }

        private void btnParsePrompt_Click(object sender, EventArgs e) {
            System.Diagnostics.Process.Start("https://spell.novelai.dev/");
        }

        private void btnPushBackPic_Click(object sender, EventArgs e) {
            System.Diagnostics.Process.Start("https://huggingface.co/spaces/SmilingWolf/wd-tagger");
        }

        private void btnTutorial_Click(object sender, EventArgs e) {
            System.Diagnostics.Process.Start("https://cyanautumn.github.io/NovalAi3AutoMaticDoc/");
        }

        private void btnDocToolsBook_Click(object sender, EventArgs e) {
            System.Diagnostics.Process.Start("https://docs.qq.com/doc/p/230e7ada2a60d8e347d639edd5521f5e62332fe9");
        }

        private void btnDocGithub_Click(object sender, EventArgs e) {
            System.Diagnostics.Process.Start("https://docs.qq.com/doc/p/230e7ada2a60d8e347d639edd5521f5e62332fe9");
        }

        #endregion

        private int resizeAreaSize = 10;
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST) {
                // 获取鼠标相对于窗体的位置
                int x = (m.LParam.ToInt32() & 0xFFFF);
                int y = (m.LParam.ToInt32() >> 16) & 0xFFFF;
                var clientPos = this.PointToClient(new System.Drawing.Point(x, y));

                // 判断在哪个边缘
                if (clientPos.X <= resizeAreaSize && clientPos.Y <= resizeAreaSize)
                    m.Result = (IntPtr)HTTOPLEFT;
                else if (clientPos.X >= this.ClientSize.Width - resizeAreaSize && clientPos.Y <= resizeAreaSize)
                    m.Result = (IntPtr)HTTOPRIGHT;
                else if (clientPos.X <= resizeAreaSize && clientPos.Y >= this.ClientSize.Height - resizeAreaSize)
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (clientPos.X >= this.ClientSize.Width - resizeAreaSize &&
                         clientPos.Y >= this.ClientSize.Height - resizeAreaSize)
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (clientPos.Y <= resizeAreaSize)
                    m.Result = (IntPtr)HTTOP;
                else if (clientPos.Y >= this.ClientSize.Height - resizeAreaSize)
                    m.Result = (IntPtr)HTBOTTOM;
                else if (clientPos.X <= resizeAreaSize)
                    m.Result = (IntPtr)HTLEFT;
                else if (clientPos.X >= this.ClientSize.Width - resizeAreaSize)
                    m.Result = (IntPtr)HTRIGHT;
                else
                    m.Result = (IntPtr)HTCLIENT; // 其他区域
            }
        }
    }
}
