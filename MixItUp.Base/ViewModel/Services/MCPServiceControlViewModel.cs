using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class MCPServiceControlViewModel : ServiceControlViewModelBase
    {
        public ICommand ConnectCommand { get; set; }
        public ICommand DisconnectCommand { get; set; }

        public override string WikiPageName { get { return "mcp-server"; } }

        public string ServerAddress { get { return ServiceManager.Get<IMCPService>().ServerAddress; } }

        public bool EnableMCPServerAdvancedMode
        {
            get { return ChannelSession.Settings.EnableMCPServerAdvancedMode; }
            set
            {
                ChannelSession.Settings.EnableMCPServerAdvancedMode = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("EnableMCPServerAdvancedMode");
            }
        }

        public MCPServiceControlViewModel()
            : base(Resources.MCPServer)
        {
            this.ConnectCommand = this.CreateCommand(async () =>
            {
                ChannelSession.Settings.EnableMCPServer = false;
                Result result = await ServiceManager.Get<IMCPService>().Connect();
                if (result.Success)
                {
                    ChannelSession.Settings.EnableMCPServer = true;
                    this.IsConnected = true;
                }
                else
                {
                    await this.ShowConnectFailureMessage(result);
                }
            });

            this.DisconnectCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<IMCPService>().Disconnect();
                ChannelSession.Settings.EnableMCPServer = false;
                this.IsConnected = false;
            });

            this.IsConnected = ServiceManager.Get<IMCPService>().IsConnected;
        }
    }
}
