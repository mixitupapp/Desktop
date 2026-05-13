using MixItUp.Base.Services.Twitch.New;
using System;

namespace MixItUp.Base.Model.Twitch.EventSub
{
    public enum ChannelCustomPowerUpRedemptionNotificationStatus
    {
        Unknown,
        Unfulfilled,
        Fulfilled,
        Canceled,
    }

    public class ChannelCustomPowerUpRedemptionNotification
    {
        public string id { get; set; }
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string user_id { get; set; }
        public string user_login { get; set; }
        public string user_name { get; set; }
        public string user_input { get; set; }
        public string status { get; set; }
        public ChannelCustomPowerUp custom_power_up { get; set; }
        public string redeemed_at { get; set; }

        public ChannelCustomPowerUpRedemptionNotificationStatus StatusType
        {
            get
            {
                switch (status)
                {
                    case "unfulfilled": return ChannelCustomPowerUpRedemptionNotificationStatus.Unfulfilled;
                    case "fulfilled": return ChannelCustomPowerUpRedemptionNotificationStatus.Fulfilled;
                    case "canceled": return ChannelCustomPowerUpRedemptionNotificationStatus.Canceled;
                    default: return ChannelCustomPowerUpRedemptionNotificationStatus.Unknown;
                }
            }
        }

        public DateTimeOffset RedeemedAt { get { return TwitchService.GetTwitchDateTime(redeemed_at); } }
    }

    public class ChannelCustomPowerUp
    {
        public string id { get; set; }
        public string title { get; set; }
        public int bits { get; set; }
        public string prompt { get; set; }
    }
}
