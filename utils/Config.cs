using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoNai3Tools.body;
using Nett;

namespace AutoNai3Tools.utils {
    internal class SystemConfigData {
        public string Api { get; set; }
        public string Token { get; set; }
        public List<SnippetItem> SnippetItems { get; set; }
        public string PromptBlackList { get; set; }
        public bool? PromptBlackListEnabled { get; set; }
        public string PromptBlackListRegex { get; set; }
        public bool? PromptBlackListRegexEnabled { get; set; }
        public int? SleepTimeShortLow { get; set; }
        public int? SleepTimeShortHigh { get; set; }
        public int? SleepTimeLongLow { get; set; }
        public int? SleepTimeLongHigh { get; set; }
        public string UiLanguage { get; set; }
        public bool? AnlasTracking { get; set; }
    }

    internal class SystemConfigRepository {
        private readonly string folderPath;
        private readonly string fileName;

        public SystemConfigRepository(string folderPath = "C:\\Users\\Public\\Documents\\auto_nai3_system\\",
            string fileName = "config.toml") {
            this.folderPath = folderPath;
            this.fileName = fileName;
        }

        private string ConfigFilePath => Path.Combine(folderPath, fileName);

        public void Save(SystemConfigData data) {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            EnsureDirectory();
            Toml.WriteFile(data, ConfigFilePath);
        }

        public SystemConfigData Load() {
            if (!File.Exists(ConfigFilePath))
                return null;

            return Toml.ReadFile<SystemConfigData>(ConfigFilePath);
        }

        private void EnsureDirectory() {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }
    }

    internal class PresetConfigData {
        public string Prompt { get; set; }
        public string NegativePrompt { get; set; }
        public string PromptBlackList { get; set; }
        public bool? PromptBlackListEnabled { get; set; }
        public string PromptBlackListRegex { get; set; }
        public bool? PromptBlackListRegexEnabled { get; set; }
        public int GenerateMaxNum { get; set; }
        public int KeepParams { get; set; }
        public bool SavePromptToTxt { get; set; }
        public bool SavePromptToTxtNoArtist { get; set; }
        public ResolutionMode ResolutionMode { get; set; }
        public string RandomPromptFolderPath { get; set; }
        public string WildcardFolderPath { get; set; }
        public string OutputPath { get; set; }
        public string Api { get; set; }
        public string Token { get; set; }
        public int SamplerIndex { get; set; }
        public int Steps { get; set; }
        public float Scale { get; set; }
        public float CFG { get; set; }
        public int Noise { get; set; }
        public bool Smea { get; set; }
        public bool Dyn { get; set; }
        public bool? NormalizeReferenceStrengthValues { get; set; }
        public int? ImageFormat { get; set; }
        public bool? QualityToggle { get; set; }
        public string[] ResolutionList { get; set; }
        public string ArtistFixed { get; set; }
        public string ArtistRandom { get; set; }
        public int DefaultArtistWeightReduceMax { get; set; }
        public int DefaultArtistWeightIncreaseMax { get; set; }
        public double? DefaultArtistWeightReduceDoubleColonMax { get; set; }
        public double? DefaultArtistWeightIncreaseDoubleColonMax { get; set; }
        public int ArtistMin { get; set; }
        public int ArtistMax { get; set; }
        public bool ArtistModify { get; set; }
        public string Proxy { get; set; }
        public bool KeepRandomArtist { get; set; }
        public bool KeepWildcard { get; set; }
        public bool KeepRandomPrompt { get; set; }
        public bool KeepResolution { get; set; }
        public bool Decrisp { get; set; }
        public long Seeds { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Variety { get; set; }
        public bool VarietyDefault { get; set; }
        public double VarietyNum { get; set; }
        public Switch FixedSeeds { get; set; }
        public BodyTools.Model ModelSelect { get; set; }
        public OutputFileNameFormat OutputFileNameFormat { get; set; }
        public List<VibeConfigData> Vibes { get; set; }
    }

    internal class VibeConfigData {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public float IE { get; set; }
        public float RS { get; set; }
        public string Path { get; set; }
    }

    internal class PresetConfigRepository {
        private readonly string folderPath;

        public PresetConfigRepository(string folderPath = "C:\\Users\\Public\\Documents\\auto_nai3_2\\") {
            this.folderPath = folderPath;
        }

        public string FolderPath => folderPath;

        public void Save(string name, PresetConfigData data) {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("配置名称不能为空", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            EnsureDirectory();
            Toml.WriteFile(data, BuildFilePath(name));
        }

        public PresetConfigData Load(string name) {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("配置名称不能为空", nameof(name));

            string path = BuildFilePath(name);
            if (!File.Exists(path))
                throw new FileNotFoundException("未找到配置文件", path);

            return Toml.ReadFile<PresetConfigData>(path);
        }

        public IReadOnlyList<string> ListPresetNames() {
            EnsureDirectory();
            return Directory.GetFiles(folderPath, "*.toml")
                .Select(file => Path.GetFileNameWithoutExtension(file))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void Delete(string name) {
            if (string.IsNullOrWhiteSpace(name))
                return;

            string path = BuildFilePath(name);
            if (File.Exists(path))
                File.Delete(path);
        }

        private string BuildFilePath(string name) => Path.Combine(folderPath, name + ".toml");

        private void EnsureDirectory() {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }
    }

    internal class SnippetItem {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
