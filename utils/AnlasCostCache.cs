using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Nett;

namespace AutoNai3Tools.utils {
    internal sealed class AnlasCostCacheEntry {
        public string Key { get; set; }
        public int Cost { get; set; }
        public string RecordedAt { get; set; }
    }

    internal sealed class AnlasCostCacheData {
        public List<AnlasCostCacheEntry> Entries { get; set; }
    }

    /// <summary>
    /// 「参数组合 -> 实测 Anlas 消耗」的本地缓存，默认 8 小时有效。
    /// Prompt 不影响消耗，所以键里只放模型、尺寸、Steps、张数与动作类型。
    /// </summary>
    internal static class AnlasCostCache {
        public static readonly TimeSpan Ttl = TimeSpan.FromHours(8);

        private const string FolderPath = @"C:\Users\Public\Documents\auto_nai3_system";
        private const string FileName = "anlas_cost_cache.toml";

        private static readonly object SyncRoot = new object();
        private static Dictionary<string, AnlasCostCacheEntry> entries;
        private static bool dirty;

        private static string FilePath => Path.Combine(FolderPath, FileName);

        public static string BuildKey(string model, int width, int height, int steps, int nSamples, string action,
            float? strength) {
            string key = string.Format(CultureInfo.InvariantCulture, "{0}|{1}x{2}|s{3}|n{4}|{5}",
                string.IsNullOrWhiteSpace(model) ? "?" : model,
                width, height, steps, Math.Max(1, nSamples),
                string.IsNullOrWhiteSpace(action) ? "generate" : action);

            if (strength.HasValue && strength.Value > 0)
                key += "|" + strength.Value.ToString("0.###", CultureInfo.InvariantCulture);

            return key;
        }

        /// <summary>命中且未过期时返回 true，并把实测消耗写入 cost。</summary>
        public static bool TryGet(string key, out int cost) {
            cost = 0;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            lock (SyncRoot) {
                EnsureLoaded();

                AnlasCostCacheEntry entry;
                if (!entries.TryGetValue(key, out entry) || entry == null)
                    return false;

                DateTime recordedAt;
                if (!TryParseTimestamp(entry.RecordedAt, out recordedAt) ||
                    DateTime.UtcNow - recordedAt > Ttl) {
                    entries.Remove(key);
                    dirty = true;
                    Save();
                    return false;
                }

                cost = entry.Cost;
                return true;
            }
        }

        /// <summary>记录一次实测消耗（覆盖旧值）。</summary>
        public static void Record(string key, int cost) {
            if (string.IsNullOrWhiteSpace(key) || cost < 0)
                return;

            lock (SyncRoot) {
                EnsureLoaded();
                entries[key] = new AnlasCostCacheEntry {
                    Key = key,
                    Cost = cost,
                    RecordedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                };
                dirty = true;
                Save();
            }
        }

        public static void Clear() {
            lock (SyncRoot) {
                entries = new Dictionary<string, AnlasCostCacheEntry>(StringComparer.Ordinal);
                dirty = true;
                Save();
            }
        }

        private static void EnsureLoaded() {
            if (entries != null)
                return;

            entries = new Dictionary<string, AnlasCostCacheEntry>(StringComparer.Ordinal);
            try {
                if (!File.Exists(FilePath))
                    return;

                var data = Toml.ReadFile<AnlasCostCacheData>(FilePath);
                if (data?.Entries == null)
                    return;

                foreach (var entry in data.Entries) {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                        continue;
                    entries[entry.Key] = entry;
                }
            }
            catch (Exception ex) {
                Logger.Warn($"读取 Anlas 消耗缓存失败，将重新统计（{ex.Message}）",
                    context: Logger.Context(("path", FilePath)));
                entries.Clear();
            }
        }

        private static void Save() {
            if (!dirty)
                return;

            try {
                Directory.CreateDirectory(FolderPath);
                var data = new AnlasCostCacheData {
                    Entries = new List<AnlasCostCacheEntry>(entries.Values)
                };
                Toml.WriteFile(data, FilePath);
                dirty = false;
            }
            catch (Exception ex) {
                Logger.Warn($"保存 Anlas 消耗缓存失败（{ex.Message}）",
                    context: Logger.Context(("path", FilePath)));
            }
        }

        private static bool TryParseTimestamp(string value, out DateTime timestampUtc) {
            timestampUtc = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            DateTime parsed;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                return false;

            timestampUtc = parsed.ToUniversalTime();
            return true;
        }
    }
}
