using System;
using System.Collections.Generic;

namespace MixItUp.API.V2.Models
{
    public class GetTopWatchtimeUsersResponse
    {
        public List<WatchtimeUserAmount> Users { get; set; } = new List<WatchtimeUserAmount>();
    }

    public class WatchtimeUserAmount
    {
        public Guid UserID { get; set; }
        public string Username { get; set; }
        public int OnlineViewingMinutes { get; set; }
    }
}
