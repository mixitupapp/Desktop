using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class KickKicksCommandModel : CommandModelBase
    {
        public static Dictionary<string, string> GetKicksTestSpecialIdentifiers()
        {
            return new Dictionary<string, string>()
            {
                { "kicksamount", "100" },
                { "giftname", "Full Send" },
                { "gifttype", "BASIC" },
                { "gifttier", "BASIC" },
                { "message", "Test Message" },
                { "giftpinnedseconds", "0" }
            };
        }

        [DataMember]
        public int StartingAmount { get; set; }

        [DataMember]
        public int EndingAmount { get; set; }

        public KickKicksCommandModel(string name, int startingAmount, int endingAmount)
            : base(name, CommandTypeEnum.KickKicks)
        {
            this.StartingAmount = startingAmount;
            this.EndingAmount = endingAmount;
        }

        [Obsolete]
        public KickKicksCommandModel() : base() { }

        public bool IsRange { get { return this.StartingAmount != this.EndingAmount; } }

        public bool IsSingle { get { return !this.IsRange; } }

        public int Range { get { return this.EndingAmount - this.StartingAmount; } }

        public string AmountDisplay
        {
            get
            {
                if (this.IsRange)
                {
                    return $"{this.StartingAmount} - {this.EndingAmount}";
                }
                else
                {
                    return this.StartingAmount.ToString();
                }
            }
        }

        public bool IsInRange(int amount) { return this.StartingAmount <= amount && amount <= this.EndingAmount; }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return KickKicksCommandModel.GetKicksTestSpecialIdentifiers(); }
    }
}
