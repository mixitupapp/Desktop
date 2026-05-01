using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Users
{
    public class KickUserModel
    {
        [JsonProperty("user_id")]
        public long UserID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("profile_picture")]
        public string ProfilePicture { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("is_verified")]
        public bool? IsVerified { get; set; }

        [JsonProperty("is_anonymous")]
        public bool IsAnonymous { get; set; }

        [JsonProperty("channel_slug")]
        public string ChannelSlug { get; set; }

        [JsonProperty("identity")]
        public KickIdentityModel Identity { get; set; }
    }

    public class KickIdentityModel
    {
        [JsonProperty("username_color")]
        public string UsernameColor { get; set; }

        [JsonProperty("badges")]
        public KickBadgeModel[] Badges { get; set; }
    }

    public class KickBadgeModel
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("count")]
        public int? Count { get; set; }
    }
}

