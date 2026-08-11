using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class RahiTuberServiceControlViewModel : ServiceControlViewModelBase
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

        public override string WikiPageName { get { return "rahituber"; } }

        public RahiTuberServiceControlViewModel()
            : base(Resources.RahiTuber)
        {
            this.PortNumber = ChannelSession.Settings.RahiTuberPortNumber;

            this.ConnectCommand = this.CreateCommand(async () =>
            {
                ChannelSession.Settings.RahiTuberPortNumber = this.PortNumber;

                Result result = await ServiceManager.Get<RahiTuberService>().Connect();
                if (result.Success)
                {
                    ChannelSession.Settings.RahiTuberEnabled = true;
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
                await ServiceManager.Get<RahiTuberService>().Disconnect();

                ChannelSession.Settings.RahiTuberEnabled = false;
                this.IsConnected = false;
                this.RefreshConnectionDetails();
            });

            this.IsConnected = ServiceManager.Get<RahiTuberService>().IsConnected;
            this.RefreshConnectionDetails();
        }

        protected override Task OnOpenInternal()
        {
            this.IsConnected = ServiceManager.Get<RahiTuberService>().IsConnected;
            this.RefreshConnectionDetails();

            return base.OnOpenInternal();
        }

        private void RefreshConnectionDetails()
        {
            // Showing the address is worth the line. It is the first thing support asks for, and it
            // is how the user can tell the port box was saved rather than just typed into.
            string address = ServiceManager.Get<RahiTuberService>().ConnectedAddress;
            this.ConnectedAddress = string.IsNullOrEmpty(address) ? null : string.Format(Resources.RahiTuberConnectedTo, address);
        }
    }
}
