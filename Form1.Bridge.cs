using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AutoNai3Tools.utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;

namespace AutoNai3Tools {
    public partial class Form1 {
        #region 前端指令分发

        private static readonly string[] ImageExtensions =
            { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif", ".tif", ".tiff" };

        private void HandleWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
            JObject message;
            try {
                message = JObject.Parse(e.WebMessageAsJson);
            }
            catch (Exception ex) {
                Logger.Warn("无法解析前端消息", context: Logger.Context(("reason", ex.Message)));
                return;
            }

            string command = message["cmd"]?.ToString();
            var data = message["data"] as JObject ?? new JObject();

            try {
                DispatchWebCommand(command, data);
            }
            catch (Exception ex) {
                Logger.Error($"处理前端指令失败：{command}", exception: ex);
                PushToast("err", ex.Message);
            }
        }

        private void DispatchWebCommand(string command, JObject data) {
            switch (command) {
                case "ready":
                    MarkWebUiReady();
                    PushState();
                    BeginStartupAnlasRefresh();
                    break;

                case "state:get":
                    PushState();
                    break;

                case "window:minimize":
                    MinimizeWindow();
                    break;
                case "window:maximize":
                    ToggleMaximize();
                    break;
                case "window:close":
                    Close();
                    break;
                case "window:drag":
                    BeginWindowDrag();
                    break;

                case "theme:set":
                    SaveUiTheme(data["theme"]?.ToString());
                    break;

                case "text:set":
                    SetText(data["field"]?.ToString(), data["value"]?.ToString());
                    break;

                case "artist:set":
                    SetArtistField(data["name"]?.ToString(), data["value"]);
                    break;

                case "param:set":
                    SetPicProperty(data["name"]?.ToString(), data["value"]);
                    break;

                case "setting:set":
                    SetSettingProperty(data["name"]?.ToString(), data["value"]);
                    break;

                case "generate":
                    StartGeneration();
                    break;
                case "stop":
                    RequestStopGeneration();
                    break;

                case "anlas:query":
                    _ = QueryAnlasAsync(true);
                    break;
                case "anlas:refresh":
                    PushAnlasEstimate();
                    break;

                case "output:open":
                    OpenOutputFolder();
                    break;

                case "config:list":
                    PushConfigs();
                    break;
                case "config:save":
                    SavePreset(data["name"]?.ToString());
                    break;
                case "config:load":
                    LoadPreset(data["name"]?.ToString());
                    break;
                case "config:delete":
                    DeletePreset(data["name"]?.ToString());
                    break;
                case "config:openFolder":
                    OpenPresetFolder();
                    break;

                case "wildcard:reload":
                    ReloadWildcards();
                    break;
                case "wildcard:add":
                    AddWildcard(data["name"]?.ToString(), data["content"]?.ToString());
                    break;
                case "wildcard:update":
                    UpdateWildcard(data["name"]?.ToString(), data["content"]?.ToString());
                    break;
                case "wildcard:delete":
                    DeleteWildcard(data["name"]?.ToString());
                    break;
                case "wildcard:openFolder":
                    OpenWildcardFolder();
                    break;

                case "vibe:pick":
                    PickVibeFile();
                    break;
                case "vibe:options":
                    Post("vibe-options", new JObject {
                        ["path"] = data["path"]?.ToString(),
                        ["options"] = GetVibeInformationOptions(data["path"]?.ToString())
                    });
                    break;
                case "vibe:add":
                    AddVibe(data["path"]?.ToString(), ClampFloat(data["ie"], 0.01f, 1f, 1f),
                        ClampFloat(data["rs"], 0.01f, 1f, 0.6f));
                    break;
                case "vibe:bundle":
                    ImportVibeBundle(data["path"]?.ToString());
                    break;
                case "vibe:update":
                    UpdateVibe(data["index"]?.Value<int>() ?? -1, ClampFloat(data["ie"], 0.01f, 1f, 1f),
                        ClampFloat(data["rs"], 0.01f, 1f, 0.6f), data["enabled"]?.Value<bool>() ?? true,
                        data["name"]?.ToString());
                    break;
                case "vibe:delete":
                    DeleteVibe(data["index"]?.Value<int>() ?? -1);
                    break;
                case "vibe:select":
                    SelectVibe(data["index"]?.Value<int>() ?? -1);
                    break;

                case "img2img:pick":
                    PickImg2ImgFile();
                    break;
                case "img2img:clear":
                    ClearImg2Img();
                    break;
                case "img2img:set":
                    Img2ImgStrength = ClampFloat(data["strength"], 0.01f, 0.99f, Img2ImgStrength);
                    Img2ImgNoise = ClampFloat(data["noise"], 0f, 0.99f, Img2ImgNoise);
                    PushImg2Img();
                    break;

                case "director:set":
                    SetDirectorField(data);
                    break;
                case "director:pickInput":
                    PickDirectorInputFile();
                    break;
                case "director:pickFolder":
                    PickDirectorFolder();
                    break;
                case "director:run":
                    _ = RunDirectorToolAsync();
                    break;

                case "metadata:import":
                    ImportMetadataFromBase64(data["name"]?.ToString(), data["base64"]?.ToString());
                    break;

                case "tag:suggest":
                    Post("tag-suggest", new JObject {
                        ["prefix"] = data["prefix"]?.ToString(),
                        ["items"] = SuggestTags(data["prefix"]?.ToString())
                    });
                    break;

                case "path:pickFolder":
                    PickFolder(data["initial"]?.ToString());
                    break;

                case "folder:open":
                    OpenFolder(data["path"]?.ToString());
                    break;

                case "open:url":
                    OpenExternal(data["url"]?.ToString());
                    break;

                case "log:clear":
                    lock (logLock) {
                        bufferedLogs.Clear();
                    }
                    break;

                default:
                    Logger.Warn($"未知的前端指令：{command}");
                    break;
            }
        }

