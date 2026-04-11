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
        private readonly string[] subscriptionTopics = new string[] { StreamElementsService.AstroTopicTips };
        private readonly object subscriptionLock = new object();
        private readonly Dictionary<string, string> pendingSubscriptionsByNonce = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> successfulSubscriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool subscriptionFailed = false;

        private int isDisconnecting = 0;
        private int? lastConnectionFailureStatusCode = null;

        private readonly SemaphoreSlim connectSemaphore = new SemaphoreSlim(1);
        private readonly SemaphoreSlim reconnectSemaphore = new SemaphoreSlim(1);

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
            this.SetIsDisconnecting(true);
            this.WebSocketConnected = false;
            this.ResetSubscriptionState();
            this.lastConnectionFailureStatusCode = null;

            this.webSocket.PacketReceived -= WebSocket_PacketReceived;
            this.webSocket.Disconnected -= WebSocket_Disconnected;
            await this.webSocket.Disconnect();

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
            this.SetIsDisconnecting(false);

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

        private void WebSocket_Disconnected(object sender, WebSocketCloseStatus e)
        {
            _ = Task.Run(() => this.ReconnectLoop());
        }

        private async Task<bool> ConnectWebSocket()
        {
            await this.connectSemaphore.WaitAsync();
            try
            {
                if (this.IsDisconnecting() || this.channel == null || this.token == null)
                {
                    return false;
                }

                this.WebSocketConnected = false;
                this.ResetSubscriptionState();
                this.lastConnectionFailureStatusCode = null;

                this.webSocket.PacketReceived -= WebSocket_PacketReceived;
                this.webSocket.Disconnected -= WebSocket_Disconnected;
                await this.webSocket.Disconnect();

                this.webSocket.PacketReceived += WebSocket_PacketReceived;
                this.webSocket.Disconnected += WebSocket_Disconnected;

                if (!await this.webSocket.Connect(StreamElementsService.AstroWebSocketAddress, CancellationToken.None))
                {
                    return false;
                }

                foreach (string topic in this.subscriptionTopics)
                {
                    string nonce = Guid.NewGuid().ToString();
                    lock (this.subscriptionLock)
                    {
                        this.pendingSubscriptionsByNonce[nonce] = topic;
                    }

                    JObject subscribePacket = new JObject();
                    subscribePacket["type"] = "subscribe";
                    subscribePacket["nonce"] = nonce;
                    subscribePacket["data"] = new JObject()
                    {
                        ["topic"] = topic,
                        ["room"] = this.channel._id,
                        ["token"] = this.token.accessToken,
                        ["token_type"] = StreamElementsService.AstroTokenTypeOAuth,
                    };
                    await this.webSocket.Send(subscribePacket);
                }

                for (int i = 0; i < 10 && !this.WebSocketConnected && !this.HasSubscriptionFailure(); i++)
                {
                    await Task.Delay(1000);
                }

                if (!this.WebSocketConnected || this.HasSubscriptionFailure())
                {
                    await this.webSocket.Disconnect();
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.lastConnectionFailureStatusCode = this.webSocket.LastConnectHttpStatusCode;
                Logger.Log(ex);
            }
            finally
            {
                this.connectSemaphore.Release();
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
                    if (!string.IsNullOrEmpty(nonce))
                    {
                        string expectedTopic = null;
                        lock (this.subscriptionLock)
                        {
                            this.pendingSubscriptionsByNonce.TryGetValue(nonce, out expectedTopic);
                        }

                        if (!string.IsNullOrEmpty(expectedTopic) &&
                            (string.IsNullOrEmpty(topic) || string.Equals(topic, expectedTopic, StringComparison.OrdinalIgnoreCase)))
                        {
                            lock (this.subscriptionLock)
                            {
                                if (string.IsNullOrEmpty(error))
                                {
                                    this.successfulSubscriptions.Add(expectedTopic);
                                }
                                else
                                {
                                    this.subscriptionFailed = true;
                                }
                                this.pendingSubscriptionsByNonce.Remove(nonce);
                            }

                            this.WebSocketConnected = this.AreAllSubscriptionsConnected();
                        }
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
                Logger.ForceLog(LogLevel.Information, $"StreamElements donation event received: {tipEvent.username ?? string.Empty} - {tipEvent.amount?.ToString() ?? string.Empty} - {tipEvent.message ?? string.Empty}");

                await this.ProcessTipDonation(tipEvent);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async Task ReconnectLoop()
        {
            if (!await this.reconnectSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (this.IsDisconnecting())
                {
                    return;
                }

                this.WebSocketConnected = false;
                ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.StreamElements);

                int attempt = 0;
                while (!this.IsDisconnecting())
                {
                    int delayMs = this.GetReconnectDelayMs(attempt++);
                    await Task.Delay(delayMs);

                    if (this.IsDisconnecting())
                    {
                        break;
                    }

                    if (await this.ConnectWebSocket())
                    {
                        ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.StreamElements);
                        break;
                    }
                }
            }
            finally
            {
                this.reconnectSemaphore.Release();
            }
        }

        private int GetReconnectDelayMs(int attempt)
        {
            int clampedAttempt = Math.Min(attempt, 8);
            int delayMs = (int)Math.Min(60000, 5000 * (1 << clampedAttempt));

            if (this.lastConnectionFailureStatusCode == 429)
            {
                delayMs = Math.Max(delayMs, 30000);
            }
            else if (this.lastConnectionFailureStatusCode >= 500 && this.lastConnectionFailureStatusCode <= 599)
            {
                delayMs = Math.Max(delayMs, 10000);
            }

            return delayMs + Random.Shared.Next(0, 1001);
        }

        private void ResetSubscriptionState()
        {
            lock (this.subscriptionLock)
            {
                this.pendingSubscriptionsByNonce.Clear();
                this.successfulSubscriptions.Clear();
                this.subscriptionFailed = false;
            }
        }

        private bool HasSubscriptionFailure()
        {
            lock (this.subscriptionLock)
            {
                return this.subscriptionFailed;
            }
        }

        private bool AreAllSubscriptionsConnected()
        {
            lock (this.subscriptionLock)
            {
                foreach (string topic in this.subscriptionTopics)
                {
                    if (!this.successfulSubscriptions.Contains(topic))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private bool IsDisconnecting()
        {
            return Interlocked.CompareExchange(ref this.isDisconnecting, 0, 0) == 1;
        }

        private void SetIsDisconnecting(bool value)
        {
            Interlocked.Exchange(ref this.isDisconnecting, value ? 1 : 0);
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
