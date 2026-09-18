using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoNai3Tools.utils;

namespace AutoNai3Tools {
    public partial class Form1 {
        #region Director Tools

        private static readonly string[] DirectorImageExtensions =
            { ".xbm", ".tif", ".ico", ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        internal DirectorToolExecutionOptions CaptureDirectorToolOptions() {
            return new DirectorToolExecutionOptions {
                Iterations = Math.Max(1, DirectorIterations),
                ColorizePrompt = ColorizePrompt ?? string.Empty,
                ColorizeDefry = ColorizeDefry,
                Emotion = EmotionValue ?? string.Empty,
                EmotionPrompt = EmotionPrompt ?? string.Empty,
                EmotionDefry = EmotionDefry,
                Token = settingProps.Token,
                Proxy = settingProps.Proxy
            };
        }

        internal async Task RunDirectorToolAsync() {
            var options = CaptureDirectorToolOptions();

            if (DirectorBatchMode) {
                string folderPath = DirectorFolderPath;
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) {
                    PushToast("warn", Properties.Resources.Msg_SelectValidInputFolder);
                    return;
                }

                List<string> files;
                try {
                    files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                        .Where(file => DirectorImageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToList();
                }
                catch (Exception ex) {
                    Logger.Error("读取导演工具输入目录失败", exception: ex,
                        context: Logger.Context(("folder", folderPath)));
                    PushToast("err", "读取目录失败：" + ex.Message);
                    return;
                }

                if (files.Count == 0) {
                    PushToast("warn", Properties.Resources.Msg_NoImagesInFolder);
                    return;
                }

                await directorToolController.RunBatchAsync(files, DirectorTab, options);
                return;
            }

            if (string.IsNullOrWhiteSpace(DirectorInputPath) || !File.Exists(DirectorInputPath)) {
                PushToast("warn", Properties.Resources.Msg_SelectImageFirst);
                return;
            }

            await directorToolController.RunSingleAsync(DirectorInputPath, DirectorTab, options);
        }

        internal void OpenOutputFolder() {
            try {
                string path = picProps.OutputPath;
                if (string.IsNullOrWhiteSpace(path))
                    return;
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start(path);
            }
            catch (Exception ex) {
                Logger.Warn("无法打开输出目录",
                    context: Logger.Context(("path", picProps.OutputPath), ("reason", ex.Message)));
            }
        }

        #endregion
    }
}