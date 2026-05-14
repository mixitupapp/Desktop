using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Kick.ChannelRewards;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class KickChannelPointsMainControlViewModel : GroupedCommandsMainControlViewModelBase
    {
        public ICommand CreateChannelPointRewardCommand { get; set; }

        public ICommand ChannelPointsEditorCommand { get; set; }

        public KickChannelPointsMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
            GroupedCommandsMainControlViewModelBase.OnCommandAddedEdited += GroupedCommandsMainControlViewModelBase_OnCommandAddedEdited;

            this.CreateChannelPointRewardCommand = this.CreateCommand(async () =>
            {
                if (!ServiceManager.Get<KickSession>().IsConnected)
                {
                    await DialogHelper.ShowMessage(MixItUp.Base.Resources.KickAccountMustBeConnectedToUseThisFeature);
                    return;
                }

                string name = await DialogHelper.ShowTextEntry(MixItUp.Base.Resources.ChannelPointRewardName);
                if (!string.IsNullOrEmpty(name))
                {
                    ChannelRewardModel reward = await ServiceManager.Get<KickSession>().StreamerService.CreateChannelReward(name, 1);

                    if (reward != null)
                    {
                        this.AddCommand(new KickChannelPointsCommandModel(reward.Title, reward.ID));

                        await DialogHelper.ShowMessage(MixItUp.Base.Resources.KickCreateChannelPointRewardSuccess);
                    }
                    else
                    {
                        await DialogHelper.ShowMessage(string.Format(MixItUp.Base.Resources.CreateChannelPointRewardFailure, string.Empty));
                    }
                }
            });

            this.ChannelPointsEditorCommand = this.CreateCommand(() =>
            {
                if (ServiceManager.Get<KickSession>().IsConnected)
                {
                    ServiceManager.Get<IProcessService>().LaunchLink("https://dashboard.kick.com/community/chat/channel-points");
                }
            });
        }

        protected override IEnumerable<CommandModelBase> GetCommands()
        {
            return ServiceManager.Get<CommandService>().KickChannelPointsCommands.ToList();
        }

        private void GroupedCommandsMainControlViewModelBase_OnCommandAddedEdited(object sender, CommandModelBase command)
        {
            if (command.Type == CommandTypeEnum.KickChannelPoints)
            {
                this.AddCommand(command);
            }
        }
    }
}
