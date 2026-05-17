namespace MixItUp.WPF.Util
{
    public static class BuildChannelHelper
    {
        public static string GetChannelSuffix()
        {
#if PREVIEW_BUILD
            return " - Preview";
#elif TEST_BUILD
            return " - Test";
#elif DEBUG_BUILD
            return " - Debug";
#else
            return string.Empty;
#endif
        }
    }
}
