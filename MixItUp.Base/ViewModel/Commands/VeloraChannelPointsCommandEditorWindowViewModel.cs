using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Velora.ChannelPoints;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Commands
{
    public class VeloraChannelPointsCommandEditorWindowViewModel : CommandEditorWindowViewModelBase
    {
        public ObservableCollection<ChannelPointRewardModel> ChannelPointRewards { get; set; } = new ObservableCollection<ChannelPointRewardModel>();

        public ChannelPointRewardModel ChannelPointReward
        {
            get { return this.channelPointReward; }
            set
            {
                this.channelPointReward = value;
                this.NotifyPropertyChanged();

                this.Name = (this.channelPointReward != null) ? this.channelPointReward.Name : string.Empty;
            }
        }
        private ChannelPointRewardModel channelPointReward;

        private string existingChannelPointRewardID = string.Empty;

        public VeloraChannelPointsCommandEditorWindowViewModel(VeloraChannelPointsCommandModel existingCommand)
            : base(existingCommand)
        {
            this.existingChannelPointRewardID = existingCommand.ChannelPointRewardID;
        }

        public VeloraChannelPointsCommandEditorWindowViewModel() : base(CommandTypeEnum.VeloraChannelPoints) { }

        public override Task<Result> Validate()
        {
            if (this.ChannelPointReward == null)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ChannelPointRewardMissing));
            }

            return Task.FromResult(new Result());
        }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return VeloraChannelPointsCommandModel.GetChannelPointTestSpecialIdentifiers(); }

        public override Task<CommandModelBase> CreateNewCommand() { return Task.FromResult<CommandModelBase>(new VeloraChannelPointsCommandModel(this.ChannelPointReward.Name, this.ChannelPointReward.ID)); }

        public override async Task UpdateExistingCommand(CommandModelBase command)
        {
            await base.UpdateExistingCommand(command);
            ((VeloraChannelPointsCommandModel)command).ChannelPointRewardID = this.ChannelPointReward.ID;
        }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            ServiceManager.Get<CommandService>().VeloraChannelPointsCommands.Remove((VeloraChannelPointsCommandModel)this.existingCommand);
            ServiceManager.Get<CommandService>().VeloraChannelPointsCommands.Add((VeloraChannelPointsCommandModel)command);
            return Task.CompletedTask;
        }

        protected override async Task OnOpenInternal()
        {
            if (ServiceManager.Get<VeloraSession>().IsConnected)
            {
                IEnumerable<ChannelPointRewardModel> rewards = await ServiceManager.Get<VeloraSession>().StreamerService.GetChannelPointRewards(ServiceManager.Get<VeloraSession>().ChannelID);
                if (rewards != null && rewards.Count() > 0)
                {
                    foreach (ChannelPointRewardModel channelPoint in rewards.OrderBy(c => c.Name))
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
