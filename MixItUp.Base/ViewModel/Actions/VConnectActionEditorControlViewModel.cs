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
    public class VConnectActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.VConnect; } }

        public bool VConnectConnected { get { return ServiceManager.Get<VConnectService>().IsConnected; } }
        public bool VConnectNotConnected { get { return !this.VConnectConnected; } }

        public IEnumerable<VConnectActionTypeEnum> ActionTypes { get { return EnumHelper.GetEnumList<VConnectActionTypeEnum>(); } }

        public VConnectActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.ShowTriggerGrid));
                this.NotifyPropertyChanged(nameof(this.ShowCustomMessageGrid));
                this.NotifyPropertyChanged(nameof(this.ShowAssetGrid));
            }
        }
        private VConnectActionTypeEnum selectedActionType;

        public bool ShowTriggerGrid { get { return this.SelectedActionType == VConnectActionTypeEnum.ActivateTrigger; } }

        public ObservableCollection<VConnectTrigger> Triggers { get; set; } = new ObservableCollection<VConnectTrigger>();

        public VConnectTrigger SelectedTrigger
        {
            get { return this.selectedTrigger; }
            set
            {
                this.selectedTrigger = value;
                this.NotifyPropertyChanged();
            }
        }
        private VConnectTrigger selectedTrigger;

        public bool ShowCustomMessageGrid { get { return this.SelectedActionType == VConnectActionTypeEnum.SendCustomMessage; } }

        public string MessageChannel
        {
            get { return this.messageChannel; }
            set
            {
                this.messageChannel = value;
                this.NotifyPropertyChanged();
            }
        }
        private string messageChannel;

        public string MessageArguments
        {
            get { return this.messageArguments; }
            set
            {
                this.messageArguments = value;
                this.NotifyPropertyChanged();
            }
        }
        private string messageArguments;

        public bool ShowAssetGrid { get { return this.SelectedActionType == VConnectActionTypeEnum.LookupAsset; } }

        public ObservableCollection<VConnectAsset> Assets { get; set; } = new ObservableCollection<VConnectAsset>();

        public VConnectAsset SelectedAsset
        {
            get { return this.selectedAsset; }
            set
            {
                this.selectedAsset = value;
                this.NotifyPropertyChanged();
            }
        }
        private VConnectAsset selectedAsset;

        public string ScreenshotFilePath
        {
            get { return this.screenshotFilePath; }
            set
            {
                this.screenshotFilePath = value;
                this.NotifyPropertyChanged();
            }
        }
        private string screenshotFilePath;

        public bool RefreshCommandEnabled
        {
            get { return this.refreshCommandEnabled; }
            set
            {
                this.refreshCommandEnabled = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool refreshCommandEnabled = true;
        public ICommand RefreshCommand { get; set; }

        // Held separately from the selections so that opening and saving a command with VConnect
        // closed does not wipe the trigger or asset the user already picked.
        private string triggerUid;
        private string triggerName;
        private string assetUid;
        private string assetName;

        public VConnectActionEditorControlViewModel(VConnectActionModel action)
            : base(action)
        {
            this.SelectedActionType = action.ActionType;
            this.triggerUid = action.TriggerUid;
            this.triggerName = action.TriggerName;
            this.MessageChannel = action.MessageChannel;
            this.MessageArguments = action.MessageArguments;
            this.assetUid = action.AssetUid;
            this.assetName = action.AssetName;
            this.ScreenshotFilePath = action.ScreenshotFilePath;
        }

        public VConnectActionEditorControlViewModel() : base() { }

        public override Task<Result> Validate()
        {
            if (this.ShowTriggerGrid)
            {
                if (this.SelectedTrigger == null && (this.VConnectConnected || string.IsNullOrEmpty(this.triggerUid)))
                {
                    return Task.FromResult<Result>(new Result(Resources.VConnectActionMissingTrigger));
                }
            }
            else if (this.ShowCustomMessageGrid)
            {
                if (string.IsNullOrWhiteSpace(this.MessageChannel))
                {
                    return Task.FromResult<Result>(new Result(Resources.VConnectActionMissingChannel));
                }
            }
            else if (this.ShowAssetGrid)
            {
                if (this.SelectedAsset == null && (this.VConnectConnected || string.IsNullOrEmpty(this.assetUid)))
                {
                    return Task.FromResult<Result>(new Result(Resources.VConnectActionMissingAsset));
                }
            }
            return Task.FromResult<Result>(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            if (this.ShowTriggerGrid)
            {
                return Task.FromResult<ActionModelBase>(VConnectActionModel.CreateForTrigger(
                    this.SelectedTrigger?.uid ?? this.triggerUid, this.SelectedTrigger?.DisplayName ?? this.triggerName));
            }
            else if (this.ShowCustomMessageGrid)
            {
                return Task.FromResult<ActionModelBase>(VConnectActionModel.CreateForCustomMessage(this.MessageChannel, this.MessageArguments));
            }
            else if (this.ShowAssetGrid)
            {
                return Task.FromResult<ActionModelBase>(VConnectActionModel.CreateForAssetLookup(
                    this.SelectedAsset?.uid ?? this.assetUid, this.SelectedAsset?.DisplayName ?? this.assetName, this.ScreenshotFilePath));
            }
            return Task.FromResult<ActionModelBase>(null);
        }

        protected override async Task OnOpenInternal()
        {
            this.RefreshCommand = this.CreateCommand(async () =>
            {
                this.RefreshCommandEnabled = false;

                await this.TryConnectToVConnect();

                await this.LoadData(forceRefresh: true);

                this.RefreshCommandEnabled = true;
            });

            await this.TryConnectToVConnect();

            await this.LoadData(forceRefresh: false);

            await base.OnOpenInternal();
        }

        private async Task<bool> TryConnectToVConnect()
        {
            if (ChannelSession.Settings.VConnectEnabled && !this.VConnectConnected)
            {
                Result result = await ServiceManager.Get<VConnectService>().Connect();
                return result.Success;
            }
            return false;
        }

        private async Task LoadData(bool forceRefresh)
        {
            this.NotifyPropertyChanged(nameof(this.VConnectConnected));
            this.NotifyPropertyChanged(nameof(this.VConnectNotConnected));

            if (this.VConnectConnected)
            {
                this.Triggers.ClearAndAddRange(await ServiceManager.Get<VConnectService>().GetTriggers(forceRefresh));
                this.SelectedTrigger = this.Triggers.FirstOrDefault(t => string.Equals(t.uid, this.triggerUid));

                this.Assets.ClearAndAddRange(await ServiceManager.Get<VConnectService>().GetAssets(forceRefresh));
                this.SelectedAsset = this.Assets.FirstOrDefault(a => string.Equals(a.uid, this.assetUid));
            }
        }
    }
}
