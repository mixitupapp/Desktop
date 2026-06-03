using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class EventCommandGroupViewModel
    {
        public string Name { get; set; }

        public string Image { get; set; }

        public string PackIconName { get; set; }

        public ObservableCollection<EventCommandItemViewModel> Commands { get; set; } = new ObservableCollection<EventCommandItemViewModel>();

        public bool ShowImage { get { return !string.IsNullOrEmpty(this.Image); } }

        public bool ShowPackIcon { get { return !this.ShowImage; } }

        public EventCommandGroupViewModel(string name, string image = null, string packIconName = null)
        {
            this.Name = name;
            this.Image = image;
            this.PackIconName = packIconName;
        }
    }

    public class EventCommandItemViewModel : UIViewModelBase
    {
        public EventTypeEnum EventType { get; set; }

        public EventCommandModel Command { get; set; }

        public EventCommandItemViewModel(EventTypeEnum eventType)
        {
            this.EventType = eventType;
            this.RefreshCommand();
        }

        public string Name { get { return EnumLocalizationHelper.GetLocalizedName(this.EventType); } }

        public string Service
        {
            get
            {
                int eventNumber = (int)this.EventType;
                if (this.EventType == EventTypeEnum.StreamlabsDonation)
                {
                    return Resources.Streamlabs;
                }
                else if (this.EventType == EventTypeEnum.TiltifyDonation)
                {
                    return Resources.Tiltify;
                }
                else if (this.EventType == EventTypeEnum.DonorDriveDonation || this.EventType == EventTypeEnum.DonorDriveDonationIncentive || this.EventType == EventTypeEnum.DonorDriveDonationMilestone ||
                    this.EventType == EventTypeEnum.DonorDriveDonationTeamIncentive || this.EventType == EventTypeEnum.DonorDriveDonationTeamMilestone)
                {
                    return Resources.DonorDrive;
                }
                else if (this.EventType == EventTypeEnum.TipeeeStreamDonation)
                {
                    return Resources.TipeeeStream;
                }
                else if (this.EventType == EventTypeEnum.TreatStreamDonation)
                {
                    return Resources.TreatStream;
                }
                else if (this.EventType == EventTypeEnum.RainmakerDonation)
                {
                    return Resources.Rainmaker;
                }
                else if (this.EventType == EventTypeEnum.PatreonSubscribed)
                {
                    return Resources.Patreon;
                }
                else if (this.EventType == EventTypeEnum.JustGivingDonation)
                {
                    return Resources.JustGiving;
                }
                else if (this.EventType == EventTypeEnum.StreamlootsCardRedeemed || this.EventType == EventTypeEnum.StreamlootsPackGifted || this.EventType == EventTypeEnum.StreamlootsPackPurchased)
                {
                    return Resources.Streamloots;
                }
                else if (this.EventType == EventTypeEnum.StreamElementsDonation)
                {
                    return Resources.StreamElements;
                }
                else if (eventNumber >= 200 && eventNumber < 300)
                {
                    return Resources.Twitch;
                }
                else if (eventNumber >= 300 && eventNumber < 400)
                {
                    return Resources.YouTube;
                }
                else if (eventNumber >= 600 && eventNumber < 700)
                {
                    return Resources.Kick;
                }
                else
                {
                    return Resources.Generic;
                }
            }
        }

        public bool IsNewCommand { get { return this.Command == null; } }

        public bool IsExistingCommand { get { return this.Command != null; } }

        public void RefreshCommand()
        {
            this.Command = ServiceManager.Get<EventService>().GetEventCommand(this.EventType);
            this.NotifyPropertyChanged("Command");
            this.NotifyPropertyChanged("Name");
            this.NotifyPropertyChanged("Service");
            this.NotifyPropertyChanged("IsNewCommand");
            this.NotifyPropertyChanged("IsExistingCommand");
        }
    }

    public class EventsMainControlViewModel : WindowControlViewModelBase
    {
        public ObservableCollection<EventCommandGroupViewModel> EventCommandGroups { get; set; } = new ObservableCollection<EventCommandGroupViewModel>();

        public Dictionary<EventTypeEnum, EventCommandItemViewModel> EventTypeItems { get; set; } = new Dictionary<EventTypeEnum, EventCommandItemViewModel>();

        public EventsMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
            this.EventCommandGroups.Clear();

            List<EventCommandGroupViewModel> commandGroups = new List<EventCommandGroupViewModel>();

            EventCommandGroupViewModel genericCommands = new EventCommandGroupViewModel(Resources.Generic, packIconName: "AlarmLight");
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ApplicationLaunch));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ApplicationExit));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelStreamStart));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelStreamStop));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelFollowed));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelHosted));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelRaided));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelSubscribed));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelResubscribed));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelSubscriptionGifted));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelMassSubscriptionsGifted));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.GenericDonation));
            commandGroups.Add(genericCommands);

            EventCommandGroupViewModel twitchCommands = new EventCommandGroupViewModel(Resources.Twitch, image: StreamingPlatforms.TwitchLogoImageAssetFilePath);
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelStreamStart));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelStreamStop));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelUpdated));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelFollowed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelRaided));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelOutgoingRaidCompleted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSubscribed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelResubscribed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSubscriptionGifted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelMassSubscriptionsGifted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelWatchStreak));
            if (MixItUpService.Options.TwitchModiversaryEnabled)
            {
                twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelModiversary));
            }
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHighlightedMessage));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelUserIntro));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelPowerUpMessageEffect));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelPowerUpGigantifiedEmote));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelPowerUpCelebration));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelBitsCheered));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelPointsRedeemed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelCustomPowerUpRedeemed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelCharityDonation));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelAdUpcoming));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelAdStarted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelAdEnded));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHypeTrainBegin));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHypeTrainLevelUp));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHypeTrainEnd));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelUserWarned));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelShoutoutReceived));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSuspiciousUserMessage));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSuspiciousUserUpdated));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelShieldModeStarted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelShieldModeEnded));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelGoalStarted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelGoalEnded));
            commandGroups.Add(twitchCommands);

            EventCommandGroupViewModel youtubeCommands = new EventCommandGroupViewModel(Resources.YouTube, image: StreamingPlatforms.YouTubeLogoImageAssetFilePath);
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelStreamStart));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelStreamStop));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelNewMember));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelMemberMilestone));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelMembershipGifted));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelMassMembershipGifted));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelSuperChat));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelJewelsGift));
            commandGroups.Add(youtubeCommands);

            EventCommandGroupViewModel kickCommands = new EventCommandGroupViewModel(Resources.Kick, image: StreamingPlatforms.KickLogoImageAssetFilePath);
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelStreamStart));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelStreamStop));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelUpdated));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelFollowed));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelSubscribed));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelResubscribed));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelSubscriptionGifted));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelMassSubscriptionsGifted));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelPointsRedeemed));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelKicksGifted));
            commandGroups.Add(kickCommands);

            EventCommandGroupViewModel chatCommands = new EventCommandGroupViewModel(Resources.Chat, packIconName: "Chat");
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserEntranceCommand));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserFirstMessage));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatMessageReceived));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatWhisperReceived));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatMessageDeleted));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserTimeout));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserBan));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserFirstJoin));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserJoined));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserLeft));
            commandGroups.Add(chatCommands);

            EventCommandGroupViewModel donationCommands = new EventCommandGroupViewModel(Resources.Donations, packIconName: "Cash");
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonationIncentive));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonationMilestone));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonationTeamIncentive));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonationTeamMilestone));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlabsDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamElementsDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TipeeeStreamDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TreatStreamDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.RainmakerDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TiltifyDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.JustGivingDonation));
            commandGroups.Add(donationCommands);

            EventCommandGroupViewModel streamlootsCommands = new EventCommandGroupViewModel(Resources.Streamloots, packIconName: "CardsOutline");
            streamlootsCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlootsCardRedeemed));
            streamlootsCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlootsPackPurchased));
            streamlootsCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlootsPackGifted));
            commandGroups.Add(streamlootsCommands);

            EventCommandGroupViewModel crowdControlCommands = new EventCommandGroupViewModel(Resources.CrowdControl, packIconName: "ControllerClassic");
            crowdControlCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.CrowdControlEffectRedeemed));
            commandGroups.Add(crowdControlCommands);

            EventCommandGroupViewModel pulsoidCommands = new EventCommandGroupViewModel(Resources.Pulsoid, packIconName: "HeartPulse");
            pulsoidCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.PulsoidHeartRateChanged));
            commandGroups.Add(pulsoidCommands);

            EventCommandGroupViewModel patreonCommands = new EventCommandGroupViewModel(Resources.Patreon, packIconName: "Patreon");
            patreonCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.PatreonSubscribed));
            commandGroups.Add(patreonCommands);

            this.EventCommandGroups.AddRange(commandGroups);

            foreach (EventCommandGroupViewModel group in this.EventCommandGroups)
            {
                foreach (EventCommandItemViewModel item in group.Commands)
                {
                    this.EventTypeItems[item.EventType] = item;
                }
            }
        }
    }
}
