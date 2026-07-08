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
}
