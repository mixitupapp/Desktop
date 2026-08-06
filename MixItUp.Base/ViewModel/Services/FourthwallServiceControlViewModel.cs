using MixItUp.Base.Model.Webhooks;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class FourthwallServiceControlViewModel : ServiceControlViewModelBase
    {
        public string WebhookURL
        {
            get { return this.webhookURL; }
            set
            {
                this.webhookURL = value;
                this.NotifyPropertyChanged();
            }
        }
        private string webhookURL;

        public ICommand ConnectCommand { get; set; }
        public ICommand DisconnectCommand { get; set; }

        private Guid webhookId;

        public override string WikiPageName { get { return "fourthwall"; } }

        public FourthwallServiceControlViewModel()
            : base(Resources.Fourthwall)
        {
            this.ConnectCommand = this.CreateCommand(async () =>
            {
                Webhook webhook = await ServiceManager.Get<MixItUpService>().CreateWebhook(WebhookServices.Fourthwall);
                if (webhook != null)
                {
                    this.SetConnectedWebhook(webhook);
                }
                else
                {
                    await this.ShowConnectFailureMessage(new Result(success: false));
                }
            });

            this.DisconnectCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<MixItUpService>().DeleteWebhook(this.webhookId);

                this.webhookId = Guid.Empty;
                this.WebhookURL = null;
                this.IsConnected = false;
            });
        }

        protected override async Task OnOpenInternal()
        {
            await base.OnOpenInternal();

            try
            {
                GetWebhooksResponseModel response = await ServiceManager.Get<MixItUpService>().GetWebhooks();
                Webhook webhook = response?.Webhooks?.FirstOrDefault(w => string.Equals(w.Service, WebhookServices.Fourthwall, StringComparison.OrdinalIgnoreCase));
                if (webhook != null)
                {
                    this.SetConnectedWebhook(webhook);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void SetConnectedWebhook(Webhook webhook)
        {
            this.webhookId = webhook.Id;
            this.WebhookURL = $"{MixItUpService.MixItUpAPIEndpoint}webhook/{webhook.Id}?secret={webhook.Secret}";
            this.IsConnected = true;
        }
    }
}
