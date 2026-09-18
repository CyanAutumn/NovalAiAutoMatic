using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AutoNai3Tools.body;
using AutoNai3Tools.utils;

namespace AutoNai3Tools {
    public partial class Form1 {
        #region Config

        private static string NormalizeApiValue(string api) {
            if (string.IsNullOrWhiteSpace(api))
                return null;

            string normalized = ApiEndpoint.ResolveBaseUrl(api);
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private PresetConfigData CapturePresetConfig() {
            var resolutionList = (picProps.ResolutionList ?? string.Empty)
                .Split(new[] { "\r\n" }, StringSplitOptions.None)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

            var vibes = new System.Collections.Generic.List<VibeConfigData>();
            foreach (DataGridViewRow row in dgvVibe.Rows) {
                if (row.IsNewRow) continue;
                float ie = 0f;
                float rs = 0f;
                if (row.Cells["Column2"].Value != null)
                    float.TryParse(row.Cells["Column2"].Value.ToString(), out ie);
                if (row.Cells["Column3"].Value != null)
                    float.TryParse(row.Cells["Column3"].Value.ToString(), out rs);

                vibes.Add(new VibeConfigData {
                    Enabled = row.Cells["ColumnEnabled"].Value?.ToString() == "启用",
                    Name = row.Cells["ColumnName"].Value?.ToString(),
                    IE = ie,
                    RS = rs,
                    Path = row.Cells["Column1"].Value?.ToString()
                });
            }

            return new PresetConfigData {
                Prompt = txtPrompt.Text,
                NegativePrompt = txtNegativePrompt.Text,
                PromptBlackList = picProps.PromptBlackList,
                PromptBlackListEnabled = picProps.EnablePromptBlackList,
                PromptBlackListRegex = picProps.PromptBlackListRegex,
                PromptBlackListRegexEnabled = picProps.EnablePromptBlackListRegex,
                GenerateMaxNum = picProps.RunNum,
                KeepParams = picProps.RunKeepParams,
                SavePromptToTxt = picProps.SavePromptToTxt,
                SavePromptToTxtNoArtist = picProps.SavePromptToTxtNoArtist,
                ResolutionMode = picProps.ResolutionMode,
                RandomPromptFolderPath = picProps.RandomPromptFolderPath,
                WildcardFolderPath = picProps.WildcardFolderPath,
                OutputPath = picProps.OutputPath,
                Api = settingProps.Api,
                Token = settingProps.Token,
                SamplerIndex = (int)picProps.Sampler,
                Steps = picProps.Steps,
                Scale = picProps.Scale,
                CFG = picProps.CFG,
                Noise = (int)picProps.Noise,
                Smea = picProps.Smea == Switch.开,
                Dyn = picProps.Dyn == Switch.开,
                NormalizeReferenceStrengthValues = picProps.NormalizeReferenceStrengthValues,
                ImageFormat = (int)picProps.ImageFormat,
                QualityToggle = picProps.QualityToggle,
                ResolutionList = resolutionList.Length > 0 ? resolutionList : new[] { "832x1216" },
                ArtistFixed = txtArtistFixed.Text,
                ArtistRandom = txtArtistRandom.Text,
                DefaultArtistWeightReduceMax = (int)numDefaultArtistWeightReduceMax.Value,
                DefaultArtistWeightIncreaseMax = (int)numDefaultArtistWeightIncreaseMax.Value,
                DefaultArtistWeightReduceDoubleColonMax = (double)numDefaultArtistWeightReduceDoubleColonMax.Value,
                DefaultArtistWeightIncreaseDoubleColonMax = (double)numDefaultArtistWeightIncreaseDoubleColonMax.Value,
                ArtistMin = (int)numArtistMin.Value,
                ArtistMax = (int)numArtistMax.Value,
                ArtistModify = chkArtistModify.Checked,
                Proxy = settingProps.Proxy,
                KeepRandomArtist = settingProps.KeepRandomArtist,
                KeepWildcard = settingProps.KeepWildcard,
                KeepRandomPrompt = settingProps.KeepRandomPrompt,
                KeepResolution = settingProps.KeepResolution,
                Decrisp = picProps.Decrisp == Switch.开,
                FixedSeeds = picProps.FixedSeeds,
                Seeds = picProps.Seeds,
                Width = picProps.Width,
                Height = picProps.Height,
                Variety = picProps.Variety != VarietyOptions.关,
                VarietyDefault = picProps.Variety == VarietyOptions.自定义_风险参数,
                VarietyNum = picProps.VarietyNum,
                ModelSelect = picProps.Model,
                OutputFileNameFormat = settingProps.OutputFileNameFormat,
                Vibes = vibes
            };
        }

        private void ApplyPresetConfig(PresetConfigData data) {
            if (data == null)
                return;

            txtPrompt.Text = data.Prompt ?? string.Empty;
            txtNegativePrompt.Text = data.NegativePrompt ?? string.Empty;
            if (!string.IsNullOrEmpty(data.PromptBlackList))
                picProps.PromptBlackList = data.PromptBlackList;
            picProps.EnablePromptBlackList = data.PromptBlackListEnabled ?? true;
            if (!string.IsNullOrEmpty(data.PromptBlackListRegex))
                picProps.PromptBlackListRegex = data.PromptBlackListRegex;
            picProps.EnablePromptBlackListRegex = data.PromptBlackListRegexEnabled ?? true;
            picProps.RunNum = data.GenerateMaxNum;
            picProps.RunKeepParams = data.KeepParams;
            picProps.SavePromptToTxt = data.SavePromptToTxt;
            picProps.SavePromptToTxtNoArtist = data.SavePromptToTxtNoArtist;
            picProps.ResolutionMode = data.ResolutionMode;
            if (!string.IsNullOrEmpty(data.RandomPromptFolderPath))
                picProps.RandomPromptFolderPath = data.RandomPromptFolderPath;
            if (!string.IsNullOrEmpty(data.WildcardFolderPath))
                picProps.WildcardFolderPath = data.WildcardFolderPath;
            if (!string.IsNullOrEmpty(data.OutputPath))
                picProps.OutputPath = data.OutputPath;
            var normalizedApi = NormalizeApiValue(data.Api);
            if (!string.IsNullOrEmpty(normalizedApi))
                settingProps.Api = normalizedApi;
            picProps.Sampler = (SamplerOptions)data.SamplerIndex;
            picProps.Steps = data.Steps;
            picProps.Scale = data.Scale;
            picProps.CFG = data.CFG;
            picProps.Noise = (NoiseOptions)data.Noise;
            picProps.Smea = data.Smea ? Switch.开 : Switch.关;
            picProps.Dyn = data.Dyn ? Switch.开 : Switch.关;
            if (data.NormalizeReferenceStrengthValues.HasValue)
                picProps.NormalizeReferenceStrengthValues = data.NormalizeReferenceStrengthValues.Value;
            else
                picProps.NormalizeReferenceStrengthValues = false;
            if (data.ImageFormat.HasValue)
                picProps.ImageFormat = (ImageFormatOptions)data.ImageFormat.Value;
            if (data.QualityToggle.HasValue)
                picProps.QualityToggle = data.QualityToggle.Value;
            if (data.ResolutionList != null)
                picProps.ResolutionList = string.Join("\r\n", data.ResolutionList);

            txtArtistFixed.Text = data.ArtistFixed ?? string.Empty;
            txtArtistRandom.Text = data.ArtistRandom ?? string.Empty;
            numDefaultArtistWeightReduceMax.Value = data.DefaultArtistWeightReduceMax;
            numDefaultArtistWeightIncreaseMax.Value = data.DefaultArtistWeightIncreaseMax;
            if (data.DefaultArtistWeightReduceDoubleColonMax.HasValue)
                numDefaultArtistWeightReduceDoubleColonMax.Value = (decimal)data.DefaultArtistWeightReduceDoubleColonMax.Value;
            if (data.DefaultArtistWeightIncreaseDoubleColonMax.HasValue)
                numDefaultArtistWeightIncreaseDoubleColonMax.Value = (decimal)data.DefaultArtistWeightIncreaseDoubleColonMax.Value;
            numArtistMin.Value = data.ArtistMin;
            numArtistMax.Value = data.ArtistMax;
            chkArtistModify.Checked = data.ArtistModify;
            settingProps.Proxy = data.Proxy;
            settingProps.KeepRandomArtist = data.KeepRandomArtist;
            settingProps.KeepWildcard = data.KeepWildcard;
            settingProps.KeepRandomPrompt = data.KeepRandomPrompt;
            settingProps.KeepResolution = data.KeepResolution;
            picProps.Decrisp = data.Decrisp ? Switch.开 : Switch.关;
            picProps.FixedSeeds = data.FixedSeeds;
            picProps.Seeds = data.Seeds;
            picProps.Width = data.Width;
            picProps.Height = data.Height;
            if (data.Variety) {
                picProps.Variety = data.VarietyDefault ? VarietyOptions.自定义_风险参数 : VarietyOptions.开;
            }
            else {
                picProps.Variety = VarietyOptions.关;
            }
            picProps.VarietyNum = data.VarietyNum;
            picProps.Model = data.ModelSelect;
            if (BodyTools.IsRetired(picProps.Model)) {
                Logger.Warn("该模型已被 NovelAI 官方下线，已自动切换为 NAI3",
                    context: Logger.Context(("model", data.ModelSelect)));
                picProps.Model = BodyTools.Model.Nai3;
            }
            settingProps.OutputFileNameFormat = data.OutputFileNameFormat;

            dgvVibe.Rows.Clear();
            if (data.Vibes != null) {
                foreach (var vibe in data.Vibes) {
                    dgvVibe.Rows.Add(
                        vibe.Enabled ? "启用" : "禁用",
                        vibe.Name,
                        (decimal)vibe.IE,
                        (decimal)vibe.RS,
                        vibe.Path
                    );
                }
            }
        }

        private SystemConfigData CaptureSystemConfig() {
            return new SystemConfigData {
                Api = settingProps.Api,
                Token = settingProps.Token,
                PromptBlackList = picProps.PromptBlackList,
                PromptBlackListEnabled = picProps.EnablePromptBlackList,
                PromptBlackListRegex = picProps.PromptBlackListRegex,
                PromptBlackListRegexEnabled = picProps.EnablePromptBlackListRegex,
                SleepTimeShortLow = settingProps.SleepTimeShortLow,
                SleepTimeShortHigh = settingProps.SleepTimeShortHigh,
                SleepTimeLongLow = settingProps.SleepTimeLongLow,
                SleepTimeLongHigh = settingProps.SleepTimeLongHigh,
                UiLanguage = settingProps.UiLanguage.ToCultureName(),
                AnlasTracking = settingProps.AnlasTracking
            };
        }

        private bool ApplySystemConfig(SystemConfigData data) {
            if (data == null)
                return false;

            bool apiNormalized = false;
            var normalizedApi = NormalizeApiValue(data.Api);
            if (!string.IsNullOrWhiteSpace(normalizedApi)) {
                settingProps.Api = normalizedApi;
                apiNormalized = !string.Equals(data.Api?.Trim(), normalizedApi, StringComparison.Ordinal);
            }
            settingProps.Token = data.Token ?? settingProps.Token;
            if (!string.IsNullOrEmpty(data.PromptBlackList))
                picProps.PromptBlackList = data.PromptBlackList;
            picProps.EnablePromptBlackList = data.PromptBlackListEnabled ?? picProps.EnablePromptBlackList;
            if (!string.IsNullOrEmpty(data.PromptBlackListRegex))
                picProps.PromptBlackListRegex = data.PromptBlackListRegex;
            picProps.EnablePromptBlackListRegex = data.PromptBlackListRegexEnabled ?? picProps.EnablePromptBlackListRegex;
            if (data.SleepTimeShortLow.HasValue)
                settingProps.SleepTimeShortLow = data.SleepTimeShortLow.Value;
            if (data.SleepTimeShortHigh.HasValue)
                settingProps.SleepTimeShortHigh = data.SleepTimeShortHigh.Value;
            if (data.SleepTimeLongLow.HasValue)
                settingProps.SleepTimeLongLow = data.SleepTimeLongLow.Value;
            if (data.SleepTimeLongHigh.HasValue)
                settingProps.SleepTimeLongHigh = data.SleepTimeLongHigh.Value;
            if (!string.IsNullOrWhiteSpace(data.UiLanguage))
                settingProps.UiLanguage = UiLanguageExtensions.FromCultureName(data.UiLanguage);
            settingProps.AnlasTracking = data.AnlasTracking ?? settingProps.AnlasTracking;

            return apiNormalized;
        }

        private void RefreshConfig() {
            try {
                var names = configService.GetPresetNames();
                cmbConfigName.Items.Clear();
                foreach (var name in names)
                    cmbConfigName.Items.Add(name);
            }
            catch (Exception ex) {
                Logger.Error("刷新配置列表失败", exception: ex);
            }
        }

        private void btnAddOrEditConfig_Click(object sender, EventArgs e) {
            string name = cmbConfigName.Text?.Trim();
            if (string.IsNullOrEmpty(name)) {
                MessageBox.Show(Properties.Resources.Msg_ConfigNameEmpty, Properties.Resources.Title_Info,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try {
                configService.SavePreset(name, CapturePresetConfig());
                RefreshConfig();
                Logger.Info("配置已保存", context: Logger.Context(("config", name)));
            }
            catch (Exception ex) {
                Logger.Error("保存配置失败", exception: ex,
                    context: Logger.Context(("config", name)));
                MessageBox.Show(Properties.Resources.Msg_SaveConfigFailed, Properties.Resources.Title_Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnOpenConfigFolder_Click(object sender, EventArgs e) {
            try {
                System.Diagnostics.Process.Start(configService.PresetFolderPath);
            }
            catch (Exception ex) {
                Logger.Warn("无法打开配置目录",
                    context: Logger.Context(("folder", configService.PresetFolderPath), ("reason", ex.Message)));
            }
        }

        private void btnDeleteConfig_Click(object sender, EventArgs e) {
            string name = cmbConfigName.Text?.Trim();
            if (string.IsNullOrEmpty(name)) {
                MessageBox.Show(Properties.Resources.Msg_SelectConfigToDelete, Properties.Resources.Title_Info,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try {
                configService.DeletePreset(name);
                RefreshConfig();
            }
            catch (Exception ex) {
                Logger.Error("删除配置失败", exception: ex,
                    context: Logger.Context(("config", name)));
                MessageBox.Show(Properties.Resources.Msg_DeleteConfigFailed, Properties.Resources.Title_Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cmbConfigName_SelectedIndexChanged(object sender, EventArgs e) {
            string name = cmbConfigName.Text?.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            try {
                var data = configService.LoadPreset(name);
                ApplyPresetConfig(data);
                InitTagSnippetDGV();
                propertyGrid1.Refresh();
                propertyGridSettings.Refresh();
            }
            catch (Exception ex) {
                Logger.Error("读取配置失败", exception: ex,
                    context: Logger.Context(("config", name)));
                MessageBox.Show(Properties.Resources.Msg_ReadConfigFailed, Properties.Resources.Title_Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e) {
            try {
                configService.SaveAutoPreset(CapturePresetConfig());
            }
            catch (Exception ex) {
                Logger.Warn("自动保存图像配置失败",
                    context: Logger.Context(("config", "autoSave"), ("reason", ex.Message)));
            }

            try {
                configService.SaveSystemConfig(CaptureSystemConfig());
            }
            catch (Exception ex) {
                Logger.Warn("保存系统配置失败",
                    context: Logger.Context(("config", "system"), ("reason", ex.Message)));
            }
        }

        private void Form1_Load(object sender, EventArgs e) {
            try {
                var data = configService.LoadAutoPreset();
                ApplyPresetConfig(data);
                cmbConfigName.Text = configService.AutoSavePresetName;
            }
            catch (Exception ex) {
                Logger.Warn("未找到上一次关闭的自动保存，使用默认配置",
                    context: Logger.Context(("config", "autoSave"), ("reason", ex.Message)));
            }

            try {
                var systemConfig = configService.LoadSystemConfig();
                bool apiNormalized = ApplySystemConfig(systemConfig);
                if (apiNormalized) {
                    try {
                        configService.SaveSystemConfig(CaptureSystemConfig());
                    }
                    catch (Exception ex) {
                        Logger.Warn("系统配置 API 规范化保存失败",
                            context: Logger.Context(("config", "system"), ("reason", ex.Message)));
                    }
                }
            }
            catch (Exception ex) {
                Logger.Warn("未找到系统配置文件，使用默认配置",
                    context: Logger.Context(("config", "system"), ("reason", ex.Message)));
            }

            RefreshConfig();
            InitTagSnippetDGV();
            propertyGrid1.Refresh();
            propertyGridSettings.Refresh();

            BeginStartupAnlasRefresh();
        }

        private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e) {
            if (e.ChangedItem?.PropertyDescriptor?.Name == nameof(picProps.WildcardFolderPath)) {
                InitTagSnippetDGV();
            }

            RefreshAnlasButtonText();
        }

        private void cmbConfigName_MouseClick(object sender, MouseEventArgs e) {
            RefreshConfig();
        }

        private void btnClearAllLog_Click(object sender, EventArgs e) {
            txtLog.Text = string.Empty;
        }

        #endregion
    }
}
