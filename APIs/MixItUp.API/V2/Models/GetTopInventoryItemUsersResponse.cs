using System;
using System.Collections.Generic;

namespace MixItUp.API.V2.Models
{
    public class GetTopInventoryItemUsersResponse
    {
        public Guid InventoryID { get; set; }
        public string InventoryName { get; set; }
        public Guid ItemID { get; set; }
        public string ItemName { get; set; }
        public List<InventoryItemUserAmount> Users { get; set; } = new List<InventoryItemUserAmount>();
    }

    public class InventoryItemUserAmount
    {
        public Guid UserID { get; set; }
        public string Username { get; set; }
        public int Amount { get; set; }
    }
}
