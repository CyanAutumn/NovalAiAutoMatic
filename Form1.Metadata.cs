using AutoNai3Tools.utils;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace AutoNai3Tools {
    public partial class Form1 {
        private void InitializeMetadataDragDrop() {
            ConfigureMetadataDropTarget(tabPage2);
            ConfigureMetadataDropTarget(panel6);
            ConfigureMetadataDropTarget(propertyGrid1);
        }

        private void ConfigureMetadataDropTarget(Control control) {
            if (control == null)
                return;

            control.AllowDrop = true;
            control.DragEnter += MetadataDropTarget_DragEnter;
            control.DragDrop += MetadataDropTarget_DragDrop;
        }

        private void MetadataDropTarget_DragEnter(object sender, DragEventArgs e) {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) {
                e.Effect = DragDropEffects.Copy;
                return;
            }

            e.Effect = DragDropEffects.None;
        }

        private void MetadataDropTarget_DragDrop(object sender, DragEventArgs e) {
            if (!(e.Data?.GetData(DataFormats.FileDrop) is string[] files) || files.Length == 0)
                return;

            string filePath = files[0];
            ImportGenerationMetadataFromImage(filePath);
        }

        private void ImportGenerationMetadataFromImage(string filePath) {
            if (!ImageSourceMetadataReader.TryReadGenerationMetadata(filePath, out JObject metadata, out string sourceLocation,
                    out string errorMessage)) {
                Logger.Warn("读取源数据失败",
                    context: Logger.Context(("file", filePath), ("reason", errorMessage ?? "unknown")));
                MessageBox.Show(errorMessage ?? "无法读取图片源数据。", Properties.Resources.Title_Prompt,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ApplyMetadataToUi(metadata);
            propertyGrid1.Refresh();
            Logger.PicInfo($"源数据位置: {sourceLocation}");
            Logger.Info("已按图片源数据更新生成参数",
                context: Logger.Context(("file", filePath), ("source", sourceLocation)));
        }

        private void ApplyMetadataToUi(JObject metadata) {
            if (metadata == null)
                return;

            if (TryGetString(metadata, "prompt", out string prompt))
                txtPrompt.Text = prompt;
            if (TryGetString(metadata, "uc", out string uc))
                txtNegativePrompt.Text = uc;

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
                bool deliberateEulerAncestralBug = TryGetBool(metadata, "deliberate_euler_ancestral_bug", out bool bug) && bug;
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
    }
}
