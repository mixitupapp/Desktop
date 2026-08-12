using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace MixItUp.Base.Model.VPZone.Users
{
    /// <summary>
    /// A VPZone member profile. Backs both GET /users/{username} and the profile nested inside
    /// GET /me, so the VPZ+ fields are only populated on the former.
    /// </summary>
    public class VPZoneUserModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("bio")]
        public string Bio { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty("is_verified")]
        public bool IsVerified { get; set; }

        [JsonProperty("is_streamer")]
        public bool IsStreamer { get; set; }

        [JsonProperty("creator_tier")]
        public string CreatorTier { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        // GET /users/{username} extends the profile with the member's VPZ+ standing. vpz_plus_since is
        // the first-ever activation (the membership anniversary), never reset by a renewal, and null
        // whenever the membership is not currently active.
        [JsonProperty("vpz_plus_active")]
        public bool VPZPlusActive { get; set; }

        [JsonProperty("vpz_plus_since")]
        public string VPZPlusSince { get; set; }

        [JsonProperty("channel")]
        public VPZoneUserChannelModel Channel { get; set; }

        [JsonIgnore]
        public string UserID { get { return this.ID ?? string.Empty; } }

        [JsonIgnore]
        public string BestAvatarUrl { get { return FirstNonEmpty(this.AvatarUrl); } }

        [JsonIgnore]
        public string BestDisplayName { get { return FirstNonEmpty(this.DisplayName, this.Username); } }

        /// <summary>The member's own channel slug, which is the room name the chat gateway keys on.</summary>
        [JsonIgnore]
        public string ChannelSlug { get { return FirstNonEmpty(this.Channel?.Slug, this.Username?.ToLowerInvariant()); } }

        internal static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
    }

    /// <summary>The channel summary GET /users/{username} nests under "channel" (null for non-streamers).</summary>
    public class VPZoneUserChannelModel
    {
        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("is_live")]
        public bool IsLive { get; set; }

        [JsonProperty("viewer_count")]
        public int ViewerCount { get; set; }
    }

    /// <summary>
    /// GET /me: the token's own metadata (auth source, granted scopes, rate limit) plus the
    /// authenticated member's profile.
    /// </summary>
    public class VPZoneTokenInfoModel
    {
        [JsonProperty("auth_source")]
        public string AuthSource { get; set; }

        [JsonProperty("key_id")]
        public string KeyID { get; set; }

        [JsonProperty("client_id")]
        public string ClientID { get; set; }

        [JsonProperty("scopes")]
        public System.Collections.Generic.List<string> Scopes { get; set; } = new System.Collections.Generic.List<string>();

        [JsonProperty("rate_limit_rpm")]
        public int RateLimitPerMinute { get; set; }

        [JsonProperty("profile")]
        public VPZoneUserModel Profile { get; set; }
    }

    /// <summary>
    /// GET /me/vpz-plus. "since" is the first-ever activation and never resets on renewal, so it is
    /// the membership anniversary; it is null whenever the membership is not currently active.
    /// "source" is stripe for a paid recurring subscription or granted for a gift, referral or comp.
    /// </summary>
    public class VPZPlusMembershipModel
    {
        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("since")]
        public string Since { get; set; }

        [JsonProperty("current_period_end")]
        public string CurrentPeriodEnd { get; set; }

        [JsonProperty("cancel_at_period_end")]
        public bool CancelAtPeriodEnd { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        /// <summary>Whole months between the first activation and now, or 0 when it cannot be resolved.</summary>
        [JsonIgnore]
        public int MonthsActive
        {
            get
            {
                if (!this.Active || string.IsNullOrWhiteSpace(this.Since) ||
                    !System.DateTimeOffset.TryParse(this.Since, out System.DateTimeOffset since))
                {
                    return 0;
                }

                System.DateTimeOffset now = System.DateTimeOffset.Now;
                int months = ((now.Year - since.Year) * 12) + now.Month - since.Month;
                if (now.Day < since.Day)
                {
                    months--;
                }
                return System.Math.Max(months, 0);
            }
        }
    }

    /// <summary>
    /// Every VPZone REST response wraps its payload in { "data": ... } and every error in
    /// { "error": { "code", "message" } }. This unwraps the envelope for the typed models.
    /// </summary>
    public static class VPZoneResponseEnvelope
    {
        public static JToken Unwrap(JToken response)
        {
            if (response is JObject obj && obj["data"] != null)
            {
                return obj["data"];
            }
            return response;
        }

        public static T Unwrap<T>(JToken response) where T : class
        {
            JToken data = Unwrap(response);
            if (data == null || data.Type == JTokenType.Null)
            {
                return null;
            }

            try
            {
                return data.ToObject<T>();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Pulls the human-readable part out of an { error: { code, message } } body.</summary>
        public static string ExtractErrorMessage(string responseBody)
        {
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    JObject jobj = JObject.Parse(responseBody);
                    JToken error = jobj["error"];
                    if (error is JObject errorObj)
                    {
                        string message = errorObj.Value<string>("message");
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            return message;
                        }
                        string code = errorObj.Value<string>("code");
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            return code;
                        }
                    }
                    else if (error != null && error.Type == JTokenType.String)
                    {
                        return error.ToString();
                    }
                }
                catch (JsonException) { }
            }
            return responseBody ?? string.Empty;
        }

        /// <summary>The machine-readable error.code, used to branch on a rejection reason.</summary>
        public static string ExtractErrorCode(string responseBody)
        {
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    return (JObject.Parse(responseBody)["error"] as JObject)?.Value<string>("code");
                }
                catch (JsonException) { }
            }
            return null;
        }
    }
}
