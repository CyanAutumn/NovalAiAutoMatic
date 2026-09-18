using AutoNai3Tools.utils;

namespace AutoNai3Tools {
    public partial class Form1 : IPromptContext {
        PicProperty IPromptContext.PicProps => picProps;
        SettingProperty IPromptContext.SettingProps => settingProps;
        int IPromptContext.RunNumber => runNum;
        int IPromptContext.RunKeepParams => picProps.RunKeepParams;
        string IPromptContext.ArtistFixedText => ArtistFixedText;
        string IPromptContext.ArtistRandomText => ArtistRandomText;
        int IPromptContext.DefaultArtistWeightReduceMax => DefaultArtistWeightReduceMax;
        int IPromptContext.DefaultArtistWeightIncreaseMax => DefaultArtistWeightIncreaseMax;
        double IPromptContext.DefaultArtistWeightReduceDoubleColonMax => DefaultArtistWeightReduceDoubleColonMax;
        double IPromptContext.DefaultArtistWeightIncreaseDoubleColonMax => DefaultArtistWeightIncreaseDoubleColonMax;
        bool IPromptContext.ArtistModify => ArtistModify;
        int IPromptContext.ArtistMin => ArtistMin;
        int IPromptContext.ArtistMax => ArtistMax;

        void IPromptContext.SetRunNumber(int runNumber) {
            runNum = runNumber;
        }
    }
}