using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.YouTube.New;
using MixItUp.Base.Services.Trovo.New;

namespace MixItUp.Base.Util
{
    public static class UtilServiceHelper
    {
        public static string GenerateClientKey()
        {
            string twitchId = ServiceManager.Get<TwitchSession>()?.StreamerID ?? "0";
            string youtubeId = ServiceManager.Get<YouTubeSession>()?.StreamerID ?? "0";
            string trovoId = ServiceManager.Get<TrovoSession>()?.StreamerID ?? "0";

            return $"tw{twitchId}yt{youtubeId}tr{trovoId}";
        }
    }
}