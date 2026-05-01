using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookUserReferenceModel
    {
        [JsonProperty("is_anonymous")]
        public bool IsAnonymous { get; set; }

        [JsonProperty("user_id")]
        public long UserID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("is_verified")]
        public bool? IsVerified { get; set; }

        [JsonProperty("profile_picture")]
        public string ProfilePicture { get; set; }

        [JsonProperty("channel_slug")]
        public string ChannelSlug { get; set; }

        [JsonProperty("identity")]
        public KickWebhookIdentityModel Identity { get; set; }
    }

    public class KickWebhookIdentityModel
    {
        [JsonProperty("username_color")]
        public string UsernameColor { get; set; }

        [JsonProperty("badges")]
        public List<KickWebhookBadgeModel> Badges { get; set; } = new List<KickWebhookBadgeModel>();
    }

    public class KickWebhookBadgeModel
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("count")]
        public int? Count { get; set; }
    }
}
