using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.VPZone.ChannelPoints;
using MixItUp.Base.Services;
using MixItUp.Base.Services.VPZone.New;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Commands
{
    public class VPZoneChannelPointsCommandEditorWindowViewModel : CommandEditorWindowViewModelBase
    {
        public ObservableCollection<VPZoneChannelPointRewardModel> ChannelPointRewards { get; set; } = new ObservableCollection<VPZoneChannelPointRewardModel>();

        public VPZoneChannelPointRewardModel ChannelPointReward
        {
            get { return this.channelPointReward; }
            set
            {
                this.channelPointReward = value;
                this.NotifyPropertyChanged();

                this.Name = (this.channelPointReward != null) ? this.channelPointReward.Name : string.Empty;
            }
        }
        private VPZoneChannelPointRewardModel channelPointReward;

        private string existingChannelPointRewardID = string.Empty;

        public VPZoneChannelPointsCommandEditorWindowViewModel(VPZoneChannelPointsCommandModel existingCommand)
            : base(existingCommand)
        {
            this.existingChannelPointRewardID = existingCommand.ChannelPointRewardID;
        }

        public VPZoneChannelPointsCommandEditorWindowViewModel() : base(CommandTypeEnum.VPZoneChannelPoints) { }

        public override Task<Result> Validate()
        {
            if (this.ChannelPointReward == null)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ChannelPointRewardMissing));
            }

            return Task.FromResult(new Result());
        }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return VPZoneChannelPointsCommandModel.GetChannelPointTestSpecialIdentifiers(); }

        public override Task<CommandModelBase> CreateNewCommand() { return Task.FromResult<CommandModelBase>(new VPZoneChannelPointsCommandModel(this.ChannelPointReward.Name, this.ChannelPointReward.ID)); }

        public override async Task UpdateExistingCommand(CommandModelBase command)
        {
            await base.UpdateExistingCommand(command);
            ((VPZoneChannelPointsCommandModel)command).ChannelPointRewardID = this.ChannelPointReward.ID;
        }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            ServiceManager.Get<CommandService>().VPZoneChannelPointsCommands.Remove((VPZoneChannelPointsCommandModel)this.existingCommand);
            ServiceManager.Get<CommandService>().VPZoneChannelPointsCommands.Add((VPZoneChannelPointsCommandModel)command);
            return Task.CompletedTask;
        }

        protected override async Task OnOpenInternal()
        {
            if (ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                IEnumerable<VPZoneChannelPointRewardModel> rewards = await ServiceManager.Get<VPZoneSession>().StreamerService.GetChannelPointRewards(ServiceManager.Get<VPZoneSession>().ChannelSlug);
                if (rewards != null && rewards.Count() > 0)
                {
                    foreach (VPZoneChannelPointRewardModel channelPoint in rewards.OrderBy(c => c.Name))
                    {
                        this.ChannelPointRewards.Add(channelPoint);
                    }

                    if (!string.IsNullOrEmpty(this.existingChannelPointRewardID))
                    {
                        this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => string.Equals(c.ID, this.existingChannelPointRewardID));
                    }
                    else if (!string.IsNullOrEmpty(this.Name))
                    {
                        this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => string.Equals(c.Name, this.Name));
                    }
                }
            }
            await base.OnOpenInternal();
        }
    }
}
