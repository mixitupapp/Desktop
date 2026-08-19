using Newtonsoft.Json;

namespace MixItUp.Base.Model.VPZone.Bots
{
    /// <summary>
    /// The identity Mix It Up speaks as when the bot account is connected. VPZone has no bot entity of
    /// its own, so a bot here is simply a second VPZone member who authorized the app: it authenticates
    /// its own OAuth token, opens its own chat gateway connection, and sends with the chat:write scope
    /// exactly as the streamer account does.
    /// </summary>
    public class VPZoneBotModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonIgnore]
        public string BestID { get { return this.ID ?? string.Empty; } }

        [JsonIgnore]
        public string BestUsername { get { return Users.VPZoneUserModel.FirstNonEmpty(this.Username, this.DisplayName); } }

        [JsonIgnore]
        public string BestDisplayName { get { return Users.VPZoneUserModel.FirstNonEmpty(this.DisplayName, this.Username); } }

        [JsonIgnore]
        public string BestAvatarUrl { get { return this.AvatarUrl ?? string.Empty; } }

        public static VPZoneBotModel FromProfile(Users.VPZoneUserModel profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.UserID))
            {
                return null;
            }

            return new VPZoneBotModel()
            {
                ID = profile.UserID,
                Username = profile.Username,
                DisplayName = profile.BestDisplayName,
                AvatarUrl = profile.BestAvatarUrl,
            };
        }
    }
}
