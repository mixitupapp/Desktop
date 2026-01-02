using System;
using System.Runtime.Serialization;

namespace MixItUp.API.V2.Models
{
    [DataContract]
    public class GetCurrencyAmountResponse
    {
        [DataMember]
        public Guid CurrencyID { get; set; }

        [DataMember]
        public string CurrencyName { get; set; }

        [DataMember]
        public Guid UserID { get; set; }

        [DataMember]
        public int Amount { get; set; }
    }
}