        private void SetText(string field, string value) {
            switch (field) {
                case "prompt":
                    PromptText = value ?? string.Empty;
                    break;
                case "negativePrompt":
                    NegativePromptText = value ?? string.Empty;
                    break;
                case "artistFixed":
                    ArtistFixedText = value ?? string.Empty;
                    break;
                case "artistRandom":
                    ArtistRandomText = value ?? string.Empty;
                    break;
                default:
                    return;
            }

            Post("text-ack", new JObject { ["field"] = field, ["value"] = value ?? string.Empty });
        }

        private void SetArtistField(string name, JToken value) {
            switch (name) {
                case "fixed":
                    ArtistFixedText = value?.ToString() ?? string.Empty;
                    break;
                case "random":
                    ArtistRandomText = value?.ToString() ?? string.Empty;
                    break;
                case "reduceMax":
                    DefaultArtistWeightReduceMax = ClampInt(value, 0, 99, DefaultArtistWeightReduceMax);
                    break;
                case "increaseMax":
                    DefaultArtistWeightIncreaseMax = ClampInt(value, 0, 99, DefaultArtistWeightIncreaseMax);
                    break;
                case "reduceDoubleColonMax":
                    DefaultArtistWeightReduceDoubleColonMax =
                        ClampDouble(value, 0, 99, DefaultArtistWeightReduceDoubleColonMax);
                    break;
                case "increaseDoubleColonMax":
                    DefaultArtistWeightIncreaseDoubleColonMax =
                        ClampDouble(value, 0, 99, DefaultArtistWeightIncreaseDoubleColonMax);
                    break;
                case "modify":
                    ArtistModify = value?.Value<bool>() ?? ArtistModify;
                    break;
                case "min":
                    // 旧版 numArtistMin 的取值范围是 1~100
                    ArtistMin = ClampInt(value, 1, 100, ArtistMin);
                    break;
                case "max":
                    // 旧版 numArtistMax 的取值范围是 1~100
                    ArtistMax = ClampInt(value, 1, 100, ArtistMax);
                    break;
                default:
                    return;
            }

            Post("artist", BuildArtistJson());
        }

        private static int ClampInt(JToken value, int min, int max, int fallback) {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            int parsed = (int)Math.Round(value.Value<double>());
            return Math.Max(min, Math.Min(max, parsed));
        }

        private static double ClampDouble(JToken value, double min, double max, double fallback) {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            double parsed = value.Value<double>();
            return Math.Max(min, Math.Min(max, parsed));
        }

        private static float ClampFloat(JToken value, float min, float max, float fallback) {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            float parsed = value.Value<float>();
            return Math.Max(min, Math.Min(max, parsed));
        }

        private void SetDirectorField(JObject data) {
            string field = data["field"]?.ToString();
            JToken value = data["value"];
            switch (field) {
                case "input":
                    DirectorInputPath = value?.ToString();
                    break;
                case "folder":
                    DirectorFolderPath = value?.ToString();
                    break;
                case "iterations":
                    // 旧版 nudLineArtParseNum 的取值范围是 1~100
                    DirectorIterations = ClampInt(value, 1, 100, DirectorIterations);
                    break;
                case "colorizePrompt":
                    ColorizePrompt = value?.ToString() ?? string.Empty;
                    break;
                case "colorizeDefry":
                    ColorizeDefry = ClampInt(value, 0, ColorizeDefryOptions.Length - 1, ColorizeDefry);
                    break;
                case "emotion":
                    EmotionValue = value?.ToString() ?? EmotionValue;
                    break;
                case "emotionPrompt":
                    EmotionPrompt = value?.ToString() ?? string.Empty;
                    break;
                case "emotionDefry":
                    EmotionDefry = ClampInt(value, 0, EmotionDefryOptions.Length - 1, EmotionDefry);
                    break;
                case "tab":
                    DirectorTab = ClampInt(value, 0, 5, DirectorTab);
                    break;
                case "batch":
                    DirectorBatchMode = value?.Value<bool>() ?? DirectorBatchMode;
                    break;
                default:
                    return;
            }

            PushDirector();
        }

