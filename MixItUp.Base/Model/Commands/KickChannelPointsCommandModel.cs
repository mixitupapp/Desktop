using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class KickChannelPointsCommandModel : CommandModelBase
    {
        public static Dictionary<string, string> GetChannelPointTestSpecialIdentifiers()
        {
            return new Dictionary<string, string>()
            {
                { "rewardname", "Test Reward" },
                { "rewardcost", "100" },
                { "message", "Test Message" }
            };
        }

        [DataMember]
        public string ChannelPointRewardID { get; set; } = string.Empty;

        public KickChannelPointsCommandModel(string name, string channelPointRewardID)
            : base(name, CommandTypeEnum.KickChannelPoints)
        {
            this.ChannelPointRewardID = channelPointRewardID;
        }

        [Obsolete]
        public KickChannelPointsCommandModel() : base() { }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return KickChannelPointsCommandModel.GetChannelPointTestSpecialIdentifiers(); }
    }
}
