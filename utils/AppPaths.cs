using System;
using System.IO;

namespace AutoNai3Tools.utils {
    /// <summary>程序运行时用到的固定路径。</summary>
    internal static class AppPaths {
        public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>HTML 界面资源目录（随程序一起发布）。</summary>
        public static string WebUiRoot => Path.Combine(BaseDir, "webui");

        public static string LocalRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoNai3Tools");

        public static string WebViewUserDataRoot => Path.Combine(LocalRoot, "WebView2", "userdata");

        /// <summary>生成结果 / 导演工具结果的临时预览目录，通过 preview.local 虚拟域名暴露给前端。</summary>
        public static string PreviewRoot => Path.Combine(LocalRoot, "WebView2", "preview");
    }
}