using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Kick.ChannelRewards;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Kick;
using MixItUp.Base.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class KickActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Kick; } }

        public IEnumerable<KickActionType> ActionTypes { get { return EnumHelper.GetEnumList<KickActionType>(); } }

        public KickActionType SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.ShowTextGrid));
                this.NotifyPropertyChanged(nameof(this.ShowSetCustomTagsGrid));
                this.NotifyPropertyChanged(nameof(this.ShowUpdateChannelPointRewardGrid));
            }
        }
        private KickActionType selectedActionType;

        public bool ShowTextGrid { get { return this.SelectedActionType == KickActionType.SetTitle || this.SelectedActionType == KickActionType.SetGame; } }

        public string Text
        {
            get { return this.text; }
            set
            {
                this.text = value;
                this.NotifyPropertyChanged();
            }
        }
        private string text;

        public bool ShowSetCustomTagsGrid { get { return this.SelectedActionType == KickActionType.SetCustomTags; } }

        public KickTagEditorViewModel TagEditor { get; set; } = new KickTagEditorViewModel();

        public bool ShowUpdateChannelPointRewardGrid { get { return this.SelectedActionType == KickActionType.UpdateChannelPointReward; } }

        public ObservableCollection<ChannelRewardModel> ChannelPointRewards { get; set; } = new ObservableCollection<ChannelRewardModel>();

        public ChannelRewardModel ChannelPointReward
        {
            get { return this.channelPointReward; }
            set
            {
                this.channelPointReward = value;
                this.NotifyPropertyChanged();

                if (this.existingChannelPointRewardID == null)
                {
                    this.ChannelPointRewardState = this.ChannelPointReward.IsEnabled;
                    this.ChannelPointRewardPaused = this.ChannelPointReward.IsPaused;
                    this.ChannelPointRewardName = this.ChannelPointReward.Title;
                    this.ChannelPointRewardDescription = this.ChannelPointReward.Description;
                    this.ChannelPointRewardBackgroundColor = this.ChannelPointReward.BackgroundColor;
                    this.ChannelPointRewardCost = this.ChannelPointReward.Cost.ToString();
                }
                this.existingChannelPointRewardID = null;
            }
        }
        private ChannelRewardModel channelPointReward;

        public bool ChannelPointRewardState
        {
            get { return this.channelPointRewardState; }
            set
            {
                this.channelPointRewardState = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool channelPointRewardState;

        public bool ChannelPointRewardPaused
        {
            get { return this.channelPointRewardPaused; }
            set
            {
                this.channelPointRewardPaused = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool channelPointRewardPaused;

        public string ChannelPointRewardName
        {
            get { return this.channelPointRewardName; }
            set
            {
                this.channelPointRewardName = value;
                this.NotifyPropertyChanged();
            }
        }
        private string channelPointRewardName;

        public string ChannelPointRewardDescription
        {
            get { return this.channelPointRewardDescription; }
            set
            {
                this.channelPointRewardDescription = value;
                this.NotifyPropertyChanged();
            }
        }
        private string channelPointRewardDescription;

        public string ChannelPointRewardBackgroundColor
        {
            get { return this.channelPointRewardBackgroundColor; }
            set
            {
                this.channelPointRewardBackgroundColor = value;
                this.NotifyPropertyChanged();
            }
        }
        private string channelPointRewardBackgroundColor;

        public string ChannelPointRewardCost
        {
            get { return this.channelPointRewardCost; }
            set
            {
                this.channelPointRewardCost = value;
                this.NotifyPropertyChanged();
            }
        }
        private string channelPointRewardCost;

        private string existingChannelPointRewardID;
        private IEnumerable<string> existingTags = null;

        public KickActionEditorControlViewModel(KickActionModel action)
            : base(action)
        {
            this.InitializeCommands();

            this.SelectedActionType = action.ActionType;

            if (this.ShowTextGrid)
            {
                this.Text = action.Text;
            }
            else if (this.ShowSetCustomTagsGrid)
            {
                this.existingTags = action.CustomTags;
            }
            else if (this.ShowUpdateChannelPointRewardGrid)
            {
                this.existingChannelPointRewardID = action.ChannelPointRewardID;
                this.ChannelPointRewardState = action.ChannelPointRewardState;
                this.ChannelPointRewardPaused = action.ChannelPointRewardPaused;
                this.ChannelPointRewardName = action.ChannelPointRewardName;
                this.ChannelPointRewardDescription = action.ChannelPointRewardDescription;
                this.ChannelPointRewardBackgroundColor = action.ChannelPointRewardBackgroundColor;
                this.ChannelPointRewardCost = action.ChannelPointRewardCostString;
            }
        }

        public KickActionEditorControlViewModel() : base()
        {
            this.InitializeCommands();
        }

        public override async Task<Result> Validate()
        {
            if (this.ShowTextGrid)
            {
                if (string.IsNullOrEmpty(this.Text))
                {
                    return new Result(MixItUp.Base.Resources.KickActionNameMissing);
                }
            }
            else if (this.ShowUpdateChannelPointRewardGrid)
            {
                if (this.ChannelPointReward == null)
                {
                    return new Result(MixItUp.Base.Resources.KickActionChannelPointRewardMissing);
                }
            }
            return await base.Validate();
        }

        protected override async Task OnOpenInternal()
        {
            await this.TagEditor.OnOpen();

            if (ServiceManager.Get<KickSession>().IsConnected)
            {
                IEnumerable<ChannelRewardModel> rewards = await ServiceManager.Get<KickSession>().StreamerService.GetChannelRewards();
                if (rewards != null && rewards.Count() > 0)
                {
                    foreach (ChannelRewardModel reward in rewards.OrderBy(c => c.Title))
                    {
                        this.ChannelPointRewards.Add(reward);
                    }

                    if (this.ShowUpdateChannelPointRewardGrid && !string.IsNullOrEmpty(this.existingChannelPointRewardID))
                    {
                        this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => string.Equals(c.ID, this.existingChannelPointRewardID, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (this.existingTags != null)
                {
                    foreach (string tag in this.existingTags)
                    {
                        await this.TagEditor.AddCustomTag(tag);
                    }
                }
                else if (this.ShowSetCustomTagsGrid)
                {
                    await this.TagEditor.LoadCurrentTags();
                }
            }
            await base.OnOpenInternal();
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            if (this.ShowTextGrid)
            {
                return Task.FromResult<ActionModelBase>(KickActionModel.CreateTextAction(this.SelectedActionType, this.Text));
            }
            else if (this.ShowSetCustomTagsGrid)
            {
                return Task.FromResult<ActionModelBase>(KickActionModel.CreateSetCustomTagsAction(this.TagEditor.CustomTags.Select(t => t.Tag)));
            }
            else if (this.ShowUpdateChannelPointRewardGrid)
            {
                return Task.FromResult<ActionModelBase>(KickActionModel.CreateUpdateChannelPointReward(
                    this.ChannelPointReward.ID, this.ChannelPointRewardName, this.ChannelPointRewardDescription,
                    this.ChannelPointRewardState, this.ChannelPointRewardPaused,
                    this.ChannelPointRewardBackgroundColor, this.ChannelPointRewardCost));
            }
            else
            {
                return Task.FromResult<ActionModelBase>(KickActionModel.CreateAction(this.SelectedActionType));
            }
        }

        private void InitializeCommands()
        {
        }
    }
}
