using MixItUp.Base.ViewModel.Settings.Generic;
using MixItUp.Base.ViewModels;

namespace MixItUp.Base.ViewModel.Settings
{
    public class AlertsSettingsControlViewModel : UIViewModelBase
    {
        public GenericToggleSettingsOptionControlViewModel OnlyShowAlertsInDashboard { get; set; }

        public GenericColorComboBoxSettingsOptionControlViewModel UserJoinLeave { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel UserFirstMessage { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Follow { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Host { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Raid { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Sub { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel GiftedSub { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel MassGiftedSub { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchBitsCheered { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchChannelPoints { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchCustomPowerUps { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchHypeTrain { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchAds { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchWatchStreak { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel YouTubeSuperChat { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel YouTubeJewelsGift { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Donation { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Streamloots { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Moderation { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel KickChannelPoints { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel KickKicks { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchUserWarned { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchShoutoutReceived { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchSuspiciousUserMessage { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel TwitchSuspiciousUserUpdated { get; set; }

        public AlertsSettingsControlViewModel()
        {
            this.OnlyShowAlertsInDashboard = new GenericToggleSettingsOptionControlViewModel(MixItUp.Base.Resources.OnlyShowAlertsInDashboard, ChannelSession.Settings.OnlyShowAlertsInDashboard, (value) => { ChannelSession.Settings.OnlyShowAlertsInDashboard = value; });

            this.UserJoinLeave = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowUserJoinLeave, ChannelSession.Settings.AlertUserJoinLeaveColor, (value) => { ChannelSession.Settings.AlertUserJoinLeaveColor = value; });
            this.UserFirstMessage = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowUserFirstMessage, ChannelSession.Settings.AlertUserFirstMessageColor, (value) => { ChannelSession.Settings.AlertUserFirstMessageColor = value; });
            this.Follow = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowFollows, ChannelSession.Settings.AlertFollowColor, (value) => { ChannelSession.Settings.AlertFollowColor = value; });
            this.Host = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowHosts, ChannelSession.Settings.AlertHostColor, (value) => { ChannelSession.Settings.AlertHostColor = value; });
            this.Raid = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowRaids, ChannelSession.Settings.AlertRaidColor, (value) => { ChannelSession.Settings.AlertRaidColor = value; });
            this.Sub = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowSubsResubs, ChannelSession.Settings.AlertSubColor, (value) => { ChannelSession.Settings.AlertSubColor = value; });
            this.GiftedSub = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowGiftedSubs, ChannelSession.Settings.AlertGiftedSubColor, (value) => { ChannelSession.Settings.AlertGiftedSubColor = value; });
            this.MassGiftedSub = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowMassGiftedSubs, ChannelSession.Settings.AlertMassGiftedSubColor, (value) => { ChannelSession.Settings.AlertMassGiftedSubColor = value; });
            this.TwitchBitsCheered = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchBitsCheered, ChannelSession.Settings.AlertTwitchBitsCheeredColor, (value) => { ChannelSession.Settings.AlertTwitchBitsCheeredColor = value; });
            this.TwitchChannelPoints = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchChannelPoints, ChannelSession.Settings.AlertTwitchChannelPointsColor, (value) => { ChannelSession.Settings.AlertTwitchChannelPointsColor = value; });
            this.TwitchCustomPowerUps = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchCustomPowerUps, ChannelSession.Settings.AlertTwitchCustomPowerUpsColor, (value) => { ChannelSession.Settings.AlertTwitchCustomPowerUpsColor = value; });
            this.TwitchHypeTrain = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchHypeTrain, ChannelSession.Settings.AlertTwitchHypeTrainColor, (value) => { ChannelSession.Settings.AlertTwitchHypeTrainColor = value; });
            this.TwitchAds = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchAds, ChannelSession.Settings.AlertTwitchAdsColor, (value) => { ChannelSession.Settings.AlertTwitchAdsColor = value; });
            this.TwitchWatchStreak = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchWatchStreak, ChannelSession.Settings.AlertTwitchWatchStreakColor, (value) => { ChannelSession.Settings.AlertTwitchWatchStreakColor = value; });
            this.YouTubeSuperChat = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowYouTubeSuperChat, ChannelSession.Settings.AlertYouTubeSuperChatColor, (value) => { ChannelSession.Settings.AlertYouTubeSuperChatColor = value; });
            this.YouTubeJewelsGift = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowYouTubeJewelsGift, ChannelSession.Settings.AlertYouTubeJewelsGiftColor, (value) => { ChannelSession.Settings.AlertYouTubeJewelsGiftColor = value; });
            this.Donation = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowDonations, ChannelSession.Settings.AlertDonationColor, (value) => { ChannelSession.Settings.AlertDonationColor = value; });
            this.Streamloots = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowStreamloots, ChannelSession.Settings.AlertStreamlootsColor, (value) => { ChannelSession.Settings.AlertStreamlootsColor = value; });
            this.Moderation = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowModeration, ChannelSession.Settings.AlertModerationColor, (value) => { ChannelSession.Settings.AlertModerationColor = value; });
            this.KickChannelPoints = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowKickChannelPoints, ChannelSession.Settings.AlertKickChannelPointsColor, (value) => { ChannelSession.Settings.AlertKickChannelPointsColor = value; });
            this.KickKicks = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowKickKicks, ChannelSession.Settings.AlertKickKicksColor, (value) => { ChannelSession.Settings.AlertKickKicksColor = value; });
            this.TwitchUserWarned = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchUserWarned, ChannelSession.Settings.AlertTwitchUserWarnedColor, (value) => { ChannelSession.Settings.AlertTwitchUserWarnedColor = value; });
            this.TwitchShoutoutReceived = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchShoutoutReceived, ChannelSession.Settings.AlertTwitchShoutoutReceivedColor, (value) => { ChannelSession.Settings.AlertTwitchShoutoutReceivedColor = value; });
            this.TwitchSuspiciousUserMessage = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchSuspiciousUserMessage, ChannelSession.Settings.AlertTwitchSuspiciousUserMessageColor, (value) => { ChannelSession.Settings.AlertTwitchSuspiciousUserMessageColor = value; });
            this.TwitchSuspiciousUserUpdated = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowTwitchSuspiciousUserUpdated, ChannelSession.Settings.AlertTwitchSuspiciousUserUpdatedColor, (value) => { ChannelSession.Settings.AlertTwitchSuspiciousUserUpdatedColor = value; });
        }
    }
}
