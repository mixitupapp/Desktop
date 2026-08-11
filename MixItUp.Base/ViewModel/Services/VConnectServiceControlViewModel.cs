using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class VConnectServiceControlViewModel : ServiceControlViewModelBase
    {
        public int PortNumber
        {
            get { return this.portNumber; }
            set
            {
                this.portNumber = value;
                this.NotifyPropertyChanged();
            }
        }
        private int portNumber;

        public string ConnectedAddress
        {
            get { return this.connectedAddress; }
            set
            {
                this.connectedAddress = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.ShowConnectedAddress));
            }
        }
        private string connectedAddress;

        public bool ShowConnectedAddress { get { return !string.IsNullOrEmpty(this.ConnectedAddress); } }

        public ICommand ConnectCommand { get; set; }
        public ICommand DisconnectCommand { get; set; }

        public override string WikiPageName { get { return "vconnect"; } }

        public VConnectServiceControlViewModel()
            : base(Resources.VConnect)
        {
            this.PortNumber = ChannelSession.Settings.VConnectPortNumber;

            this.ConnectCommand = this.CreateCommand(async () =>
            {
                ChannelSession.Settings.VConnectPortNumber = this.PortNumber;

                Result result = await ServiceManager.Get<VConnectService>().Connect();
                if (result.Success)
                {
                    ChannelSession.Settings.VConnectEnabled = true;
                    this.IsConnected = true;
                    this.RefreshConnectionDetails();
                }
                else
                {
                    await this.ShowConnectFailureMessage(result);
                }
            });

            this.DisconnectCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<VConnectService>().Disconnect();

                ChannelSession.Settings.VConnectEnabled = false;
                this.IsConnected = false;
                this.RefreshConnectionDetails();
            });

            this.IsConnected = ServiceManager.Get<VConnectService>().IsConnected;
            this.RefreshConnectionDetails();
        }

        protected override Task OnOpenInternal()
        {
            this.IsConnected = ServiceManager.Get<VConnectService>().IsConnected;
            this.RefreshConnectionDetails();

            return base.OnOpenInternal();
        }

        private void RefreshConnectionDetails()
        {
            // Showing the address is worth the line. It is the first thing support asks for, and the
            // path is not something the user picked, so they cannot otherwise see what Mix It Up
            // landed on.
            string address = ServiceManager.Get<VConnectService>().ConnectedAddress;
            this.ConnectedAddress = string.IsNullOrEmpty(address) ? null : string.Format(Resources.VConnectConnectedTo, address);
        }
    }
}
