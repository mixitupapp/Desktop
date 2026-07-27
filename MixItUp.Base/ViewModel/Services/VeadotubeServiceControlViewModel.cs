using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class VeadotubeServiceControlViewModel : ServiceControlViewModelBase
    {
        /// <summary>
        /// Optional, and empty for everyone running veadotube on this machine. It only exists for a
        /// second computer, where there is no local instance file to read the address out of.
        /// </summary>
        public string ManualAddress
        {
            get { return this.manualAddress; }
            set
            {
                this.manualAddress = value;
                this.NotifyPropertyChanged();
            }
        }
        private string manualAddress;

        public string DetectedInstance
        {
            get { return this.detectedInstance; }
            set
            {
                this.detectedInstance = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowDetectedInstance");
            }
        }
        private string detectedInstance;

        public bool ShowDetectedInstance { get { return !string.IsNullOrEmpty(this.DetectedInstance); } }

        public bool ShowVersionWarning
        {
            get { return this.showVersionWarning; }
            set
            {
                this.showVersionWarning = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool showVersionWarning;

        public ICommand ConnectCommand { get; set; }
        public ICommand DisconnectCommand { get; set; }

        public override string WikiPageName { get { return "veadotube"; } }

        public VeadotubeServiceControlViewModel()
            : base(Resources.Veadotube)
        {
            this.ManualAddress = ChannelSession.Settings.VeadotubeManualAddress;

            this.ConnectCommand = this.CreateCommand(async () =>
            {
                // Blank it rather than storing whitespace. Anything non-empty here turns discovery off
                // for good, so a stray space would leave the user pinned to nothing with no way back
                // that looks like one.
                this.ManualAddress = string.IsNullOrWhiteSpace(this.ManualAddress) ? null : this.ManualAddress.Trim();
                ChannelSession.Settings.VeadotubeManualAddress = this.ManualAddress;

                Result result = await ServiceManager.Get<VeadotubeService>().Connect();
                if (result.Success)
                {
                    ChannelSession.Settings.VeadotubeEnabled = true;
                    this.IsConnected = true;
                    this.RefreshInstanceDetails();
                }
                else
                {
                    await this.ShowConnectFailureMessage(result);
                }
            });

            this.DisconnectCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<VeadotubeService>().Disconnect();

                ChannelSession.Settings.VeadotubeEnabled = false;
                this.IsConnected = false;
                this.RefreshInstanceDetails();
            });

            this.IsConnected = ServiceManager.Get<VeadotubeService>().IsConnected;
            this.RefreshInstanceDetails();
        }

        protected override Task OnOpenInternal()
        {
            this.IsConnected = ServiceManager.Get<VeadotubeService>().IsConnected;
            this.RefreshInstanceDetails();

            return base.OnOpenInternal();
        }

        private void RefreshInstanceDetails()
        {
            VeadotubeService service = ServiceManager.Get<VeadotubeService>();
            VeadotubeInstanceInfo info = service.InstanceInfo;
            if (info != null)
            {
                string instance = string.Format("{0} {1}", info.name, info.version);

                // Showing the address is worth the line. It is the first thing support asks for and
                // the user cannot otherwise see which of their veadotubes Mix It Up landed on.
                this.DetectedInstance = string.IsNullOrEmpty(service.ConnectedAddress) ? instance :
                    string.Format(Resources.VeadotubeConnectedTo, instance, service.ConnectedAddress);
                this.ShowVersionWarning = info.IsPre2Point1;
            }
            else
            {
                this.DetectedInstance = null;
                this.ShowVersionWarning = false;
            }
        }
    }
}
