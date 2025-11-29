using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System.Linq;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class ResponsiveVoiceServiceControlViewModel : ServiceControlViewModelBase
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

        public override string WikiPageName { get { return "responsive-voice"; } }

        public ResponsiveVoiceServiceControlViewModel()
            : base(Resources.ResponsiveVoice)
        {
            this.LogInCommand = this.CreateCommand(async () =>
            {
                if (!string.IsNullOrEmpty(this.APIKey))
                {
                    ChannelSession.Settings.ResponsiveVoiceCustomAPIKey = this.APIKey;

                    ITextToSpeechConnectableService service = ServiceManager.GetAll<ITextToSpeechConnectableService>().Where(s => s.ProviderType == TextToSpeechProviderType.ResponsiveVoice).First();
                    Result result = await service.TestAccess();
                    if (result.Success)
                    {
                        this.IsConnected = true;

                        if (ServiceManager.Get<OverlayV3Service>().IsConnected)
                        {
                            await ServiceManager.Get<OverlayV3Service>().Disconnect();
                            await ServiceManager.Get<OverlayV3Service>().Connect();
                        }
                    }
                    else
                    {
                        ChannelSession.Settings.ResponsiveVoiceCustomAPIKey = null;
                        await this.ShowConnectFailureMessage(result);
                    }
                }
            });

            this.LogOutCommand = this.CreateCommand(async () =>
            {
                ChannelSession.Settings.ResponsiveVoiceCustomAPIKey = null;
                this.IsConnected = false;

                if (ServiceManager.Get<OverlayV3Service>().IsConnected)
                {
                    await ServiceManager.Get<OverlayV3Service>().Disconnect();
                    await ServiceManager.Get<OverlayV3Service>().Connect();
                }
            });

            this.IsConnected = !string.IsNullOrEmpty(ChannelSession.Settings.ResponsiveVoiceCustomAPIKey);
        }
    }
}