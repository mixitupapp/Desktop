using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Chat
{
    public class KickChatMessageResultModel
    {
        [JsonProperty("is_sent")]
        public bool IsSent { get; set; }

        [JsonProperty("message_id")]
        public string MessageID { get; set; }
    }
}

