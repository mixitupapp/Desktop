using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.YouTube.New;

namespace MixItUp.Base.Util
{
    public static class UtilServiceHelper
    {
        public static string GenerateClientKey()
        {
            string twitchId = ServiceManager.Get<TwitchSession>()?.StreamerID ?? "0";
            string youtubeId = ServiceManager.Get<YouTubeSession>()?.StreamerID ?? "0";

            return $"tw{twitchId}yt{youtubeId}tr0";
        }
    }
}
