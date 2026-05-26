namespace MixItUp.Base.Util
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

#if DEBUG_BUILD
        public const string API_ROUTE_UPDATE_LATEST = "https://dev.files.mixitupapp.com/apps/mixitup-desktop/windows-x64";
#else
        public const string API_ROUTE_UPDATE_LATEST = "https://files.mixitupapp.com/apps/mixitup-desktop/windows-x64";
#endif
    }
}
