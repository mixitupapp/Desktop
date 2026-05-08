using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class WebhookUserReferenceModel
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
        public WebhookIdentityModel Identity { get; set; }
    }

    public class WebhookIdentityModel
    {
        [JsonProperty("username_color")]
        public string UsernameColor { get; set; }

        [JsonProperty("badges")]
        public List<WebhookBadgeModel> Badges { get; set; } = new List<WebhookBadgeModel>();
    }

    public class WebhookBadgeModel
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("count")]
        public int? Count { get; set; }
    }
}
