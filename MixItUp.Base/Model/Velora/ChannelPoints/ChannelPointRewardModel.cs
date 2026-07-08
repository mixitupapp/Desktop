using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Velora.ChannelPoints
{
    public class ChannelPointRewardModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("builtInType")]
        public string BuiltInType { get; set; }

        [JsonProperty("requiresModeratorApproval")]
        public bool? RequiresModeratorApproval { get; set; }

        [JsonProperty("maxPerStream")]
        public int? MaxPerStream { get; set; }

        [JsonProperty("maxPerUserPerStream")]
        public int? MaxPerUserPerStream { get; set; }
    }

    public class ChannelPointRewardsResponseModel
    {
        [JsonProperty("items")]
        public List<ChannelPointRewardModel> Items { get; set; }

        [JsonProperty("rewards")]
        public List<ChannelPointRewardModel> Rewards { get; set; }

        [JsonIgnore]
        public List<ChannelPointRewardModel> AllRewards
        {
            get
            {
                List<ChannelPointRewardModel> rewards = new List<ChannelPointRewardModel>();
                if (this.Items != null) { rewards.AddRange(this.Items); }
                if (this.Rewards != null) { rewards.AddRange(this.Rewards); }
                return rewards;
            }
        }
    }
}
