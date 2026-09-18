using System;
using System.Collections.Generic;
using System.IO;
using AutoNai3Tools.Services;
using AutoNai3Tools.utils;

namespace AutoNai3Tools {
    public partial class Form1 {
        #region Wildcard

        internal void ReloadWildcards() {
            wildcardItems.Clear();
            string folderPath = picProps.WildcardFolderPath;
            try {
                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath)) {
                    foreach (var snippet in wildcardService.LoadSnippets(folderPath))
                        wildcardItems.Add(snippet);
                }
            }
            catch (Exception ex) {
                Logger.Warn("未能加载 wildcard 片段文件",
                    context: Logger.Context(("folder", folderPath), ("reason", ex.Message)));
            }

            PushWildcards();
        }

        internal void AddWildcard(string name, string content) {
            string folderPath = picProps.WildcardFolderPath;
            if (string.IsNullOrWhiteSpace(folderPath)) {
                PushToast("warn", Properties.Resources.Msg_WildcardFolderNotConfigured);
                return;
            }

            if (string.IsNullOrWhiteSpace(name)) {
                Logger.Warn("片段名不能为空", context: Logger.Context(("action", "SnippetAdd")));
                PushToast("warn", "片段名不能为空");
                return;
            }

            try {
                wildcardService.AddSnippet(folderPath, name, content);
                ReloadWildcards();
                Logger.Info("片段已新增",
                    context: Logger.Context(("snippet", name), ("folder", folderPath)));
                PushToast("ok", $"片段「{name}」已新增");
            }
            catch (Exception ex) {
                Logger.Warn("添加片段失败",
                    context: Logger.Context(("snippet", name), ("reason", ex.Message)));
                PushToast("err", string.Format(Properties.Resources.Msg_AddSnippetFailed, ex.Message));
            }
        }

        internal void UpdateWildcard(string name, string content) {
            string folderPath = picProps.WildcardFolderPath;
            if (string.IsNullOrWhiteSpace(folderPath)) {
                PushToast("warn", Properties.Resources.Msg_WildcardFolderNotConfigured);
                return;
            }

            if (string.IsNullOrWhiteSpace(name)) {
                Logger.Warn("未选择要编辑的片段", context: Logger.Context(("action", "SnippetEdit")));
                PushToast("warn", "未选择要编辑的片段");
                return;
            }

            try {
                wildcardService.UpdateSnippet(folderPath, name, content);
                ReloadWildcards();
                Logger.Info("片段已更新", context: Logger.Context(("snippet", name)));
                PushToast("ok", $"片段「{name}」已更新");
            }
            catch (Exception ex) {
                Logger.Warn("更新片段失败",
                    context: Logger.Context(("snippet", name), ("reason", ex.Message)));
                PushToast("err", string.Format(Properties.Resources.Msg_UpdateSnippetFailed, ex.Message));
            }
        }

        internal void DeleteWildcard(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                Logger.Warn("未选择要删除的片段", context: Logger.Context(("action", "SnippetDelete")));
                PushToast("warn", "未选择要删除的片段");
                return;
            }

            try {
                wildcardService.DeleteSnippet(picProps.WildcardFolderPath, name);
                ReloadWildcards();
                PushToast("ok", $"片段「{name}」已删除");
            }
            catch (Exception ex) {
                Logger.Warn("删除片段失败",
                    context: Logger.Context(("snippet", name), ("reason", ex.Message)));
                PushToast("err", string.Format(Properties.Resources.Msg_DeleteSnippetFailed, ex.Message));
            }
        }

        internal void OpenWildcardFolder() {
            string folderPath = picProps.WildcardFolderPath;
            if (string.IsNullOrWhiteSpace(folderPath))
                return;

            try {
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);
                System.Diagnostics.Process.Start(folderPath);
            }
            catch (Exception ex) {
                Logger.Warn("无法打开 wildcard 目录",
                    context: Logger.Context(("path", folderPath), ("reason", ex.Message)));
            }
        }

        #endregion
    }
}