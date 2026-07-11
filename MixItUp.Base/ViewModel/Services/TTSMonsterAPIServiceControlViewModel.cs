using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using MixItUp.Base.Model.Web;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class TTSMonsterAPIServiceControlViewModel : ServiceControlViewModelBase
    {
        public string APIToken
        {
            get { return this.apiToken; }
            set
            {
                this.apiToken = value;
                this.NotifyPropertyChanged();
            }
        }
        private string apiToken;

        public ICommand LogInCommand { get; set; }
        public ICommand LogOutCommand { get; set; }

        public override string WikiPageName { get { return "ttsmonster"; } }

        public TTSMonsterAPIServiceControlViewModel()
            : base(Resources.TTSMonsterAPI)
        {
            this.LogInCommand = this.CreateCommand(async () =>
            {
                if (!string.IsNullOrWhiteSpace(this.APIToken))
                {
                    Result result = await ServiceManager.Get<ITTSMonsterAPIService>().Connect(new OAuthTokenModel()
                    {
                        accessToken = this.APIToken.Trim()
                    });

                    if (result.Success)
                    {
                        this.IsConnected = true;
                        return;
                    }
                    else
                    {
                        await this.ShowConnectFailureMessage(result);
                        return;
                    }
                }

                await DialogHelper.ShowMessage(Resources.TTSMonsterInvalidAPIToken);
            });

            this.LogOutCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<ITTSMonsterAPIService>().Disconnect();

                ChannelSession.Settings.TTSMonsterAPIOAuthToken = null;

                this.IsConnected = false;
            });

            this.IsConnected = ServiceManager.Get<ITTSMonsterAPIService>().IsConnected;
        }
    }
}
