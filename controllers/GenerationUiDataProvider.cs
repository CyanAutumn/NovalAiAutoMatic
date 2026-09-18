using System;
using System.Collections.Generic;
using AutoNai3Tools.utils;

namespace AutoNai3Tools.Controllers {
    /// <summary>
    /// 生成流程与界面之间的适配器：界面已经换成 WebView2 + HTML，
    /// 所以这里只依赖「取值委托」，不再直接引用任何 WinForms 控件。
    /// </summary>
    internal sealed class GenerationUiDataProvider : IGenerationDataProvider {
        private readonly PicProperty picProps;
        private readonly SettingProperty settingProps;
        private readonly IPromptContext promptContext;
        private readonly Func<string> promptAccessor;
        private readonly Func<string> negativePromptAccessor;
        private readonly Func<List<VibeSelection>> vibeAccessor;
        private readonly Func<Img2ImgOptions> img2ImgFactory;

        public GenerationUiDataProvider(
            PicProperty picProps,
            SettingProperty settingProps,
            IPromptContext promptContext,
            Func<string> promptAccessor,
            Func<string> negativePromptAccessor,
            Func<List<VibeSelection>> vibeAccessor,
            Func<Img2ImgOptions> img2ImgFactory) {
            this.picProps = picProps ?? throw new ArgumentNullException(nameof(picProps));
            this.settingProps = settingProps ?? throw new ArgumentNullException(nameof(settingProps));
            this.promptContext = promptContext ?? throw new ArgumentNullException(nameof(promptContext));
            this.promptAccessor = promptAccessor ?? throw new ArgumentNullException(nameof(promptAccessor));
            this.negativePromptAccessor = negativePromptAccessor ?? throw new ArgumentNullException(nameof(negativePromptAccessor));
            this.vibeAccessor = vibeAccessor;
            this.img2ImgFactory = img2ImgFactory;
        }

        public PicProperty PicProps => picProps;
        public SettingProperty SettingProps => settingProps;
        public IPromptContext PromptContext => promptContext;

        public GenerationInput CaptureInput() {
            var promptText = promptAccessor();
            var negativePrompt = negativePromptAccessor();
            var vibes = vibeAccessor?.Invoke() ?? new List<VibeSelection>();
            var img2Img = img2ImgFactory?.Invoke();
            return new GenerationInput(promptText, negativePrompt, vibes, img2Img);
        }
    }
}