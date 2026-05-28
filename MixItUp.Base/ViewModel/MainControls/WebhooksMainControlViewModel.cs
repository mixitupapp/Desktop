using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Webhooks;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class WebhookCommandItemViewModel
    {
        public Webhook Webhook { get; set; }
        public WebhookCommandModel Command { get; set; }

        public string Name => Command?.Name ?? Webhook.Id.ToString();

        public WebhookCommandItemViewModel(Webhook webhook, WebhookCommandModel command)
        {
            this.Webhook = webhook;
            this.Command = command;
        }

        public bool IsNewCommand { get { return this.Command == null; } }

        public bool IsExistingCommand { get { return this.Command != null; } }
    }

    public class WebhooksMainControlViewModel : WindowControlViewModelBase
    {
        private IMixItUpService? mixItUpService;

        public ObservableCollection<WebhookCommandItemViewModel> WebhookCommands { get; set; } = new ObservableCollection<WebhookCommandItemViewModel>();

        public bool ShowApiMigrationBanner
        {
            get { return this.showApiMigrationBanner; }
            set
            {
                this.showApiMigrationBanner = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool showApiMigrationBanner = true;

        public int MaxNumberOfWebhooks
        {
            get { return this.maxNumberOfWebhooks; }
            set
            {
                this.maxNumberOfWebhooks = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("CanCreateMoreWebhooks");
                this.NotifyPropertyChanged("AddButtonText");
            }
        }
        private int maxNumberOfWebhooks = 0;
        public bool CanCreateMoreWebhooks { get { return this.WebhookCommands.Count < this.MaxNumberOfWebhooks; } }

        public string AddButtonText => $"{Resources.AddWebhook} [{this.WebhookCommands.Count} / {this.MaxNumberOfWebhooks}]";

        public WebhooksMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
        }

        protected override async Task OnOpenInternal()
        {
            await base.OnOpenInternal();
            this.mixItUpService = ServiceManager.Get<IMixItUpService>();
            if (this.mixItUpService != null)
            {
                this.mixItUpService.OnWebhooksHubAllowed += MixItUpService_OnWebhooksHubAllowed;
                if (this.mixItUpService.IsWebhookHubAllowed)
                {
                    await RefreshCommands();
                }
            }

        }

        protected override async Task OnClosedInternal()
        {
            if (this.mixItUpService != null)
            {
                this.mixItUpService.OnWebhooksHubAllowed -= MixItUpService_OnWebhooksHubAllowed;
                this.mixItUpService = null;
            }
            await base.OnClosedInternal();
        }

        private async void MixItUpService_OnWebhooksHubAllowed(object sender, bool allowed)
        {
            if (allowed && this.mixItUpService != null)
            {
                await RefreshCommands();
            }
        }

        public async Task RefreshCommands()
        {
            try
            {
                GetWebhooksResponseModel response = await ServiceManager.Get<MixItUpService>().GetWebhooks();

                lock (this.WebhookCommands)
                {
                    this.WebhookCommands.Clear();
                    foreach (var webhook in response.Webhooks)
                    {
                        var command = ServiceManager.Get<CommandService>().WebhookCommands.FirstOrDefault(c => c.ID == webhook.Id);
                        this.WebhookCommands.Add(new WebhookCommandItemViewModel(webhook, command));
                    }

                    MaxNumberOfWebhooks = response.MaxNumberOfWebhooks;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}
