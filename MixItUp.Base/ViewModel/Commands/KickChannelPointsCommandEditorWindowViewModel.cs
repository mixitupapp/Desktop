using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Kick.ChannelRewards;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Commands
{
    public class KickChannelPointsCommandEditorWindowViewModel : CommandEditorWindowViewModelBase
    {
        public ObservableCollection<ChannelRewardModel> ChannelPointRewards { get; set; } = new ObservableCollection<ChannelRewardModel>();

        public ChannelRewardModel ChannelPointReward
        {
            get { return this.channelPointReward; }
            set
            {
                this.channelPointReward = value;
                this.NotifyPropertyChanged();

                this.Name = (this.channelPointReward != null) ? this.channelPointReward.Title : string.Empty;
            }
        }
        private ChannelRewardModel channelPointReward;

        private string existingChannelPointRewardID = string.Empty;

        public KickChannelPointsCommandEditorWindowViewModel(KickChannelPointsCommandModel existingCommand)
            : base(existingCommand)
        {
            this.existingChannelPointRewardID = existingCommand.ChannelPointRewardID;
        }

        public KickChannelPointsCommandEditorWindowViewModel() : base(CommandTypeEnum.KickChannelPoints) { }

        public override Task<Result> Validate()
        {
            if (this.ChannelPointReward == null)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ChannelPointRewardMissing));
            }

            return Task.FromResult(new Result());
        }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return KickChannelPointsCommandModel.GetChannelPointTestSpecialIdentifiers(); }

        public override Task<CommandModelBase> CreateNewCommand() { return Task.FromResult<CommandModelBase>(new KickChannelPointsCommandModel(this.ChannelPointReward.Title, this.ChannelPointReward.ID)); }

        public override async Task UpdateExistingCommand(CommandModelBase command)
        {
            await base.UpdateExistingCommand(command);
            ((KickChannelPointsCommandModel)command).ChannelPointRewardID = this.ChannelPointReward.ID;
        }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            ServiceManager.Get<CommandService>().KickChannelPointsCommands.Remove((KickChannelPointsCommandModel)this.existingCommand);
            ServiceManager.Get<CommandService>().KickChannelPointsCommands.Add((KickChannelPointsCommandModel)command);
            return Task.CompletedTask;
        }

        protected override async Task OnOpenInternal()
        {
            if (ServiceManager.Get<KickSession>().IsConnected)
            {
                IEnumerable<ChannelRewardModel> rewards = await ServiceManager.Get<KickSession>().StreamerService.GetChannelRewards();
                if (rewards != null && rewards.Count() > 0)
                {
                    foreach (ChannelRewardModel channelPoint in rewards.OrderBy(c => c.Title))
                    {
                        this.ChannelPointRewards.Add(channelPoint);
                    }

                    if (!string.IsNullOrEmpty(this.existingChannelPointRewardID))
                    {
                        this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => string.Equals(c.ID, this.existingChannelPointRewardID));
                    }
                    else if (!string.IsNullOrEmpty(this.Name))
                    {
                        this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => string.Equals(c.Title, this.Name));
                    }
                }
            }
            await base.OnOpenInternal();
        }
    }
}
