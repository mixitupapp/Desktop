using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class TwitchCustomPowerUpCommandModel : CommandModelBase
    {
        public static Dictionary<string, string> GetCustomPowerUpTestSpecialIdentifiers()
        {
            return new Dictionary<string, string>()
            {
                { "powerupname", "Test Power-Up" },
                { "powerupcost", "100" },
                { "message", "Test Message" }
            };
        }

        [DataMember]
        public string CustomPowerUpID { get; set; }

        public TwitchCustomPowerUpCommandModel(string name, string customPowerUpID)
            : base(name, CommandTypeEnum.TwitchCustomPowerUp)
        {
            this.CustomPowerUpID = customPowerUpID;
        }

        [Obsolete]
        public TwitchCustomPowerUpCommandModel() : base() { }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return TwitchCustomPowerUpCommandModel.GetCustomPowerUpTestSpecialIdentifiers(); }
    }
}
