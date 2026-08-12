using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace MixItUp.Base.Model.VPZone.ChannelPoints
{
    /// <summary>
    /// A channel-points reward from GET /channels/{slug}/points/rewards. Built-in rewards (highlight
    /// and announce) sit alongside the streamer's own custom ones, which is why the editor maps on id.
    /// </summary>
    public class VPZoneChannelPointRewardModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        /// <summary>Stable identifier for the built-in rewards; null on custom ones.</summary>
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        /// <summary>"highlight" | "announce" | "custom".</summary>
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("require_prompt")]
        public bool RequirePrompt { get; set; }

        [JsonProperty("prompt_label")]
        public string PromptLabel { get; set; }

        /// <summary>Per-reward global cooldown in seconds, 0 for none.</summary>
        [JsonProperty("cooldown_sec")]
        public int CooldownSeconds { get; set; }

        [JsonProperty("is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty("is_builtin")]
        public bool IsBuiltIn { get; set; }

        public static IEnumerable<VPZoneChannelPointRewardModel> ParseList(JToken response)
        {
            List<VPZoneChannelPointRewardModel> rewards = new List<VPZoneChannelPointRewardModel>();

            if (Users.VPZoneResponseEnvelope.Unwrap(response) is JArray array)
            {
                foreach (JToken item in array)
                {
                    VPZoneChannelPointRewardModel reward = null;
                    try
                    {
                        reward = item?.ToObject<VPZoneChannelPointRewardModel>();
                    }
                    catch (JsonException) { }

                    if (!string.IsNullOrWhiteSpace(reward?.ID))
                    {
                        rewards.Add(reward);
                    }
                }
            }

            return rewards;
        }
    }

    /// <summary>A redemption from GET /channels/{slug}/points/redemptions, newest first.</summary>
    public class VPZoneChannelPointRedemptionModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("user")]
        public Subscriptions.VPZoneMemberEventModel User { get; set; }

        [JsonProperty("reward_id")]
        public string RewardID { get; set; }

        /// <summary>The text the redeemer entered, when the reward requires a prompt.</summary>
        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("points_spent")]
        public int PointsSpent { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    /// <summary>GET /channels/{slug}/points/balance - a single viewer's standing on the channel.</summary>
    public class VPZoneChannelPointBalanceModel
    {
        [JsonProperty("user_id")]
        public string UserID { get; set; }

        [JsonProperty("balance")]
        public int Balance { get; set; }

        [JsonProperty("lifetime_earned")]
        public int LifetimeEarned { get; set; }
    }

    /// <summary>
    /// POST /channels/{slug}/points/grant. replayed is true when an idempotency key matched a prior
    /// grant, in which case nothing was credited a second time.
    /// </summary>
    public class VPZoneChannelPointGrantResultModel
    {
        [JsonProperty("new_balance")]
        public int NewBalance { get; set; }

        [JsonProperty("granted")]
        public int Granted { get; set; }

        [JsonProperty("ledger_id")]
        public string LedgerID { get; set; }

        [JsonProperty("replayed")]
        public bool Replayed { get; set; }
    }
}
