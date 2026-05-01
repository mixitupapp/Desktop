using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Users
{
    public class KickUserModel
    {
        [JsonProperty("user_id")]
        public long UserID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("profile_picture")]
        public string ProfilePicture { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }
}

