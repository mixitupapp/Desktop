using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class WebhookChannelFollowedEventModel
    {
        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("follower")]
        public WebhookUserReferenceModel Follower { get; set; }
    }
}
