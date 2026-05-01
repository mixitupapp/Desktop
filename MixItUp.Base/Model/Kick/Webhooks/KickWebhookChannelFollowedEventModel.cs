using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookChannelFollowedEventModel
    {
        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("follower")]
        public KickWebhookUserReferenceModel Follower { get; set; }
    }
}
