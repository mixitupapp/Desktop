using System;
using System.Collections.Generic;

namespace MixItUp.API.V2.Models
{
    public class GetTopCurrencyUsersResponse
    {
        public Guid CurrencyID { get; set; }
        public string CurrencyName { get; set; }
        public List<CurrencyUserAmount> Users { get; set; } = new List<CurrencyUserAmount>();
    }

    public class CurrencyUserAmount
    {
        public Guid UserID { get; set; }
        public string Username { get; set; }
        public int Amount { get; set; }
    }
}
