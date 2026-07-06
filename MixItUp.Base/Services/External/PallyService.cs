using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class PallyWebSocketMessage
    {
        public const string CampaignTipNotifyType = "campaigntip.notify";

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("payload")]
        public PallyCampaignTipNotifyPayload Payload { get; set; }
    }

    public class PallyCampaignTipNotifyPayload
    {
        [JsonProperty("campaignTip")]
        public PallyCampaignTip CampaignTip { get; set; }

        [JsonProperty("page")]
        public PallyPage Page { get; set; }
    }

    public class PallyCampaignTip
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("grossAmountInCents")]
        public int? GrossAmountInCents { get; set; }

        [JsonProperty("netAmountInCents")]
        public int? NetAmountInCents { get; set; }

        [JsonProperty("processingFeeInCents")]
        public int? ProcessingFeeInCents { get; set; }

        public UserDonationModel ToGenericDonation()
        {
            bool isAnonymous = string.IsNullOrEmpty(this.DisplayName);

            return new UserDonationModel()
            {
                Source = UserDonationSourceEnum.Pally,

                ID = this.Id,
                Username = isAnonymous ? null : this.DisplayName,
                IsAnonymous = isAnonymous,

                Message = this.Message,

                Amount = Math.Round((this.GrossAmountInCents ?? 0) / 100.0, 2),

                DateTime = this.CreatedAt ?? DateTimeOffset.Now,
            };
        }
    }

    public class PallyPage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class PallyWebSocket : ClientWebSocketBase
    {
        public event EventHandler<PallyCampaignTipNotifyPayload> OnCampaignTipReceived = delegate { };

        protected override Task ProcessReceivedPacket(string packet)
        {
            try
            {
                if (string.IsNullOrEmpty(packet) || string.Equals(packet, "pong", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                PallyWebSocketMessage message = JSONSerializerHelper.DeserializeFromString<PallyWebSocketMessage>(packet);
                if (message != null && string.Equals(message.Type, PallyWebSocketMessage.CampaignTipNotifyType) && message.Payload?.CampaignTip != null)
                {
                    this.OnCampaignTipReceived(this, message.Payload);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log("Pally Service - Failed Packet Processing: " + packet);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// https://docs.pally.gg/advanced/websockets - the WebSockets feed is in Beta and payloads may
    /// contain additional undocumented fields.
    ///
    /// Smoke test without real money: connect with your API key (e.g. from websocketking.com) and send
    /// this echo message as a string over the same connection - the server echoes the payload back and
    /// it flows through the normal campaigntip.notify handling:
    /// {"type":"echo","payload":{"type":"campaigntip.notify","payload":{"campaignTip":{...},"page":{...}}}}
    /// </summary>
    public class PallyService : OAuthExternalServiceBase
    {
        public const string WebsocketUrl = "wss://events.pally.gg";

        public override string Name { get { return Resources.Pally; } }

        private PallyWebSocket socket;
        private CancellationTokenSource keepAliveCancellationTokenSource;

        public PallyService() : base(string.Empty) { }

        public override Task<Result> Connect()
        {
            return Task.FromResult(new Result(false));
        }

        public override async Task Disconnect()
        {
            await this.DisconnectWebSocket();

            this.token = null;
        }

        protected override async Task<Result> InitializeInternal()
        {
            if (await this.ConnectWebSocket())
            {
                return new Result();
            }
            return new Result(Resources.PallyFailedToConnect);
        }

        protected override Task RefreshOAuthToken()
        {
            // Pally uses a static API key; there is nothing to refresh.
            return Task.CompletedTask;
        }

        private async Task<bool> ConnectWebSocket()
        {
            await this.DisconnectWebSocket();

            this.socket = new PallyWebSocket();
            this.socket.OnCampaignTipReceived += Socket_OnCampaignTipReceived;
            this.socket.OnDisconnectOccurred += Socket_OnDisconnectOccurred;

            if (!await this.socket.Connect($"{PallyService.WebsocketUrl}?auth={this.token.accessToken}&channel=firehose"))
            {
                return false;
            }

            // Events are only delivered to open connections; a "ping" every 60 seconds keeps the
            // connection alive (the server replies "pong").
            this.keepAliveCancellationTokenSource = new CancellationTokenSource();
            AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
            {
                if (this.socket != null)
                {
                    await this.socket.Send("ping");
                }
            }, this.keepAliveCancellationTokenSource.Token, 60000);

            return true;
        }

        private async Task DisconnectWebSocket()
        {
            if (this.keepAliveCancellationTokenSource != null)
            {
                this.keepAliveCancellationTokenSource.Cancel();
                this.keepAliveCancellationTokenSource = null;
            }

            if (this.socket != null)
            {
                this.socket.OnCampaignTipReceived -= Socket_OnCampaignTipReceived;
                this.socket.OnDisconnectOccurred -= Socket_OnDisconnectOccurred;
                await this.socket.Disconnect();

                this.socket = null;
            }
        }

        private async void Socket_OnCampaignTipReceived(object sender, PallyCampaignTipNotifyPayload payload)
        {
            await this.ProcessCampaignTip(payload);
        }

        // Split out from Socket_OnCampaignTipReceived so it can be fed a synthetic payload for testing
        // (e.g. from DebugControl) without requiring a live, authenticated WebSocket connection.
        public async Task ProcessCampaignTip(PallyCampaignTipNotifyPayload payload)
        {
            try
            {
                // Pally re-sends campaigntip.notify when a tip is replayed from the activity feed;
                // replays are an intentional feature, so deliveries are not deduplicated.
                UserDonationModel donation = payload.CampaignTip.ToGenericDonation();

                Dictionary<string, string> additionalSpecialIdentifiers = new Dictionary<string, string>();
                additionalSpecialIdentifiers["pallypageslug"] = payload.Page?.Slug ?? string.Empty;
                additionalSpecialIdentifiers["pallypagetitle"] = payload.Page?.Title ?? string.Empty;

                await EventService.ProcessDonationEvent(EventTypeEnum.PallyDonation, donation, additionalSpecialIdentifiers: additionalSpecialIdentifiers);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async void Socket_OnDisconnectOccurred(object sender, WebSocketCloseStatus e)
        {
            ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.Pally);

            do
            {
                await this.DisconnectWebSocket();

                await Task.Delay(5000);
            }
            while (this.token != null && !await this.ConnectWebSocket());

            if (this.token != null)
            {
                ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.Pally);
            }
        }
    }
}
