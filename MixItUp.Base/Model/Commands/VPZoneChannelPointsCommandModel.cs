using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class VPZoneChannelPointsCommandModel : CommandModelBase
    {
        public static Dictionary<string, string> GetChannelPointTestSpecialIdentifiers()
        {
            return new Dictionary<string, string>()
            {
                { "rewardname", "Hydrate" },
                { "rewardcost", "5" },
                { "message", "Test Message" }
            };
        }

        [DataMember]
        public string ChannelPointRewardID { get; set; } = string.Empty;

        public VPZoneChannelPointsCommandModel(string name, string channelPointRewardID)
            : base(name, CommandTypeEnum.VPZoneChannelPoints)
        {
            this.ChannelPointRewardID = channelPointRewardID;
        }

        [Obsolete]
        public VPZoneChannelPointsCommandModel() : base() { }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return VPZoneChannelPointsCommandModel.GetChannelPointTestSpecialIdentifiers(); }
    }
}
