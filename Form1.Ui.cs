using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using AutoNai3Tools.body;
using AutoNai3Tools.Services;
using AutoNai3Tools.utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoNai3Tools {
    public partial class Form1 {
        #region 界面状态（替代原来的 WinForms 控件）

        internal string PromptText { get; set; } =
            " <原神>,<固定画师>,<随机画师>,1girl,loli,solo,catgirl,white hair,blue eyes,<衣服>";

        internal string NegativePromptText { get; set; } =
            "nsfw, lowres, {bad}, error, fewer, extra, missing, worst quality, jpeg artifacts, bad quality, " +
            "watermark, unfinished, displeasing, chromatic aberration, signature, extra digits, artistic error, " +
            "username, scan, [abstract],{{weibo_username}},female_pubic_hair,{{weibo_logo}},low quality,@_@," +
            "chibi, character doll,multiple girls,{{{{{chibi,doll}}}}}";

        internal string ArtistFixedText { get; set; } = "artist:画师1,artist:画师2";

        internal string ArtistRandomText { get; set; } = "画师A\n画师B,1,2,1,2\n画师C,0,0,1,2\n画师D,0,0,1,2|画师E,1,2,0,0";

        internal int DefaultArtistWeightReduceMax { get; set; } = 1;
        internal int DefaultArtistWeightIncreaseMax { get; set; } = 1;
        internal double DefaultArtistWeightReduceDoubleColonMax { get; set; } = 1;
        internal double DefaultArtistWeightIncreaseDoubleColonMax { get; set; } = 1;
        internal bool ArtistModify { get; set; }
        internal int ArtistMin { get; set; } = 1;
        internal int ArtistMax { get; set; } = 2;

        private bool isGenerating;

        internal bool IsGenerating => isGenerating;

        internal readonly List<VibeConfigData> vibeItems = new List<VibeConfigData>();
        internal int VibeSelectedIndex { get; set; } = -1;

        internal string Img2ImgPath { get; set; }
        internal float Img2ImgStrength { get; set; } = 0.01f;
        internal float Img2ImgNoise { get; set; }

        internal string DirectorInputPath { get; set; }
        internal string DirectorFolderPath { get; set; } = ".\\output";
        internal int DirectorIterations { get; set; } = 1;
        internal string ColorizePrompt { get; set; } = string.Empty;
        internal int ColorizeDefry { get; set; }
        internal string EmotionValue { get; set; } = EmotionOptions[0];
        internal string EmotionPrompt { get; set; } = string.Empty;
        internal int EmotionDefry { get; set; }
        internal int DirectorTab { get; set; }
        internal bool DirectorBatchMode { get; set; }

        internal readonly List<WildcardSnippet> wildcardItems = new List<WildcardSnippet>();
        internal string CurrentConfigName { get; set; } = string.Empty;

        internal string UiTheme { get; private set; } = "dark";

        internal static readonly string[] EmotionOptions = {
            "neutral", "happy", "sad", "angry", "scared", "surprised", "tried", "excited", "nervous",
            "thinking", "confused", "shy", "worried", "love", "determined", "hurt", "playful", "disgusted",
            "smug", "bored", "laughing", "irritated", "aroused", "embarrassed"
        };

        internal static readonly string[] EmotionDefryOptions = {
            "Normal", "Slightly Weak", "Weak", "Even Weaker", "Very Weak", "Weakest"
        };

        internal static readonly string[] ColorizeDefryOptions = { "1", "2", "3", "4", "5" };

        private static string ThemeFilePath => Path.Combine(AppPaths.LocalRoot, "ui.json");

        internal void LoadUiTheme() {
            try {
                if (!File.Exists(ThemeFilePath))
                    return;
                var obj = JObject.Parse(File.ReadAllText(ThemeFilePath));
                var theme = obj["theme"]?.ToString();
                if (!string.IsNullOrWhiteSpace(theme))
                    UiTheme = theme;
            }
            catch {
                // 主题读取失败就用默认值
            }
        }

        internal void SaveUiTheme(string theme) {
            if (string.IsNullOrWhiteSpace(theme))
                return;

            UiTheme = theme;
            try {
                Directory.CreateDirectory(AppPaths.LocalRoot);
                File.WriteAllText(ThemeFilePath,
                    new JObject { ["theme"] = theme }.ToString(Formatting.Indented));
            }
            catch (Exception ex) {
                Logger.Warn("保存界面主题失败",
                    context: Logger.Context(("reason", ex.Message)));
            }
        }

        internal List<VibeSelection> BuildVibeSelections() {
            var selections = new List<VibeSelection>();
            foreach (var vibe in vibeItems) {
                if (vibe == null || !vibe.Enabled || string.IsNullOrWhiteSpace(vibe.Path))
                    continue;
                selections.Add(new VibeSelection(vibe.Path, vibe.IE, vibe.RS));
            }

            return selections;
        }

        internal Img2ImgOptions CaptureImg2ImgOptions() {
            if (string.IsNullOrWhiteSpace(Img2ImgPath) || !File.Exists(Img2ImgPath))
                return null;

            return new Img2ImgOptions(Img2ImgPath, Img2ImgStrength, Img2ImgNoise);
        }

        #endregion

        #region 状态快照与推送

        internal JObject BuildState() {
            var state = new JObject {
                ["version"] = GetDisplayVersion(),
                ["theme"] = UiTheme,
                ["texts"] = new JObject {
                    ["prompt"] = PromptText,
                    ["negativePrompt"] = NegativePromptText,
                    ["artistFixed"] = ArtistFixedText,
                    ["artistRandom"] = ArtistRandomText
                },
                ["artist"] = BuildArtistJson(),
                ["pic"] = BuildPicPropsJson(),
                ["picDescriptors"] = BuildDescriptors(picProps),
                ["settings"] = BuildSettingPropsJson(),
                ["settingDescriptors"] = BuildDescriptors(settingProps),
                ["resolutionOptions"] = new JArray(picProps.GetResolutionOptions().Cast<object>()),
                ["models"] = BuildModelOptions(),
                ["emotions"] = new JArray(EmotionOptions.Cast<object>()),
                ["emotionDefry"] = new JArray(EmotionDefryOptions.Cast<object>()),
                ["colorizeDefry"] = new JArray(ColorizeDefryOptions.Cast<object>()),
                ["vibes"] = BuildVibeJson(),
                ["vibeSelected"] = VibeSelectedIndex,
                ["wildcardFolder"] = picProps.WildcardFolderPath ?? string.Empty,
                ["wildcards"] = BuildWildcardJson(),
                ["configs"] = BuildConfigJson(),
                ["img2img"] = BuildImg2ImgJson(),
                ["director"] = BuildDirectorJson(),
                ["anlas"] = BuildAnlasJson(),
                ["picInfo"] = lastPicInfo,
                ["log"] = SnapshotBufferedLogs()
            };

            return state;
        }

        private JArray SnapshotBufferedLogs() {
            var array = new JArray();
            lock (logLock) {
                foreach (var message in bufferedLogs) {
                    if (message["type"]?.ToString() != "log")
                        continue;
                    array.Add(message["data"]?.DeepClone() ?? new JObject());
                }
            }

            return array;
        }

        internal JObject BuildArtistJson() {
            return new JObject {
                ["fixed"] = ArtistFixedText,
                ["random"] = ArtistRandomText,
                ["reduceMax"] = DefaultArtistWeightReduceMax,
                ["increaseMax"] = DefaultArtistWeightIncreaseMax,
                ["reduceDoubleColonMax"] = DefaultArtistWeightReduceDoubleColonMax,
                ["increaseDoubleColonMax"] = DefaultArtistWeightIncreaseDoubleColonMax,
                ["modify"] = ArtistModify,
                ["min"] = ArtistMin,
                ["max"] = ArtistMax
            };
        }

        internal JObject BuildPicPropsJson() {
            var json = new JObject();
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(picProps)) {
                json[descriptor.Name] = ToJsonValue(descriptor.GetValue(picProps));
            }

            return json;
        }

        internal JObject BuildSettingPropsJson() {
            var json = new JObject();
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(settingProps)) {
                json[descriptor.Name] = ToJsonValue(descriptor.GetValue(settingProps));
            }

            return json;
        }

        internal JArray BuildVibeJson() {
            var array = new JArray();
            foreach (var vibe in vibeItems) {
                array.Add(new JObject {
                    ["enabled"] = vibe.Enabled,
                    ["name"] = vibe.Name,
                    ["ie"] = vibe.IE,
                    ["rs"] = vibe.RS,
                    ["path"] = vibe.Path
                });
            }

            return array;
        }

        internal JArray BuildWildcardJson() {
            var array = new JArray();
            foreach (var snippet in wildcardItems) {
                array.Add(new JObject {
                    ["name"] = snippet.Name,
                    ["content"] = snippet.Content
                });
            }

            return array;
        }

        internal JObject BuildConfigJson() {
            var names = new JArray();
            try {
                foreach (var name in configService.GetPresetNames())
                    names.Add(name);
            }
            catch (Exception ex) {
                Logger.Warn("读取配置列表失败",
                    context: Logger.Context(("reason", ex.Message)));
            }

            return new JObject {
                ["names"] = names,
                ["current"] = CurrentConfigName
            };
        }

        internal JObject BuildImg2ImgJson() {
            return new JObject {
                ["path"] = Img2ImgPath ?? string.Empty,
                ["name"] = string.IsNullOrWhiteSpace(Img2ImgPath) ? string.Empty : Path.GetFileName(Img2ImgPath),
                ["strength"] = (double)Img2ImgStrength,
                ["noise"] = (double)Img2ImgNoise
            };
        }

        internal JObject BuildDirectorJson() {
            return new JObject {
                ["input"] = DirectorInputPath ?? string.Empty,
                ["inputName"] = string.IsNullOrWhiteSpace(DirectorInputPath)
                    ? string.Empty
                    : Path.GetFileName(DirectorInputPath),
                ["folder"] = DirectorFolderPath ?? string.Empty,
                ["iterations"] = DirectorIterations,
                ["colorizePrompt"] = ColorizePrompt,
                ["colorizeDefry"] = ColorizeDefry,
                ["emotion"] = EmotionValue,
                ["emotionPrompt"] = EmotionPrompt,
                ["emotionDefry"] = EmotionDefry,
                ["tab"] = DirectorTab,
                ["batch"] = DirectorBatchMode
            };
        }

        internal void PushState() {
            Post("state", BuildState());
        }

        internal void PushPicProps() {
            Post("pic", BuildPicPropsJson());
        }

        internal void PushSettings() {
            Post("settings", BuildSettingPropsJson());
        }

        internal void PushVibes() {
            Post("vibes", new JObject {
                ["items"] = BuildVibeJson(),
                ["selected"] = VibeSelectedIndex
            });

            // 选中项变化时把参考图预览和可用的「信息抽取」档位一起送给前端
            VibeConfigData selected = null;
            if (VibeSelectedIndex >= 0 && VibeSelectedIndex < vibeItems.Count)
                selected = vibeItems[VibeSelectedIndex];

            Post("vibe-preview", new JObject {
                ["url"] = selected == null ? null : CachePreviewFile(selected.Path, "vibe"),
                ["options"] = selected == null ? new JArray() : GetVibeInformationOptions(selected.Path)
            });
        }

        internal void PushWildcards() {
            Post("wildcards", new JObject {
                ["folder"] = picProps.WildcardFolderPath ?? string.Empty,
                ["items"] = BuildWildcardJson()
            });
        }

        internal void PushConfigs() {
            Post("configs", BuildConfigJson());
        }

        internal void PushImg2Img() {
            Post("img2img", BuildImg2ImgJson());
        }

        internal void PushDirector() {
            Post("director", BuildDirectorJson());
        }

        internal void PushToast(string level, string text) {
            Post("toast", new JObject { ["level"] = level, ["text"] = text });
        }

        #endregion

        #region 属性描述（替代 PropertyGrid）

        private static readonly Dictionary<string, string> PropertyHints =
            new Dictionary<string, string>(StringComparer.Ordinal) {
                ["ResolutionList"] = "multiline",
                ["ResolutionSelection"] = "resolution",
                ["PromptBlackList"] = "multiline",
                ["PromptBlackListRegex"] = "multiline",
                ["RandomPromptFolderPath"] = "folder",
                ["WildcardFolderPath"] = "folder",
                ["OutputPath"] = "folder",
                ["Seeds"] = "long",
                ["Steps"] = "steps",
                ["Scale"] = "scale",
                ["Token"] = "password",
                ["Api"] = "url"
            };

        /// <summary>
        /// 数值属性的取值范围，与旧版行为保持一致：
        /// PicProperty 的 setter 会把 Steps 截断到 1~28、Scale 截断到 0~10 并保留一位小数。
        /// </summary>
        private static readonly Dictionary<string, PropertyRange> PropertyRanges =
            new Dictionary<string, PropertyRange>(StringComparer.Ordinal) {
                ["Steps"] = new PropertyRange(1, 28, 1),
                ["Scale"] = new PropertyRange(0, 10, 0.1)
            };

        private sealed class PropertyRange {
            public PropertyRange(double min, double max, double step) {
                Min = min;
                Max = max;
                Step = step;
            }

            public double Min { get; }
            public double Max { get; }
            public double Step { get; }
        }

        internal JArray BuildDescriptors(object target) {
            var array = new JArray();
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(target)) {

                var item = new JObject {
                    ["name"] = descriptor.Name,
                    ["label"] = ResolveDisplayName(descriptor),
                    ["category"] = ResolveCategory(descriptor),
                    ["type"] = ResolveTypeName(descriptor.PropertyType),
                    ["value"] = ToJsonValue(descriptor.GetValue(target))
                };

                string hint;
                if (PropertyHints.TryGetValue(descriptor.Name, out hint))
                    item["hint"] = hint;

                PropertyRange range;
                if (PropertyRanges.TryGetValue(descriptor.Name, out range)) {
                    item["min"] = range.Min;
                    item["max"] = range.Max;
                    item["step"] = range.Step;
                }

                if (descriptor.PropertyType.IsEnum) {
                    item["options"] = BuildEnumOptions(descriptor.PropertyType);
                }

                if (descriptor.Name == "ResolutionSelection") {
                    item["options"] = new JArray(picProps.GetResolutionOptions().Cast<object>());
                }

                array.Add(item);
            }

            return array;
        }

        private static JArray BuildEnumOptions(Type enumType) {
            var options = new JArray();
            if (enumType == typeof(BodyTools.Model))
                return BuildModelOptions();

            foreach (var value in Enum.GetValues(enumType)) {
                string description = BodyTools.GetEnumDescription((Enum)value);
                options.Add(new JObject {
                    ["value"] = value.ToString(),
                    ["label"] = string.IsNullOrWhiteSpace(description) ? value.ToString() : description
                });
            }

            return options;
        }

        internal static JArray BuildModelOptions() {
            var options = new JArray();
            foreach (var model in BodyTools.SelectableModels) {
                options.Add(new JObject {
                    ["value"] = model.ToString(),
                    ["label"] = BodyTools.GetEnumDescription(model),
                    ["v5"] = AnlasService.IsV5Model(BodyTools.GetEnumDescription(model))
                });
            }

            return options;
        }

        private static string ResolveDisplayName(PropertyDescriptor descriptor) {
            var attribute = descriptor.Attributes[typeof(DisplayNameAttribute)] as DisplayNameAttribute;
            return string.IsNullOrWhiteSpace(attribute?.DisplayName) ? descriptor.Name : attribute.DisplayName;
        }

        private static string ResolveCategory(PropertyDescriptor descriptor) {
            return descriptor.Category ?? string.Empty;
        }

        private static string ResolveTypeName(Type type) {
            if (type == typeof(bool))
                return "bool";
            if (type.IsEnum)
                return "enum";
            if (type == typeof(int) || type == typeof(long))
                return "int";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "float";
            return "string";
        }

        internal static JToken ToJsonValue(object value) {
            if (value == null)
                return JValue.CreateNull();
            if (value is Enum)
                return new JValue(value.ToString());
            if (value is bool boolean)
                return new JValue(boolean);
            if (value is string text)
                return new JValue(text);
            if (value is float single)
                return new JValue((double)single);
            if (value is double dbl)
                return new JValue(dbl);
            if (value is decimal dec)
                return new JValue((double)dec);
            if (value is int intValue)
                return new JValue(intValue);
            if (value is long longValue)
                return new JValue(longValue);

            return new JValue(value.ToString());
        }

        private string GetDisplayVersion() {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
                return Properties.Resources.AppVersionUnknown;
            return version.Revision <= 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : version.ToString();
        }

        #endregion

        #region 属性写入

        internal bool SetPicProperty(string name, JToken value) {
            bool changed = SetPropertyValue(picProps, name, value);
            if (!changed)
                return false;

            if (name == nameof(PicProperty.WildcardFolderPath))
                ReloadWildcards();

            PushPicProps();
            PushAnlasEstimate();
            return true;
        }

        internal bool SetSettingProperty(string name, JToken value) {
            bool changed = SetPropertyValue(settingProps, name, value);
            if (!changed)
                return false;

            PushSettings();

            if (name == nameof(SettingProperty.AnlasTracking)) {
                OnAnlasTrackingChanged();
            }
            else if (name == nameof(SettingProperty.UiLanguage)) {
                PromptLanguageRestart();
            }

            return true;
        }

        private static bool SetPropertyValue(object target, string name, JToken value) {
            if (target == null || string.IsNullOrWhiteSpace(name))
                return false;

            var descriptor = TypeDescriptor.GetProperties(target).Find(name, false);
            if (descriptor == null || descriptor.IsReadOnly)
                return false;

            try {
                object converted = ConvertToken(value, descriptor.PropertyType);
                descriptor.SetValue(target, converted);
                return true;
            }
            catch (Exception ex) {
                Logger.Warn($"设置 {name} 失败",
                    context: Logger.Context(("value", value?.ToString()), ("reason", ex.Message)));
                return false;
            }
        }

        private static object ConvertToken(JToken value, Type targetType) {
            if (targetType == typeof(string))
                return value == null || value.Type == JTokenType.Null ? null : value.ToString();

            if (targetType == typeof(bool))
                return value != null && value.Type != JTokenType.Null && value.Value<bool>();

            if (targetType.IsEnum) {
                if (value == null || value.Type == JTokenType.Null)
                    return Activator.CreateInstance(targetType);
                if (value.Type == JTokenType.Integer)
                    return Enum.ToObject(targetType, value.Value<int>());
                return Enum.Parse(targetType, value.ToString(), true);
            }

            if (targetType == typeof(int))
                return Convert.ToInt32(value.Value<long>(), CultureInfo.InvariantCulture);
            if (targetType == typeof(long))
                return value.Value<long>();
            if (targetType == typeof(float))
                return (float)value.Value<double>();
            if (targetType == typeof(double))
                return value.Value<double>();
            if (targetType == typeof(decimal))
                return (decimal)value.Value<double>();

            return Convert.ChangeType(value.ToString(), targetType, CultureInfo.InvariantCulture);
        }

        private void PromptLanguageRestart() {
            var result = MessageBox.Show(Properties.Resources.Msg_LanguageRestartPrompt,
                Properties.Resources.Title_Prompt, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;

            SaveConfigs();
            Application.Restart();
            Environment.Exit(0);
        }

        #endregion
    }
}
