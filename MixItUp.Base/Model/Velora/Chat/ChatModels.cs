using Newtonsoft.Json;

namespace MixItUp.Base.Model.Velora.Chat
{
    public class SendChatMessageResponseModel
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("messageId")]
        public string MessageID { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }
    }

    /// <summary>
    /// GET / PATCH integrations/oauth/chat/settings. Shape confirmed live:
    /// { slowMode, slowModeSeconds, followersOnly, subscribersOnly, emoteOnly }.
    /// </summary>
    public class VeloraChatSettingsModel
    {
        [JsonProperty("slowMode")]
        public bool SlowMode { get; set; }

        [JsonProperty("slowModeSeconds")]
        public int SlowModeSeconds { get; set; }

        [JsonProperty("followersOnly")]
        public bool FollowersOnly { get; set; }

        [JsonProperty("subscribersOnly")]
        public bool SubscribersOnly { get; set; }

        [JsonProperty("emoteOnly")]
        public bool EmoteOnly { get; set; }
    }
}
