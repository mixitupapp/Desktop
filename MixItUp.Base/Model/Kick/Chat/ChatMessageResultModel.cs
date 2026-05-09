using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Chat
{
    public class ChatMessageResultModel
    {
        [JsonProperty("is_sent")]
        public bool IsSent { get; set; }

        [JsonProperty("message_id")]
        public string MessageID { get; set; }
    }
}

