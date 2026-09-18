using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using AutoNai3Tools.utils;
using Newtonsoft.Json.Linq;

namespace AutoNai3Tools {
    public partial class Form1 {
        #region 从 PNG 导入生成参数

        internal void ImportMetadataFromFile(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                PushToast("warn", "文件不存在");
                return;
            }

            if (!ImageSourceMetadataReader.TryReadGenerationMetadata(filePath, out JObject metadata,
                    out string sourceLocation, out string errorMessage)) {
                Logger.Warn("读取源数据失败",
                    context: Logger.Context(("file", filePath), ("reason", errorMessage ?? "unknown")));
                PushToast("warn", errorMessage ?? "无法读取图片源数据。");
                return;
            }

            ApplyMetadataToUi(metadata);
            PushState();
            RecordPicInfo($"源数据位置: {sourceLocation}");
            Logger.Info("已按图片源数据更新生成参数",
                context: Logger.Context(("file", filePath), ("source", sourceLocation)));
            PushToast("ok", "已按图片源数据更新生成参数");
        }

        /// <summary>接收前端拖入的图片（base64），落到临时文件后复用原有的源数据解析。</summary>
        internal void ImportMetadataFromBase64(string fileName, string base64) {
            if (string.IsNullOrWhiteSpace(base64)) {
                PushToast("warn", "文件内容为空");
                return;
            }

            string tempDir = Path.Combine(AppPaths.LocalRoot, "temp");
            string path = null;
            try {
                Directory.CreateDirectory(tempDir);
                string safeName = string.IsNullOrWhiteSpace(fileName) ? "drop.png" : Path.GetFileName(fileName);
                path = Path.Combine(tempDir, Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + safeName);
                File.WriteAllBytes(path, Convert.FromBase64String(base64));
                ImportMetadataFromFile(path);
            }
            catch (Exception ex) {
                Logger.Warn("导入拖入的图片失败",
                    context: Logger.Context(("reason", ex.Message)));
                PushToast("err", "导入失败：" + ex.Message);
            }
            finally {
                try {
                    if (path != null && File.Exists(path))
                        File.Delete(path);
                }
                catch {
                    // 临时文件删除失败可以忽略
                }
            }
        }

        private void ApplyMetadataToUi(JObject metadata) {
            if (metadata == null)
                return;

            if (TryGetString(metadata, "prompt", out string prompt))
                PromptText = prompt;
            if (TryGetString(metadata, "uc", out string uc))
                NegativePromptText = uc;

            if (TryGetInt(metadata, "steps", out int steps))
                picProps.Steps = steps;
            if (TryGetInt(metadata, "width", out int width))
                picProps.Width = width;
            if (TryGetInt(metadata, "height", out int height))
                picProps.Height = height;
            if (TryGetFloat(metadata, "scale", out float scale))
                picProps.Scale = scale;
            if (TryGetFloat(metadata, "cfg_rescale", out float cfgRescale))
                picProps.CFG = cfgRescale;

            if (TryGetLong(metadata, "seed", out long seed)) {
                picProps.Seeds = seed;
                picProps.FixedSeeds = Switch.开;
            }

            if (TryGetString(metadata, "sampler", out string samplerText) &&
                TryParseEnumByDescription<SamplerOptions>(samplerText, out SamplerOptions sampler)) {
                picProps.Sampler = sampler;
            }

            if (TryGetString(metadata, "noise_schedule", out string noiseText) &&
                TryParseEnumByDescription<NoiseOptions>(noiseText, out NoiseOptions noise)) {
                picProps.Noise = noise;
            }

            if (TryGetBool(metadata, "dynamic_thresholding", out bool dynamicThresholding))
                picProps.Decrisp = dynamicThresholding ? Switch.开 : Switch.关;

            if (TryGetString(metadata, "qualityPresetId", out string qualityPresetId))
                picProps.QualityToggle = !string.Equals(qualityPresetId, "none", StringComparison.OrdinalIgnoreCase);
            else if (TryGetInt(metadata, "tag_hint_qt", out int qualityHint))
                picProps.QualityToggle = qualityHint != 0;

            bool hasSm = TryGetBool(metadata, "sm", out bool sm);
            bool hasSmDyn = TryGetBool(metadata, "sm_dyn", out bool smDyn);
            if (hasSm || hasSmDyn) {
                picProps.Smea = sm ? Switch.开 : Switch.关;
                picProps.Dyn = smDyn ? Switch.开 : Switch.关;
            }

            if (TryGetDouble(metadata, "skip_cfg_above_sigma", out double skipCfgAboveSigma)) {
                bool deliberateEulerAncestralBug =
                    TryGetBool(metadata, "deliberate_euler_ancestral_bug", out bool bug) && bug;
                bool preferBrownian = TryGetBool(metadata, "prefer_brownian", out bool brownian) && brownian;
                if (!deliberateEulerAncestralBug && preferBrownian) {
                    picProps.Variety = VarietyOptions.自定义_风险参数;
                    picProps.VarietyNum = skipCfgAboveSigma;
                }
                else if (Math.Abs(skipCfgAboveSigma - 19d) < 0.00001d) {
                    picProps.Variety = VarietyOptions.开;
                }
                else {
                    picProps.Variety = VarietyOptions.自定义_风险参数;
                    picProps.VarietyNum = skipCfgAboveSigma;
                }
            }
            else {
                picProps.Variety = VarietyOptions.关;
            }
        }

        private static bool TryGetString(JObject metadata, string key, out string value) {
            value = null;
            if (metadata == null || key == null)
                return false;

            if (!metadata.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
                return false;

            if (token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return false;

            value = token.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetInt(JObject metadata, string key, out int value) {
            value = 0;
            if (!TryGetLong(metadata, key, out long longValue))
                return false;
            if (longValue < int.MinValue || longValue > int.MaxValue)
                return false;

            value = (int)longValue;
            return true;
        }

        private static bool TryGetLong(JObject metadata, string key, out long value) {
            value = 0;
            if (metadata == null || key == null)
                return false;

            if (!metadata.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
                return false;

            return long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetFloat(JObject metadata, string key, out float value) {
            value = 0f;
            if (metadata == null || key == null)
                return false;

            if (!metadata.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
                return false;

            return float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetDouble(JObject metadata, string key, out double value) {
            value = 0d;
            if (metadata == null || key == null)
                return false;

            if (!metadata.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
                return false;

            if (token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return false;

            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetBool(JObject metadata, string key, out bool value) {
            value = false;
            if (metadata == null || key == null)
                return false;

            if (!metadata.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
                return false;

            if (token.Type == JTokenType.Boolean) {
                value = token.Value<bool>();
                return true;
            }

            return bool.TryParse(token.ToString(), out value);
        }

        private static bool TryParseEnumByDescription<TEnum>(string value, out TEnum result) where TEnum : struct {
            result = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            Type enumType = typeof(TEnum);
            if (!enumType.IsEnum)
                return false;

            foreach (TEnum enumValue in Enum.GetValues(enumType)) {
                FieldInfo field = enumType.GetField(enumValue.ToString());
                string description = field?.GetCustomAttribute<DescriptionAttribute>()?.Description;
                if (string.Equals(description, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(enumValue.ToString(), value, StringComparison.OrdinalIgnoreCase)) {
                    result = enumValue;
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}