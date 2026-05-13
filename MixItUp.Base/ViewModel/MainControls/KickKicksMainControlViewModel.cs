using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using System.Collections.Generic;
using System.Linq;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class KickKicksMainControlViewModel : GroupedCommandsMainControlViewModelBase
    {
        public KickKicksMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
            GroupedCommandsMainControlViewModelBase.OnCommandAddedEdited += GroupedCommandsMainControlViewModelBase_OnCommandAddedEdited;
        }

        protected override IEnumerable<CommandModelBase> GetCommands()
        {
            return ServiceManager.Get<CommandService>().KickKicksCommands.ToList();
        }

        private void GroupedCommandsMainControlViewModelBase_OnCommandAddedEdited(object sender, CommandModelBase command)
        {
            if (command.Type == CommandTypeEnum.KickKicks)
            {
                this.AddCommand(command);
            }
        }
    }
}
