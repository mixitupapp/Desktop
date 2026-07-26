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
    public class VeadotubeActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Veadotube; } }

        public bool VeadotubeConnected { get { return ServiceManager.Get<VeadotubeService>().IsConnected; } }
        public bool VeadotubeNotConnected { get { return !this.VeadotubeConnected; } }

        public IEnumerable<VeadotubeActionTypeEnum> ActionTypes { get { return EnumHelper.GetEnumList<VeadotubeActionTypeEnum>(); } }

        public VeadotubeActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowAvatarStateGrid");
                this.NotifyPropertyChanged("ShowPushToTalkGrid");
            }
        }
        private VeadotubeActionTypeEnum selectedActionType;

        /// <summary>
        /// Everything except the random switch and push-to-talk needs a state picked.
        /// </summary>
        public bool ShowAvatarStateGrid
        {
            get
            {
                return this.SelectedActionType == VeadotubeActionTypeEnum.SetAvatarState ||
                    this.SelectedActionType == VeadotubeActionTypeEnum.PushAvatarState ||
                    this.SelectedActionType == VeadotubeActionTypeEnum.PopAvatarState ||
                    this.SelectedActionType == VeadotubeActionTypeEnum.ToggleAvatarState;
            }
        }

        public bool ShowPushToTalkGrid { get { return this.SelectedActionType == VeadotubeActionTypeEnum.SetPushToTalk; } }

        public ObservableCollection<VeadotubeState> States { get; set; } = new ObservableCollection<VeadotubeState>();

        public VeadotubeState SelectedState
        {
            get { return this.selectedState; }
            set
            {
                this.selectedState = value;
                this.NotifyPropertyChanged();
            }
        }
        private VeadotubeState selectedState;

        public IEnumerable<VeadotubePushToTalkTypeEnum> PushToTalkTypes { get { return EnumHelper.GetEnumList<VeadotubePushToTalkTypeEnum>(); } }

        public VeadotubePushToTalkTypeEnum SelectedPushToTalkType
        {
            get { return this.selectedPushToTalkType; }
            set
            {
                this.selectedPushToTalkType = value;
                this.NotifyPropertyChanged();
            }
        }
        private VeadotubePushToTalkTypeEnum selectedPushToTalkType;

        public ICommand RefreshCacheCommand { get; set; }

        // Held separately from SelectedState so that opening and saving a command with veadotube
        // closed does not wipe the state the user already picked.
        private string stateID;
        private string stateName;

        public VeadotubeActionEditorControlViewModel(VeadotubeActionModel action)
            : base(action)
        {
            this.SelectedActionType = action.ActionType;
            this.stateID = action.StateID;
            this.stateName = action.StateName;
            this.SelectedPushToTalkType = action.PushToTalkType;
        }

        public VeadotubeActionEditorControlViewModel() : base() { }

        public override Task<Result> Validate()
        {
            if (this.ShowAvatarStateGrid)
            {
                if (this.SelectedState == null && (this.VeadotubeConnected || string.IsNullOrEmpty(this.stateID)))
                {
                    return Task.FromResult<Result>(new Result(Resources.VeadotubeActionMissingState));
                }
            }
            return Task.FromResult<Result>(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            if (this.ShowAvatarStateGrid)
            {
                return Task.FromResult<ActionModelBase>(VeadotubeActionModel.CreateForState(this.SelectedActionType,
                    this.SelectedState?.id ?? this.stateID, this.SelectedState?.DisplayName ?? this.stateName));
            }
            else if (this.SelectedActionType == VeadotubeActionTypeEnum.SetRandomAvatarState)
            {
                return Task.FromResult<ActionModelBase>(VeadotubeActionModel.CreateForRandomState());
            }
            else if (this.ShowPushToTalkGrid)
            {
                return Task.FromResult<ActionModelBase>(VeadotubeActionModel.CreateForPushToTalk(this.SelectedPushToTalkType));
            }
            return Task.FromResult<ActionModelBase>(null);
        }

        protected override async Task OnOpenInternal()
        {
            this.RefreshCacheCommand = this.CreateCommand(async () =>
            {
                await this.TryConnectToVeadotube();

                if (this.VeadotubeConnected)
                {
                    // Renaming a state in veadotube does not notify anyone, so the refresh has to be
                    // something the user can ask for.
                    ServiceManager.Get<VeadotubeService>().ClearCaches();
                }

                await this.LoadData();
            });

            await this.TryConnectToVeadotube();

            await this.LoadData();

            await base.OnOpenInternal();
        }

        private async Task<bool> TryConnectToVeadotube()
        {
            if (ChannelSession.Settings.VeadotubeEnabled && !this.VeadotubeConnected)
            {
                Result result = await ServiceManager.Get<VeadotubeService>().Connect();
                return result.Success;
            }
            return false;
        }

        private async Task LoadData()
        {
            this.NotifyPropertyChanged("VeadotubeConnected");
            this.NotifyPropertyChanged("VeadotubeNotConnected");

            if (this.VeadotubeConnected)
            {
                this.States.Clear();
                foreach (VeadotubeState state in await ServiceManager.Get<VeadotubeService>().GetStates())
                {
                    this.States.Add(state);
                }
                this.SelectedState = this.States.FirstOrDefault(s => string.Equals(s.id, this.stateID));
            }
        }
    }
}
