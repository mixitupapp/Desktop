using MixItUp.Base.Model.Velora.Users;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Velora.Bots
{
    /// <summary>
    /// A Velora bot: a first-class platform entity owned by the streamer's account (created in Velora
    /// Bot Studio or via POST /api/integrations/oauth/bot/create) that an OAuth app connects to in order
    /// to send chat as the bot. Velora publishes no response schema for the bot endpoints (the OpenAPI
    /// DTOs are empty), so the field set mirrors UpdateBotDto - the one bot schema Velora does publish -
    /// with defensive aliases in the style of the other Velora models.
    /// </summary>
    public class VeloraBotModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("botId")]
        public string BotId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("botName")]
        public string BotName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("isActive")]
        public bool? IsActive { get; set; }

        [JsonIgnore]
        public string BestID { get { return UserModel.FirstNonEmpty(this.ID, this.BotId); } }

        [JsonIgnore]
        public string BestUsername { get { return UserModel.FirstNonEmpty(this.Username, this.BotName, this.Name, this.DisplayName); } }

        [JsonIgnore]
        public string BestDisplayName { get { return UserModel.FirstNonEmpty(this.DisplayName, this.Username, this.BotName, this.Name); } }

        [JsonIgnore]
        public string BestAvatarUrl { get { return UserModel.FirstNonEmpty(this.AvatarUrl, this.Avatar); } }

        /// <summary>Parses GET /api/integrations/oauth/bot/available - confirmed live as { success, bots: [] } -
        /// tolerating a bare array or a data[] envelope.</summary>
        public static List<VeloraBotModel> ParseList(JToken response)
        {
            List<VeloraBotModel> bots = new List<VeloraBotModel>();
            JObject envelope = response as JObject;
            JArray items = response as JArray ?? envelope?["bots"] as JArray ?? envelope?["data"] as JArray;
            if (items != null)
            {
                foreach (JToken item in items)
                {
                    VeloraBotModel bot = ParseSingle(item);
                    if (bot != null && !string.IsNullOrWhiteSpace(bot.BestID))
                    {
                        bots.Add(bot);
                    }
                }
            }
            return bots;
        }

        /// <summary>Parses a lone bot object, unwrapping a { bot: {...} } envelope if present.</summary>
        public static VeloraBotModel ParseSingle(JToken token)
        {
            if (token is JObject jobj)
            {
                if (jobj["bot"] is JObject nested)
                {
                    jobj = nested;
                }

                try
                {
                    return jobj.ToObject<VeloraBotModel>();
                }
                catch (JsonException)
                {
                    return null;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// GET /api/integrations/oauth/bot/current - confirmed live as { connected: false, bot: null } when
    /// no bot is connected; when connected the bot object is the app's effective chat identity.
    /// </summary>
    public class VeloraBotCurrentResponseModel
    {
        [JsonProperty("connected")]
        public bool Connected { get; set; }

        [JsonProperty("bot")]
        public VeloraBotModel Bot { get; set; }

        [JsonIgnore]
        public bool HasConnectedBot { get { return this.Connected && this.Bot != null && !string.IsNullOrWhiteSpace(this.Bot.BestID); } }
    }
}
