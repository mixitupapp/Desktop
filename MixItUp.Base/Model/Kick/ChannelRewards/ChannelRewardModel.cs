using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.ChannelRewards
{
    public class ChannelRewardModel
    {
        [JsonProperty("background_color")]
        public string BackgroundColor { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty("is_paused")]
        public bool IsPaused { get; set; }

        [JsonProperty("is_user_input_required")]
        public bool IsUserInputRequired { get; set; }

        [JsonProperty("should_redemptions_skip_request_queue")]
        public bool ShouldRedemptionsSkipRequestQueue { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }

    public class ChannelRewardRedemptionsByRewardModel
    {
        [JsonProperty("redemptions")]
        public List<ChannelRewardRedemptionModel> Redemptions { get; set; }

        [JsonProperty("reward")]
        public MinimalChannelRewardModel Reward { get; set; }
    }

    public class ChannelRewardRedemptionModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("redeemed_at")]
        public string RedeemedAt { get; set; }

        [JsonProperty("redeemer")]
        public ChannelRewardUserInfoModel Redeemer { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("user_input")]
        public string UserInput { get; set; }
    }

    public class ChannelRewardUserInfoModel
    {
        [JsonProperty("user_id")]
        public long UserID { get; set; }
    }

    public class MinimalChannelRewardModel
    {
        [JsonProperty("can_manage")]
        public bool CanManage { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("is_deleted")]
        public bool IsDeleted { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }

    public class FailedRedemptionModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }
}
