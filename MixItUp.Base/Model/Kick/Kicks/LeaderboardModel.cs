using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Kicks
{
    public class LeaderboardModel
    {
        [JsonProperty("lifetime")]
        public List<LeaderboardEntryModel> Lifetime { get; set; }

        [JsonProperty("month")]
        public List<LeaderboardEntryModel> Month { get; set; }

        [JsonProperty("week")]
        public List<LeaderboardEntryModel> Week { get; set; }
    }

    public class LeaderboardEntryModel
    {
        [JsonProperty("gifted_amount")]
        public int GiftedAmount { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("user_id")]
        public long UserID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }
    }
}
