using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System.Linq;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class GoogleCloudTTSServiceControlViewModel : ServiceControlViewModelBase
    {
        public string APIKey
        {
            get { return this.apiKey; }
            set
            {
                this.apiKey = value;
                this.NotifyPropertyChanged();
            }
        }
        private string apiKey;

        public ICommand LogInCommand { get; set; }
        public ICommand LogOutCommand { get; set; }

        public override string WikiPageName { get { return "google-cloud-tts"; } }

        public GoogleCloudTTSServiceControlViewModel()
            : base(Resources.GoogleCloudTTS)
        {
            this.LogInCommand = this.CreateCommand(async () =>
            {
                if (!string.IsNullOrEmpty(this.APIKey))
                {
                    ChannelSession.Settings.GoogleCloudTTSCustomKey = this.APIKey;

                    ITextToSpeechConnectableService service = ServiceManager.GetAll<ITextToSpeechConnectableService>().Where(s => s.ProviderType == TextToSpeechProviderType.GoogleCloudTTS).First();
                    Result result = await service.TestAccess();
                    if (result.Success)
                    {
                        this.IsConnected = true;
                    }
                    else
                    {
                        ChannelSession.Settings.GoogleCloudTTSCustomKey = null;
                        await this.ShowConnectFailureMessage(result);
                    }
                }
            });

            this.LogOutCommand = this.CreateCommand(() =>
            {
                ChannelSession.Settings.GoogleCloudTTSCustomKey = null;
                this.IsConnected = false;
            });

            this.IsConnected = !string.IsNullOrEmpty(ChannelSession.Settings.GoogleCloudTTSCustomKey);
        }
    }
}