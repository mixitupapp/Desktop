using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MixItUp.Base.Model.VPZone.Realtime
{
    /// <summary>
    /// Frame types the chat gateway pushes. VPZone's contract is additive: new type values can appear
    /// at any time, so an unrecognized frame must be ignored rather than treated as an error.
    /// </summary>
    public static class VPZoneFrameTypes
    {
        public const string Message = "msg";
        public const string System = "system";
        public const string Presence = "presence";
        public const string Follow = "follow";
        public const string Subscription = "subscription";
        public const string Gift = "gift";
        public const string Raid = "raid";
        public const string Clip = "clip";
        public const string Shoutout = "shoutout";
        public const string Error = "error";
        public const string DeleteMessage = "delete_message";
        public const string ClearChat = "clear_chat";
        public const string PinUpdate = "pin_update";
    }

    /// <summary>
    /// The metadata.kind discriminator carried by "system" frames, plus the incoming/outgoing
    /// discriminator on "raid" frames. New kinds appear without a version bump.
    /// </summary>
    public static class VPZoneSystemKinds
    {
        public const string StreamStarted = "stream_started";
        public const string StreamEnded = "stream_ended";

        // An announcement is broadcast as a "system" frame rather than a type of its own, so this kind
        // is the only thing that distinguishes it. Match on it, never on the frame's type.
        public const string Announcement = "announcement";

        public const string PixelsCheer = "pixels_cheer";
        public const string LevelUp = "level_up";
        public const string ChannelPointsRedeem = "channel_points_redeem";
        public const string ChannelPointsAnnounce = "channel_points_announce";
        public const string ChannelPointsHighlight = "channel_points_highlight";
        public const string CaseOpened = "case_opened";
        public const string CaseClaimed = "case_claimed";
        public const string CaseGift = "case_gift";
        public const string CaseDrop = "case_drop";
        public const string CaseDropClaim = "case_drop_claim";
        public const string CostreamInvite = "costream_invite";
        public const string CostreamJoin = "costream_join";
        public const string ModerationBan = "moderation_ban";
        public const string ModerationCut = "moderation_cut";
        public const string ModMessage = "mod_message";

        public const string RaidIncoming = "incoming";
        public const string RaidOutgoing = "outgoing";
    }

    /// <summary>
    /// The machine-readable rejection reasons on an "error" frame. Branch on these, never on the text.
    /// </summary>
    public static class VPZoneErrorCodes
    {
        public const string AuthRequired = "auth_required";
        public const string Banned = "banned";
        public const string RateLimited = "rate_limited";
        public const string SubsOnly = "subs_only";
        public const string FollowersOnly = "followers_only";
        public const string SlowMode = "slow_mode";
        public const string EmoteOnly = "emote_only";
        public const string BannedWord = "banned_word";
        public const string CozyFiltered = "cozy_filtered";
    }

    /// <summary>
    /// A single frame from the chat gateway (wss://chat.vpzone.tv/ws). id, type, username, body and ts
    /// are always present; everything else depends on the frame type. Unknown fields and unknown type
    /// values are ignored by design, and body always carries a human-readable fallback.
    /// </summary>
    public class VPZoneChatEventModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        /// <summary>
        /// The raw ts. VPZone does not guarantee a single shape here: it can be epoch milliseconds,
        /// epoch seconds, either of those as a string, or an ISO date string. Their own client runs
        /// every frame through a normalizer rather than trusting the field, so this stays untyped and
        /// <see cref="Timestamp"/> does the same work.
        /// </summary>
        [JsonProperty("ts")]
        public JToken RawTimestamp { get; set; }

        /// <summary>
        /// Unix epoch milliseconds, normalized. Recorded per frame and handed back as ?since= on
        /// reconnect, so a seconds value slipping through unconverted would rewind the cursor by
        /// decades and replay the channel's entire history.
        /// </summary>
        [JsonIgnore]
        public long Timestamp { get { return NormalizeTimestamp(this.RawTimestamp); } }

        /// <summary>
        /// Mirrors VPZone's own normalizeChatTimestamp. Values outside a sane window (before 2000, or
        /// more than a day ahead) are replaced with the current time, which is what their client does
        /// rather than letting a bad frame poison ordering.
        /// </summary>
        public static long NormalizeTimestamp(JToken raw)
        {
            double value;
            if (raw == null || raw.Type == JTokenType.Null)
            {
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            if (raw.Type == JTokenType.Integer || raw.Type == JTokenType.Float)
            {
                value = raw.Value<double>();
            }
            else if (raw.Type == JTokenType.Date)
            {
                value = new DateTimeOffset(raw.Value<DateTime>().ToUniversalTime()).ToUnixTimeMilliseconds();
            }
            else
            {
                string text = raw.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }

                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
                    {
                        value = parsed.ToUnixTimeMilliseconds();
                    }
                    else
                    {
                        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                }
            }

            // Anything this small is seconds rather than milliseconds.
            if (Math.Abs(value) < 100000000000d)
            {
                value *= 1000d;
            }

            long floor = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            long ceiling = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
            long normalized = (long)value;
            return (normalized >= floor && normalized <= ceiling) ? normalized : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        [JsonProperty("color")]
        public string Color { get; set; }

        /// <summary>Connected viewers, on presence frames only.</summary>
        [JsonProperty("count")]
        public int? Count { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("retry_after_ms")]
        public int? RetryAfterMilliseconds { get; set; }

        /// <summary>Echo of the nonce supplied on send, so a broadcast can be matched to its own message.</summary>
        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        /// <summary>Not persisted, so it never appears in the history replay on reconnect.</summary>
        [JsonProperty("ephemeral")]
        public bool Ephemeral { get; set; }

        [JsonProperty("is_subscriber")]
        public bool IsSubscriber { get; set; }

        [JsonProperty("sub_months")]
        public int SubscriberMonths { get; set; }

        /// <summary>"tier1" | "tier2" | "tier3".</summary>
        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("is_owner")]
        public bool IsOwner { get; set; }

        // VPZone puts several of these flags at the top level on some frames and inside metadata on
        // others, and its own client checks both places for every one of them. The raw properties
        // below carry whatever the top level held; the effective ones underneath are what callers
        // should read. Missing the metadata copy of is_mod would silently cost a moderator their role.
        [JsonProperty("is_mod")]
        public bool IsModeratorFlag { get; set; }

        [JsonProperty("is_founder")]
        public bool IsFounderFlag { get; set; }

        [JsonProperty("is_ambassador")]
        public bool IsAmbassadorFlag { get; set; }

        [JsonProperty("guest_pass")]
        public bool GuestPassFlag { get; set; }

        [JsonProperty("is_admin")]
        public bool IsAdminFlag { get; set; }

        [JsonIgnore]
        public bool IsModerator { get { return this.IsModeratorFlag || this.GetMetadataBool("is_mod"); } }

        [JsonIgnore]
        public bool IsFounder { get { return this.IsFounderFlag || this.GetMetadataBool("is_founder"); } }

        [JsonIgnore]
        public bool IsAmbassador { get { return this.IsAmbassadorFlag || this.GetMetadataBool("is_ambassador"); } }

        [JsonIgnore]
        public bool GuestPass { get { return this.GuestPassFlag || this.GetMetadataBool("guest_pass"); } }

        /// <summary>VPZone staff. Carried alongside the other flags but not part of any badge Mix It Up shows.</summary>
        [JsonIgnore]
        public bool IsAdmin { get { return this.IsAdminFlag || this.GetMetadataBool("is_admin"); } }

        [JsonProperty("vpz_plus")]
        public bool VPZPlus { get; set; }

        [JsonProperty("has_discord")]
        public bool HasDiscord { get; set; }

        /// <summary>Set on an error frame with code "banned" when the ban is a timeout that will lapse.</summary>
        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }

        /// <summary>
        /// Token to emote image URL for this message only. VPZone builds it from what the sender was
        /// actually entitled to use, so emotes render from here rather than from a global dictionary.
        /// </summary>
        [JsonProperty("emoteMap")]
        public Dictionary<string, string> EmoteMap { get; set; }

        [JsonProperty("metadata")]
        public JObject Metadata { get; set; }

        /// <summary>The metadata.kind discriminator, which tells a system frame apart from its neighbors.</summary>
        [JsonIgnore]
        public string Kind { get { return this.GetMetadataString("kind"); } }

        [JsonIgnore]
        public int TierNumber { get { return ParseTierNumber(this.Tier); } }

        [JsonIgnore]
        public DateTimeOffset TimestampOffset
        {
            get
            {
                // A frame with no usable ts still needs a sortable timestamp for the chat list.
                return (this.Timestamp > 0) ? DateTimeOffset.FromUnixTimeMilliseconds(this.Timestamp).ToLocalTime() : DateTimeOffset.Now;
            }
        }

        /// <summary>The system frames whose author is the gateway rather than a member.</summary>
        [JsonIgnore]
        public bool IsSystemAuthored { get { return string.Equals(this.Username, "system", StringComparison.OrdinalIgnoreCase); } }

        public string GetMetadataString(string key)
        {
            JToken value = this.Metadata?[key];
            return (value != null && value.Type != JTokenType.Null) ? value.ToString() : null;
        }

        /// <summary>Reads a metadata flag, tolerating the value arriving as a bool or as a string.</summary>
        public bool GetMetadataBool(string key)
        {
            JToken value = this.Metadata?[key];
            if (value == null)
            {
                return false;
            }
            if (value.Type == JTokenType.Boolean)
            {
                return value.Value<bool>();
            }
            return value.Type == JTokenType.String && bool.TryParse(value.ToString(), out bool parsed) && parsed;
        }

        public int? GetMetadataInt(string key)
        {
            JToken value = this.Metadata?[key];
            if (value != null)
            {
                if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                {
                    return value.Value<int>();
                }
                if (value.Type == JTokenType.String && int.TryParse(value.ToString(), out int parsed))
                {
                    return parsed;
                }
            }
            return null;
        }

        public bool MetadataKindIs(string kind)
        {
            return string.Equals(this.Kind, kind, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The reply target denormalized onto a msg frame: { message_id, username, excerpt }.</summary>
        [JsonIgnore]
        public VPZoneReplyReferenceModel ReplyTo
        {
            get
            {
                if (this.Metadata?["reply_to"] is JObject replyTo)
                {
                    try
                    {
                        return replyTo.ToObject<VPZoneReplyReferenceModel>();
                    }
                    catch (JsonException) { }
                }
                return null;
            }
        }

        /// <summary>"tier2" and "2" both resolve to 2; anything unparseable falls back to tier 1.</summary>
        public static int ParseTierNumber(string tier)
        {
            if (!string.IsNullOrWhiteSpace(tier))
            {
                string digits = new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(tier, char.IsDigit)));
                if (int.TryParse(digits, out int result) && result > 0)
                {
                    return result;
                }
            }
            return 1;
        }

        public static VPZoneChatEventModel Parse(string frameJson)
        {
            if (string.IsNullOrWhiteSpace(frameJson))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<VPZoneChatEventModel>(frameJson);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public class VPZoneReplyReferenceModel
    {
        [JsonProperty("message_id")]
        public string MessageID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        /// <summary>A denormalized excerpt of the target message, capped by VPZone at 120 characters.</summary>
        [JsonProperty("excerpt")]
        public string Excerpt { get; set; }
    }

    /// <summary>The only client-to-server frame the gateway accepts. Anything else is ignored.</summary>
    public class VPZoneSendMessageModel
    {
        [JsonProperty("type")]
        public string Type { get; set; } = VPZoneFrameTypes.Message;

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("nonce", NullValueHandling = NullValueHandling.Ignore)]
        public string Nonce { get; set; }

        [JsonProperty("reply_to", NullValueHandling = NullValueHandling.Ignore)]
        public string ReplyTo { get; set; }
    }
}
