using MixItUp.Base;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel;
using MixItUp.Base.ViewModel.MainControls;
using MixItUp.WPF.Controls.Commands;
using MixItUp.WPF.Windows.Commands;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for VPZoneChannelPointsControl.xaml
    /// </summary>
    public partial class VPZoneChannelPointsControl : GroupedCommandsMainControlBase
    {
        private VPZoneChannelPointsMainControlViewModel viewModel;

        public VPZoneChannelPointsControl()
        {
            InitializeComponent();
        }

        protected override async Task InitializeInternal()
        {
            this.DataContext = this.viewModel = new VPZoneChannelPointsMainControlViewModel((MainWindowViewModel)this.Window.ViewModel);
            this.SetViewModel(this.viewModel);
            await this.viewModel.OnOpen();

            await base.InitializeInternal();
        }

        protected override async Task OnVisibilityChanged()
        {
            await base.OnVisibilityChanged();
            this.VPZoneChannelPointsNameFilterTextBox.Text = string.Empty;
        }

        private void NameFilterTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            this.viewModel.NameFilter = this.VPZoneChannelPointsNameFilterTextBox.Text;
        }

        private void CommandButtons_EditClicked(object sender, RoutedEventArgs e)
        {
            VPZoneChannelPointsCommandModel command = ((CommandListingButtonsControl)sender).GetCommandFromCommandButtons<VPZoneChannelPointsCommandModel>();
            if (command != null)
            {
                CommandEditorWindow window = CommandEditorWindow.GetCommandEditorWindow(command);
                window.CommandSaved += Window_CommandSaved;
                window.ForceShow();
            }
        }

        private async void CommandButtons_DeleteClicked(object sender, RoutedEventArgs e)
        {
            await this.Window.RunAsyncOperation(async () =>
            {
                VPZoneChannelPointsCommandModel command = ((CommandListingButtonsControl)sender).GetCommandFromCommandButtons<VPZoneChannelPointsCommandModel>();
                if (command != null)
                {
                    ServiceManager.Get<CommandService>().VPZoneChannelPointsCommands.Remove(command);
                    ChannelSession.Settings.RemoveCommand(command);
                    this.viewModel.RemoveCommand(command);
                    await ChannelSession.SaveSettings();
                }
            });
        }

        private void AddCommandButton_Click(object sender, RoutedEventArgs e)
        {
            CommandEditorWindow window = new CommandEditorWindow(CommandTypeEnum.VPZoneChannelPoints);
            window.CommandSaved += Window_CommandSaved;
            window.ForceShow();
        }
    }
}
