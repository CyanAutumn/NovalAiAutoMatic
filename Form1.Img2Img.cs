using System;
using System.IO;
using AutoNai3Tools.utils;

namespace AutoNai3Tools {
    public partial class Form1 {
        #region Img2Img

        internal void SetImg2ImgPath(string path) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                PushToast("warn", "图片不存在");
                return;
            }

            Img2ImgPath = path;
            PushImg2Img();
        }

        internal void ClearImg2Img() {
            Img2ImgPath = null;
            PushImg2Img();
        }

        #endregion
    }
}