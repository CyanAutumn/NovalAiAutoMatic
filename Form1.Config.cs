using System;
using System.Collections.Generic;
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

        internal PresetConfigData CapturePresetConfig() {
            var resolutionList = (picProps.ResolutionList ?? string.Empty)
                .Split(new[] { "\r\n" }, StringSplitOptions.None)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

            var vibes = new List<VibeConfigData>();
            foreach (var vibe in vibeItems) {
                if (vibe == null)
                    continue;

                vibes.Add(new VibeConfigData {
                    Enabled = vibe.Enabled,
                    Name = vibe.Name,
                    IE = vibe.IE,
                    RS = vibe.RS,
                    Path = vibe.Path
                });
            }

            return new PresetConfigData {
                Prompt = PromptText,
                NegativePrompt = NegativePromptText,
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
                ArtistFixed = ArtistFixedText,
                ArtistRandom = ArtistRandomText,
                DefaultArtistWeightReduceMax = DefaultArtistWeightReduceMax,
                DefaultArtistWeightIncreaseMax = DefaultArtistWeightIncreaseMax,
                DefaultArtistWeightReduceDoubleColonMax = DefaultArtistWeightReduceDoubleColonMax,
                DefaultArtistWeightIncreaseDoubleColonMax = DefaultArtistWeightIncreaseDoubleColonMax,
                ArtistMin = ArtistMin,
                ArtistMax = ArtistMax,
                ArtistModify = ArtistModify,
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

        internal void ApplyPresetConfig(PresetConfigData data) {
            if (data == null)
                return;

            PromptText = data.Prompt ?? string.Empty;
            NegativePromptText = data.NegativePrompt ?? string.Empty;
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
            picProps.NormalizeReferenceStrengthValues =
                data.NormalizeReferenceStrengthValues ?? false;
            if (data.ImageFormat.HasValue)
                picProps.ImageFormat = (ImageFormatOptions)data.ImageFormat.Value;
            if (data.QualityToggle.HasValue)
                picProps.QualityToggle = data.QualityToggle.Value;
            if (data.ResolutionList != null)
                picProps.ResolutionList = string.Join("\r\n", data.ResolutionList);

            ArtistFixedText = data.ArtistFixed ?? string.Empty;
            ArtistRandomText = data.ArtistRandom ?? string.Empty;
            DefaultArtistWeightReduceMax = data.DefaultArtistWeightReduceMax;
            DefaultArtistWeightIncreaseMax = data.DefaultArtistWeightIncreaseMax;
            if (data.DefaultArtistWeightReduceDoubleColonMax.HasValue)
                DefaultArtistWeightReduceDoubleColonMax = data.DefaultArtistWeightReduceDoubleColonMax.Value;
            if (data.DefaultArtistWeightIncreaseDoubleColonMax.HasValue)
                DefaultArtistWeightIncreaseDoubleColonMax = data.DefaultArtistWeightIncreaseDoubleColonMax.Value;
            ArtistMin = data.ArtistMin;
            ArtistMax = data.ArtistMax;
            ArtistModify = data.ArtistModify;
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

            vibeItems.Clear();
            if (data.Vibes != null) {
                foreach (var vibe in data.Vibes) {
                    vibeItems.Add(new VibeConfigData {
                        Enabled = vibe.Enabled,
                        Name = vibe.Name,
                        IE = vibe.IE,
                        RS = vibe.RS,
                        Path = vibe.Path
                    });
                }
            }

            VibeSelectedIndex = -1;
        }

        internal SystemConfigData CaptureSystemConfig() {
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

        internal bool ApplySystemConfig(SystemConfigData data) {
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
            picProps.EnablePromptBlackListRegex =
                data.PromptBlackListRegexEnabled ?? picProps.EnablePromptBlackListRegex;
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

        internal void RefreshConfigs() {
            try {
                configService.GetPresetNames();
            }
            catch (Exception ex) {
                Logger.Error("刷新配置列表失败", exception: ex);
            }

            PushConfigs();
        }

        internal void LoadConfigs() {
            LoadUiTheme();

            try {
                var data = configService.LoadAutoPreset();
                ApplyPresetConfig(data);
                CurrentConfigName = configService.AutoSavePresetName;
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

            ReloadWildcards();
        }

        internal void SaveConfigs() {
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

        internal void SavePreset(string name) {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) {
                PushToast("warn", Properties.Resources.Msg_ConfigNameEmpty);
                return;
            }

            try {
                configService.SavePreset(name, CapturePresetConfig());
                CurrentConfigName = name;
                PushConfigs();
                Logger.Info("配置已保存", context: Logger.Context(("config", name)));
                PushToast("ok", $"配置「{name}」已保存");
            }
            catch (Exception ex) {
                Logger.Error("保存配置失败", exception: ex,
                    context: Logger.Context(("config", name)));
                PushToast("err", Properties.Resources.Msg_SaveConfigFailed);
            }
        }

        internal void LoadPreset(string name) {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            try {
                var data = configService.LoadPreset(name);
                ApplyPresetConfig(data);
                CurrentConfigName = name;
                ReloadWildcards();
                PushState();
                Logger.Info("配置已载入", context: Logger.Context(("config", name)));
            }
            catch (Exception ex) {
                Logger.Error("读取配置失败", exception: ex,
                    context: Logger.Context(("config", name)));
                PushToast("err", Properties.Resources.Msg_ReadConfigFailed);
            }
        }

        internal void DeletePreset(string name) {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) {
                PushToast("warn", Properties.Resources.Msg_SelectConfigToDelete);
                return;
            }

            try {
                configService.DeletePreset(name);
                if (string.Equals(CurrentConfigName, name, StringComparison.Ordinal))
                    CurrentConfigName = string.Empty;
                PushConfigs();
                PushToast("ok", $"配置「{name}」已删除");
            }
            catch (Exception ex) {
                Logger.Error("删除配置失败", exception: ex,
                    context: Logger.Context(("config", name)));
                PushToast("err", Properties.Resources.Msg_DeleteConfigFailed);
            }
        }

        internal void OpenPresetFolder() {
            try {
                System.Diagnostics.Process.Start(configService.PresetFolderPath);
            }
            catch (Exception ex) {
                Logger.Warn("无法打开配置目录",
                    context: Logger.Context(("path", configService.PresetFolderPath),
                        ("reason", ex.Message)));
            }
        }

        #endregion
    }
}