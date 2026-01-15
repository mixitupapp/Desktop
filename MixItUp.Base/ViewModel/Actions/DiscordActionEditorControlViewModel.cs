using MixItUp.Base.Model.Actions;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Actions
{
    public class DiscordActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Discord; } }

        public IEnumerable<DiscordActionTypeEnum> ActionTypes { get { return EnumHelper.GetEnumList<DiscordActionTypeEnum>(); } }

        public DiscordActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowSendMessageGrid");
                this.NotifyPropertyChanged("ShowMuteGrid");
                this.NotifyPropertyChanged("ShowDeafenGrid");
            }
        }
        private DiscordActionTypeEnum selectedActionType;

        public bool ShowSendMessageGrid { get { return this.SelectedActionType == DiscordActionTypeEnum.SendMessage; } }

        public ObservableCollection<DiscordChannel> Channels { get; set; } = new ObservableCollection<DiscordChannel>();

        public DiscordChannel SelectedChannel
        {
            get { return this.selectedChannel; }
            set
            {
                this.selectedChannel = value;
                this.NotifyPropertyChanged();
            }
        }
        private DiscordChannel selectedChannel;

        public string ChatMessage
        {
            get { return this.chatMessage; }
            set
            {
                this.chatMessage = value;
                this.NotifyPropertyChanged();
            }
        }
        private string chatMessage;

        public string UploadFilePath
        {
            get { return this.uploadFilePath; }
            set
            {
                this.uploadFilePath = value;
                this.NotifyPropertyChanged();
            }
        }
        private string uploadFilePath;

        public bool ShowMuteGrid { get { return this.SelectedActionType == DiscordActionTypeEnum.MuteSelf; } }

        public bool MuteSelf
        {
            get { return this.muteSelf; }
            set
            {
                this.muteSelf = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool muteSelf;

        public bool ShowDeafenGrid { get { return this.SelectedActionType == DiscordActionTypeEnum.DeafenSelf; } }

        public bool DeafenSelf
        {
            get { return this.deafenSelf; }
            set
            {
                this.deafenSelf = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool deafenSelf;

        public ICommand RefreshChannelsCommand { get; set; }

        private string existingSelectedChannel;

        public DiscordActionEditorControlViewModel(DiscordActionModel action)
            : base(action)
        {
            this.SelectedActionType = action.ActionType;
            if (this.SelectedActionType == DiscordActionTypeEnum.SendMessage)
            {
                this.existingSelectedChannel = action.ChannelID;
                this.ChatMessage = action.MessageText;
                this.UploadFilePath = action.FilePath;
            }
            else if (this.SelectedActionType == DiscordActionTypeEnum.MuteSelf)
            {
                this.MuteSelf = action.ShouldMuteDeafen;
            }
            else if (this.SelectedActionType == DiscordActionTypeEnum.DeafenSelf)
            {
                this.DeafenSelf = action.ShouldMuteDeafen;
            }

            this.InitializeRefreshCommand();
        }

        public DiscordActionEditorControlViewModel() : base()
        {
            this.InitializeRefreshCommand();
        }

        private void InitializeRefreshCommand()
        {
            this.RefreshChannelsCommand = this.CreateCommand(async () =>
            {
                if (ServiceManager.Get<DiscordService>().IsConnected)
                {
                    var currentSelectedChannelID = this.SelectedChannel?.ID ?? this.existingSelectedChannel;

                    var channels = await ServiceManager.Get<DiscordService>().RefreshCachedServerChannels(ServiceManager.Get<DiscordService>().Server);

                    this.Channels.Clear();
                    this.Channels.AddRange(channels.Where(c => c.Type == DiscordChannel.DiscordChannelTypeEnum.Announcements || c.Type == DiscordChannel.DiscordChannelTypeEnum.Text));

                    if (!string.IsNullOrEmpty(currentSelectedChannelID))
                    {
                        this.SelectedChannel = this.Channels.FirstOrDefault(c => c.ID.Equals(currentSelectedChannelID));
                    }
                }
            });
        }

        public override Task<Result> Validate()
        {
            if (this.ShowSendMessageGrid)
            {
                if (this.SelectedChannel == null)
                {
                    return Task.FromResult<Result>(new Result(MixItUp.Base.Resources.DiscordActionMissingChannel));
                }

                if (string.IsNullOrEmpty(this.ChatMessage))
                {
                    return Task.FromResult<Result>(new Result(MixItUp.Base.Resources.DiscordActionMissingChatMessage));
                }
            }

            return Task.FromResult<Result>(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            if (this.ShowSendMessageGrid)
            {
                return Task.FromResult<ActionModelBase>(DiscordActionModel.CreateForChatMessage(this.SelectedChannel, this.ChatMessage, this.UploadFilePath));
            }
            else if (this.ShowMuteGrid)
            {
                return Task.FromResult<ActionModelBase>(DiscordActionModel.CreateForMuteSelf(this.MuteSelf));
            }
            else if (this.ShowDeafenGrid)
            {
                return Task.FromResult<ActionModelBase>(DiscordActionModel.CreateForDeafenSelf(this.DeafenSelf));
            }
            return Task.FromResult<ActionModelBase>(null);
        }

        protected override async Task OnOpenInternal()
        {
            if (ServiceManager.Get<DiscordService>().IsConnected)
            {
                List<DiscordChannel> channels = new List<DiscordChannel>(await ServiceManager.Get<DiscordService>().GetCachedServerChannels(ServiceManager.Get<DiscordService>().Server));
                this.Channels.AddRange(channels.Where(c => c.Type == DiscordChannel.DiscordChannelTypeEnum.Announcements || c.Type == DiscordChannel.DiscordChannelTypeEnum.Text));

                if (!string.IsNullOrEmpty(this.existingSelectedChannel))
                {
                    this.SelectedChannel = this.Channels.FirstOrDefault(c => c.ID.Equals(this.existingSelectedChannel));

                    // Fallback: if we couldn't find the channel in the list but have a saved ID, create a placeholder
                    // This prevents losing the saved channel if the API fails
                    // DISABLED FOR NOW, ENABLE LATER IF NEEDED OR IF USERS STILL HAVING ISSUES EVEN AFTER ADDING CACHING
                    //if (this.SelectedChannel == null)
                    //{
                    //    var placeholderChannel = new DiscordChannel { ID = this.existingSelectedChannel, Name = this.existingSelectedChannel };
                    //    this.Channels.Add(placeholderChannel);
                    //    this.SelectedChannel = placeholderChannel;
                    //}
                }
            }
            await base.OnOpenInternal();
        }
    }
}
