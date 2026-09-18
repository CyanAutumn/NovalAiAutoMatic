using System;
using System.Collections.Generic;
using System.IO;
using AutoNai3Tools.utils;
using Newtonsoft.Json.Linq;

namespace AutoNai3Tools {
    public partial class Form1 {
        #region Vibe

        /// <summary>读取某个参考图可用的「信息抽取」档位，供前端做下拉。</summary>
        internal JArray GetVibeInformationOptions(string path) {
            var options = new JArray();
            if (string.IsNullOrWhiteSpace(path))
                return options;

            foreach (var value in Vibe.GetVibeInformationExtractedOptions(path, picProps.Model))
                options.Add(value);

            return options;
        }

        internal void AddVibe(string path, float informationExtracted, float referenceStrength) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                PushToast("warn", "参考图不存在");
                return;
            }

            if (vibeItems.Count >= 16) {
                PushToast("warn", "最多只能添加 16 个参考图");
                return;
            }

            vibeItems.Add(new VibeConfigData {
                Enabled = true,
                Name = Path.GetFileName(path),
                IE = informationExtracted,
                RS = referenceStrength,
                Path = path
            });
            VibeSelectedIndex = vibeItems.Count - 1;
            PushVibes();
            PushToast("ok", "参考图已添加");
        }

        internal void ImportVibeBundle(string path) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                PushToast("warn", "Bundle 文件不存在");
                return;
            }

            try {
                var imported = Vibe.ImportVibeBundle(path, picProps.Model);
                if (imported.Count == 0) {
                    PushToast("warn", "Bundle 里没有可导入的 Vibe");
                    return;
                }

                foreach (var vibe in imported) {
                    if (vibeItems.Count >= 16)
                        break;
                    vibeItems.Add(vibe);
                }

                PushVibes();
                PushToast("ok", $"成功导入 {imported.Count} 个 Vibe");
            }
            catch (Exception ex) {
                Logger.Error("读取或解析 Vibe Bundle 失败", exception: ex,
                    context: Logger.Context(("path", path)));
                PushToast("err", "导入失败：" + ex.Message);
            }
        }

        internal void UpdateVibe(int index, float informationExtracted, float referenceStrength, bool enabled,
            string name) {
            if (index < 0 || index >= vibeItems.Count)
                return;

            var vibe = vibeItems[index];
            vibe.Enabled = enabled;
            vibe.IE = informationExtracted;
            vibe.RS = referenceStrength;
            if (!string.IsNullOrWhiteSpace(name))
                vibe.Name = name;

            PushVibes();
        }

        internal void DeleteVibe(int index) {
            if (index < 0 || index >= vibeItems.Count) {
                Logger.Warn("未选择要删除的参考图", context: Logger.Context(("action", "VibeDelete")));
                return;
            }

            vibeItems.RemoveAt(index);
            if (VibeSelectedIndex >= vibeItems.Count)
                VibeSelectedIndex = vibeItems.Count - 1;
            PushVibes();
        }

        internal void SelectVibe(int index) {
            VibeSelectedIndex = index >= 0 && index < vibeItems.Count ? index : -1;
            PushVibes();
        }

        #endregion
    }
}