        private JArray SuggestTags(string prefix) {
            var array = new JArray();
            if (tagDatabase == null || string.IsNullOrWhiteSpace(prefix))
                return array;

            try {
                foreach (var tag in tagDatabase.SearchTags(prefix, 20))
                    array.Add(tag);
            }
            catch (Exception ex) {
                Logger.Warn("标签补全失败", context: Logger.Context(("reason", ex.Message)));
            }

            return array;
        }

        #endregion

        #region 生成控制

        private void StartGeneration() {
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
                PushToast("err", Properties.Resources.Msg_InvalidGenerationParams);
                Post("failed", new JObject { ["message"] = ex.Message });
            }
        }

        private void RequestStopGeneration() {
            if (!generationController.IsGenerating)
                return;

            generationController.RequestStopGeneration();
        }

        #endregion

        #region 文件选择

        private static bool IsImageFile(string path) {
            return !string.IsNullOrWhiteSpace(path) &&
                   ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        /// <summary>把本地图片复制到预览目录，前端才能通过 preview.local 显示它。</summary>
        internal string CachePreviewFile(string sourcePath, string prefix) {
            if (!IsImageFile(sourcePath) || !File.Exists(sourcePath))
                return null;

            try {
                Directory.CreateDirectory(AppPaths.PreviewRoot);
                string name = $"{prefix}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{Path.GetExtension(sourcePath)}";
                string target = Path.Combine(AppPaths.PreviewRoot, name);
                File.Copy(sourcePath, target, true);
                return "https://preview.local/" + name;
            }
            catch (Exception ex) {
                Logger.Warn("缓存预览图片失败",
                    context: Logger.Context(("path", sourcePath), ("reason", ex.Message)));
                return null;
            }
        }

        private void PickFolder(string initial) {
            var dialog = new FolderBrowserDialog {
                Description = "选择文件夹",
                SelectedPath = !string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial)
                    ? initial
                    : AppPaths.BaseDir
            };
            using (dialog) {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                Post("path-picked", new JObject { ["path"] = dialog.SelectedPath });
            }
        }

        private void OpenFolder(string path) {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start(path);
            }
            catch (Exception ex) {
                Logger.Warn("无法打开目录",
                    context: Logger.Context(("path", path), ("reason", ex.Message)));
            }
        }
        private void PickImg2ImgFile() {
            using (var dialog = new OpenFileDialog {
                Title = "选择图生图源图片",
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                CheckFileExists = true
            }) {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                Img2ImgPath = dialog.FileName;
                PushImg2Img();
                Post("img2img-preview", new JObject {
                    ["url"] = CachePreviewFile(dialog.FileName, "img2img")
                });
            }
        }

        private void PickVibeFile() {
            using (var dialog = new OpenFileDialog {
                Title = "选择参考图 / Vibe 文件",
                Filter = "参考图|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.naiv4vibe;*.naiv4vibebundle|所有文件|*.*",
                CheckFileExists = true
            }) {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string path = dialog.FileName;
                if (path.EndsWith(".naiv4vibebundle", StringComparison.OrdinalIgnoreCase)) {
                    ImportVibeBundle(path);
                    return;
                }

                Post("vibe-pick", new JObject {
                    ["path"] = path,
                    ["name"] = Path.GetFileName(path),
                    ["options"] = GetVibeInformationOptions(path),
                    ["preview"] = CachePreviewFile(path, "vibe")
                });
            }
        }

        private void PickDirectorInputFile() {
            using (var dialog = new OpenFileDialog {
                Title = "选择导演工具输入图片",
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif;*.tif;*.ico|所有文件|*.*",
                CheckFileExists = true
            }) {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                DirectorInputPath = dialog.FileName;
                PushDirector();
                Post("director-preview", new JObject {
                    ["url"] = CachePreviewFile(dialog.FileName, "director-in")
                });
            }
        }

        private void PickDirectorFolder() {
            using (var dialog = new FolderBrowserDialog {
                Description = "选择导演工具批处理目录",
                SelectedPath = Directory.Exists(DirectorFolderPath)
                    ? DirectorFolderPath
                    : AppPaths.BaseDir
            }) {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                DirectorFolderPath = dialog.SelectedPath;
                PushDirector();
            }
        }

        #endregion
    }
}
