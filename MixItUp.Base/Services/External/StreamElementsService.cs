using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    [DataContract]
    public class StreamElementsChannel
    {
        [DataMember]
        public string _id { get; set; }
        [DataMember]
        public string username { get; set; }
        [DataMember]
        public string alias { get; set; }
        [DataMember]
        public string displayName { get; set; }

        [DataMember]
        public string providerId { get; set; }
        [DataMember]
        public string provider { get; set; }

        [DataMember]
        public string createdAt { get; set; }
        [DataMember]
        public string updatedAt { get; set; }
    }

    [DataContract]
    public class StreamElementsTipEventModel
    {
        [DataMember]
        public string tipId { get; set; }

        [DataMember]
        public string username { get; set; }

        [DataMember]
        public double? amount { get; set; }

        [DataMember]
        public string currency { get; set; }

        [DataMember]
        public string message { get; set; }

        public UserDonationModel ToGenericDonation()
        {
            return new UserDonationModel()
            {
                Source = UserDonationSourceEnum.StreamElements,

                ID = this.tipId,
                Username = this.username,
                Message = this.message ?? string.Empty,

                Amount = Math.Round(this.amount.GetValueOrDefault(), 2),

                DateTime = DateTimeOffset.Now,
            };
        }
    }

    public class StreamElementsService : OAuthExternalServiceBase
    {
        private const string BaseAddress = "https://api.streamelements.com/kappa/v2/";

        private const string ClientID = "460928647d5469dd";
        private const string AuthorizationUrl = "https://api.streamelements.com/oauth2/authorize?client_id={0}&redirect_uri=http://localhost:8919/&response_type=code&state={1}&scope=tips:read";
        private const string TokenUrl = "https://api.streamelements.com/oauth2/token";

        private const string AstroWebSocketAddress = "wss://astro.streamelements.com";
        private const string AstroTopicTips = "channel.tips";
        private const string AstroTokenTypeOAuth = "oauth";

        public bool WebSocketConnected { get; private set; }

        private HashSet<string> donationsProcessed = new HashSet<string>();
        private object donationsProcessedLock = new object();

        private StreamElementsChannel channel;
        private AdvancedClientWebSocket webSocket = new AdvancedClientWebSocket();
        private string tipsSubscriptionNonce;
        private bool isDisconnecting = false;

        public StreamElementsService()
            : base(StreamElementsService.BaseAddress)
        {
        }

        public override string Name { get { return MixItUp.Base.Resources.StreamElements; } }

        public override async Task<Result> Connect()
        {
            try
            {
                string authorizationCode = await this.ConnectViaOAuthRedirect(string.Format(StreamElementsService.AuthorizationUrl, StreamElementsService.ClientID, Guid.NewGuid().ToString()));
                if (!string.IsNullOrEmpty(authorizationCode))
                {
                    string clientSecret = ServiceManager.Get<SecretsService>().GetSecret("StreamElementsSecret");

                    List<KeyValuePair<string, string>> bodyContent = new List<KeyValuePair<string, string>>();
                    bodyContent.Add(new KeyValuePair<string, string>("grant_type", "authorization_code"));
                    bodyContent.Add(new KeyValuePair<string, string>("client_id", StreamElementsService.ClientID));
                    bodyContent.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
                    bodyContent.Add(new KeyValuePair<string, string>("code", authorizationCode));
                    bodyContent.Add(new KeyValuePair<string, string>("redirect_uri", OAuthExternalServiceBase.DEFAULT_OAUTH_LOCALHOST_URL));

                    this.token = await this.GetWWWFormUrlEncodedOAuthToken(StreamElementsService.TokenUrl, StreamElementsService.ClientID, clientSecret, bodyContent);
                    if (this.token != null)
                    {
                        return await this.InitializeInternal();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result(ex);
            }
            return new Result(false);
        }

        public override async Task Disconnect()
        {
            this.isDisconnecting = true;
            this.webSocket.PacketReceived -= WebSocket_PacketReceived;
            this.webSocket.Disconnected -= WebSocket_Disconnected;
            await this.webSocket.Disconnect();
            this.WebSocketConnected = false;

            this.token = null;
        }

        public async Task<StreamElementsChannel> GetCurrentChannel()
        {
            return await this.GetAsync<StreamElementsChannel>("channels/me");
        }

        protected override async Task RefreshOAuthToken()
        {
            if (this.token != null)
            {
                string clientSecret = ServiceManager.Get<SecretsService>().GetSecret("StreamElementsSecret");

                List<KeyValuePair<string, string>> bodyContent = new List<KeyValuePair<string, string>>();
                bodyContent.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
                bodyContent.Add(new KeyValuePair<string, string>("client_id", StreamElementsService.ClientID));
                bodyContent.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
                bodyContent.Add(new KeyValuePair<string, string>("refresh_token", this.token.refreshToken));
                bodyContent.Add(new KeyValuePair<string, string>("redirect_uri", OAuthExternalServiceBase.DEFAULT_OAUTH_LOCALHOST_URL));

                this.token = await this.GetWWWFormUrlEncodedOAuthToken(StreamElementsService.TokenUrl, StreamElementsService.ClientID, clientSecret, bodyContent);
            }
        }

        protected override async Task<Result> InitializeInternal()
        {
            this.channel = await this.GetCurrentChannel();
            if (this.channel != null)
            {
                if (await this.ConnectWebSocket())
                {
                    this.TrackServiceTelemetry("StreamElements");
                    return new Result();
                }
                return new Result(Resources.StreamElementsSocketFailed);
            }
            return new Result(Resources.StreamElementsUserDataFailed);
        }

        protected override async Task<AdvancedHttpClient> GetHttpClient(bool autoRefreshToken = true)
        {
            AdvancedHttpClient client = await base.GetHttpClient(autoRefreshToken);
            if (this.token != null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", this.token.accessToken);
            }
            return client;
        }

        private async void WebSocket_Disconnected(object sender, WebSocketCloseStatus e)
        {
            if (this.isDisconnecting) { return; }

            ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.StreamElements);

            do
            {
                await Task.Delay(5000);
            } while (!this.isDisconnecting && !await this.ConnectWebSocket());

            if (!this.isDisconnecting)
            {
                ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.StreamElements);
            }
        }

        private async Task<bool> ConnectWebSocket()
        {
            try
            {
                this.isDisconnecting = false;
                this.WebSocketConnected = false;
                this.webSocket.PacketReceived -= WebSocket_PacketReceived;
                this.webSocket.Disconnected -= WebSocket_Disconnected;
                await this.webSocket.Disconnect();

                this.webSocket.PacketReceived += WebSocket_PacketReceived;
                this.webSocket.Disconnected += WebSocket_Disconnected;

                if (!await this.webSocket.Connect(StreamElementsService.AstroWebSocketAddress, CancellationToken.None))
                {
                    return false;
                }

                this.tipsSubscriptionNonce = Guid.NewGuid().ToString();
                JObject tipsPacket = new JObject();
                tipsPacket["type"] = "subscribe";
                tipsPacket["nonce"] = this.tipsSubscriptionNonce;
                tipsPacket["data"] = new JObject()
                {
                    ["topic"] = StreamElementsService.AstroTopicTips,
                    ["room"] = this.channel._id,
                    ["token"] = this.token.accessToken,
                    ["token_type"] = StreamElementsService.AstroTokenTypeOAuth,
                };
                await this.webSocket.Send(tipsPacket);

                for (int i = 0; i < 10 && !this.WebSocketConnected; i++)
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return this.WebSocketConnected;
        }

        private void WebSocket_PacketReceived(object sender, string packet)
        {
            _ = Task.Run(() => this.ProcessWebSocketPacket(packet));
        }

        private async Task ProcessWebSocketPacket(string packet)
        {
            try
            {
                if (string.IsNullOrEmpty(packet)) { return; }

                Logger.Log(LogLevel.Debug, "StreamElements event: " + packet);

                JObject eventJObj = JObject.Parse(packet);
                string type = eventJObj["type"]?.Value<string>();
                if (string.IsNullOrEmpty(type)) { return; }

                if (string.Equals(type, "response", StringComparison.OrdinalIgnoreCase))
                {
                    string nonce = eventJObj["nonce"]?.Value<string>();
                    string error = eventJObj["error"]?.Value<string>();
                    string topic = eventJObj["data"]?["topic"]?.Value<string>();
                    if (string.Equals(nonce, this.tipsSubscriptionNonce, StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrEmpty(error)
                        && string.Equals(topic, StreamElementsService.AstroTopicTips, StringComparison.OrdinalIgnoreCase))
                    {
                        this.WebSocketConnected = true;
                    }
                    return;
                }

                if (!string.Equals(type, "message", StringComparison.OrdinalIgnoreCase)) { return; }

                string topicName = eventJObj["topic"]?.Value<string>();
                if (!string.Equals(topicName, StreamElementsService.AstroTopicTips, StringComparison.OrdinalIgnoreCase)) { return; }

                JObject tipData = eventJObj["data"] as JObject;
                if (tipData == null) { return; }

                string tipStatus = tipData["status"]?.Value<string>();
                if (!string.IsNullOrEmpty(tipStatus) && !string.Equals(tipStatus, "success", StringComparison.OrdinalIgnoreCase)) { return; }

                StreamElementsTipEventModel tipEvent = new StreamElementsTipEventModel()
                {
                    tipId = tipData["transactionId"]?.Value<string>() ?? tipData["_id"]?.Value<string>() ?? eventJObj["id"]?.Value<string>(),
                    username = tipData["donation"]?["user"]?["username"]?.Value<string>() ?? tipData["username"]?.Value<string>(),
                    amount = tipData["donation"]?["amount"]?.Value<double?>() ?? tipData["amount"]?.Value<double?>(),
                    currency = tipData["donation"]?["currency"]?.Value<string>() ?? tipData["currency"]?.Value<string>(),
                    message = tipData["donation"]?["message"]?.Value<string>() ?? tipData["message"]?.Value<string>(),
                };

                await this.ProcessTipDonation(tipEvent);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async Task ProcessTipDonation(StreamElementsTipEventModel tipEvent)
        {
            if (tipEvent == null) { return; }

            bool shouldProcessDonation = false;
            lock (this.donationsProcessedLock)
            {
                if (!this.donationsProcessed.Contains(tipEvent.tipId))
                {
                    this.donationsProcessed.Add(tipEvent.tipId);
                    shouldProcessDonation = true;
                }
            }

            if (shouldProcessDonation && !string.IsNullOrWhiteSpace(tipEvent.username) && tipEvent.amount.GetValueOrDefault() > 0)
            {
                await EventService.ProcessDonationEvent(EventTypeEnum.StreamElementsDonation, tipEvent.ToGenericDonation());
            }
        }
    }
}
