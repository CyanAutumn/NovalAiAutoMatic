using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace AutoNai3Tools.utils {
    public class SettingProperty {
        [LocalizedCategory("Category_Auth")]
        [LocalizedDisplayName("Display_Api")]
        public string Api { get; set; } = ApiEndpoint.DefaultBaseUrl;

        [LocalizedCategory("Category_Auth")]
        [LocalizedDisplayName("Display_Token")]
        public string Token { get; set; }

        [LocalizedCategory("Category_Auth")]
        [LocalizedDisplayName("Display_Proxy")]
        public string Proxy { get; set; }

        [LocalizedCategory("Category_Auth")]
        [LocalizedDisplayName("Display_AnlasTracking")]
        public bool AnlasTracking { get; set; } = true;

        [LocalizedCategory("Category_Display")]
        [LocalizedDisplayName("Display_ClosePicPreview")]
        public bool ClosePicPreview { get; set; }

        [LocalizedCategory("Category_KeepParams")]
        [LocalizedDisplayName("Display_KeepRandomArtist")]
        public bool KeepRandomArtist { get; set; } = true;

        [LocalizedCategory("Category_KeepParams")]
        [LocalizedDisplayName("Display_KeepWildcard")]
        public bool KeepWildcard { get; set; } = true;

        [LocalizedCategory("Category_KeepParams")]
        [LocalizedDisplayName("Display_KeepRandomPrompt")]
        public bool KeepRandomPrompt { get; set; } = true;

        [LocalizedCategory("Category_KeepParams")]
        [LocalizedDisplayName("Display_KeepResolution")]
        public bool KeepResolution { get; set; } = true;

        [LocalizedCategory("Category_Sleep")]
        [LocalizedDisplayName("Display_SleepTimeShortLow")]
        public int SleepTimeShortLow { get; set; } = 5;

        [LocalizedCategory("Category_Sleep")]
        [LocalizedDisplayName("Display_SleepTimeShortHigh")]
        public int SleepTimeShortHigh { get; set; } = 8;

        [LocalizedCategory("Category_Sleep")]
        [LocalizedDisplayName("Display_SleepTimeLongLow")]
        public int SleepTimeLongLow { get; set; } = 20;

        [LocalizedCategory("Category_Sleep")]
        [LocalizedDisplayName("Display_SleepTimeLongHigh")]
        public int SleepTimeLongHigh { get; set; } = 25;

        [LocalizedCategory("Category_Output")]
        [LocalizedDisplayName("Display_OutputFileNameFormat")]
        [TypeConverter(typeof(OutputFileNameFormatConverter))]
        public OutputFileNameFormat OutputFileNameFormat { get; set; } = OutputFileNameFormat.NovalAI;

        [LocalizedCategory("Category_System")]
        [LocalizedDisplayName("Display_UiLanguage")]
        [TypeConverter(typeof(UiLanguageConverter))]
        public UiLanguage UiLanguage { get; set; } = UiLanguage.ChineseSimplified;
    }

    public enum OutputFileNameFormat {
        [Description("NovalAI")]
        NovalAI,
        [Description("全画师词")]
        AllArtists,
        [Description("日期")]
        DateTime
    }

    public class OutputFileNameFormatConverter : EnumConverter {
        public OutputFileNameFormatConverter() : base(typeof(OutputFileNameFormat)) {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value,
            Type destinationType) {
            if (destinationType == typeof(string) && value is OutputFileNameFormat format) {
                return GetDescription(format);
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
            if (value is string text) {
                foreach (OutputFileNameFormat format in Enum.GetValues(typeof(OutputFileNameFormat))) {
                    if (string.Equals(text, GetDescription(format), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(text, format.ToString(), StringComparison.OrdinalIgnoreCase))
                        return format;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }

        private static string GetDescription(OutputFileNameFormat value) {
            var localized = Properties.Resources.ResourceManager.GetString(
                $"OutputFileNameFormat_{value}", CultureInfo.CurrentUICulture);
            if (!string.IsNullOrWhiteSpace(localized)) {
                return localized;
            }

            var field = typeof(OutputFileNameFormat).GetField(value.ToString());
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? value.ToString();
        }
    }

    public enum UiLanguage {
        [Description("English")]
        English,
        [Description("简体中文")]
        ChineseSimplified,
        [Description("日本語")]
        Japanese
    }

    public static class UiLanguageExtensions {
        public static string ToCultureName(this UiLanguage language) {
            switch (language) {
                case UiLanguage.English:
                    return "en";
                case UiLanguage.Japanese:
                    return "ja-JP";
                default:
                    return "zh-CN";
            }
        }

        public static UiLanguage FromCultureName(string cultureName) {
            if (string.IsNullOrWhiteSpace(cultureName)) {
                return UiLanguage.ChineseSimplified;
            }

            var normalized = cultureName.Trim().ToLowerInvariant();
            if (normalized.StartsWith("en")) {
                return UiLanguage.English;
            }

            if (normalized.StartsWith("ja")) {
                return UiLanguage.Japanese;
            }

            if (normalized.StartsWith("zh")) {
                return UiLanguage.ChineseSimplified;
            }

            return UiLanguage.ChineseSimplified;
        }
    }

    public class UiLanguageConverter : EnumConverter {
        public UiLanguageConverter() : base(typeof(UiLanguage)) {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value,
            Type destinationType) {
            if (destinationType == typeof(string) && value is UiLanguage language) {
                return GetDescription(language);
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
            if (value is string text) {
                foreach (UiLanguage language in Enum.GetValues(typeof(UiLanguage))) {
                    if (string.Equals(text, GetDescription(language), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(text, language.ToString(), StringComparison.OrdinalIgnoreCase))
                        return language;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }

        private static string GetDescription(UiLanguage value) {
            var field = typeof(UiLanguage).GetField(value.ToString());
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? value.ToString();
        }
    }
}
