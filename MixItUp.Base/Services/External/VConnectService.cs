using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class VConnectTrigger
    {
        public string uid { get; set; }
        public string name { get; set; }
        public bool enabled { get; set; }
        public int nodeCount { get; set; }

        public string DisplayName { get { return !string.IsNullOrEmpty(this.name) ? this.name : this.uid; } }

        // The action editor binds these into a ComboBox, and without this a screen reader and UI
        // Automation both read the row as the class name rather than the trigger.
        public override string ToString() { return this.DisplayName; }
    }

    public class VConnectAsset
    {
        public string uid { get; set; }
        public string name { get; set; }
        public string type { get; set; }

        /// <summary>
        /// A base64 PNG, and only ever filled in when a screenshot was asked for. VConnect returns null
        /// for the asset types it cannot render a preview of, such as audio, video and particle systems.
        /// </summary>
        public string screenshot { get; set; }

        public string DisplayName { get { return !string.IsNullOrEmpty(this.name) ? this.name : this.uid; } }

        public override string ToString() { return this.DisplayName; }
    }

    /// <summary>
    /// The envelope every request, response and broadcast event shares.
    /// </summary>
    public class VConnectPacket
    {
        public const string PublicApiName = "VConnectPublicApi";
        public const string PublicApiVersion = "1.0";

        public string apiName { get; set; }
        public string apiVersion { get; set; }
        public string requestID { get; set; }
        public string messageType { get; set; }
        public string timestamp { get; set; }
        public JObject data { get; set; }

        public VConnectPacket() { }

        public VConnectPacket(string messageType, JObject data = null)
        {
            this.apiName = PublicApiName;
            this.apiVersion = PublicApiVersion;
            this.requestID = Guid.NewGuid().ToString();
            this.messageType = messageType;
            this.timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            this.data = data;
        }

        public string GetErrorMessage()
        {
            if (this.data != null && this.data.TryGetValue("message", out JToken message))
            {
                return message.ToString();
            }
            return null;
        }
    }

    public class VConnectWebSocket : ClientWebSocketBase
    {
        public event EventHandler<VConnectPacket> PacketReceived = delegate { };

        public async Task Send(VConnectPacket packet)
        {
            // The shared serializer settings emit a $type property carrying the C# class name by
            // default. VConnect ignores unknown fields, but sending the name of one of our internals
            // to another program is not something to rely on.
            string contents = JSONSerializerHelper.SerializeToString(packet, includeObjectType: false);

            Logger.Log(LogLevel.Debug, "VConnect Packet Sent - " + contents);

            await this.Send(contents);
        }

        protected override Task ProcessReceivedPacket(string packet)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packet))
                {
                    return Task.CompletedTask;
                }

                Logger.Log(LogLevel.Debug, "VConnect Packet Received - " + packet);

                VConnectPacket received = JSONSerializerHelper.DeserializeFromString<VConnectPacket>(packet);
                if (received != null && !string.IsNullOrEmpty(received.messageType))
                {
                    this.PacketReceived(this, received);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log("VConnect Service - Failed Packet Processing: " + packet);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// https://github.com/Remasuri/VConnect_API
    /// </summary>
    public class VConnectService : IExternalService
    {
        /// <summary>
        /// The port VConnect's WebSocket server ships on. The services page carries a port box for the
        /// case where it has been moved.
        /// </summary>
        public const int DefaultPortNumber = 39542;

        public const string TriggerActivateRequest = "TriggerActivateRequest";
        public const string TriggerActivateResponse = "TriggerActivateResponse";
        public const string TriggerListRequest = "TriggerListRequest";
        public const string TriggerListResponse = "TriggerListResponse";
        public const string AssetListRequest = "AssetListRequest";
        public const string AssetListResponse = "AssetListResponse";
        public const string AssetInfoRequest = "AssetInfoRequest";
        public const string AssetInfoResponse = "AssetInfoResponse";
        public const string ErrorResponse = "ErrorResponse";

        public const string TriggerActivatedEvent = "TriggerActivatedEvent";
        public const string TriggerEndedEvent = "TriggerEndedEvent";
        public const string AssetSpawnEvent = "AssetSpawnEvent";
        public const string AssetHitEvent = "AssetHitEvent";
        public const string AssetDespawnEvent = "AssetDespawnEvent";

        /// <summary>
        /// The message type shared by the Send WebSocket Message and On WebSocket Receive graph nodes.
        /// </summary>
        public const string CustomMessageType = "websocket_node";

        /// <summary>How long a trigger or asset list is reused before it is asked for again.</summary>
        public const int MaxCacheMinutes = 30;

        private const string websocketAddressFormat = "ws://127.0.0.1:{0}{1}";

        /// <summary>
        /// The spec does not name a path, so the one T.I.T.S. serves on is tried first and the bare
        /// root second. Whichever answers is kept for the rest of the session, including reconnects.
        /// </summary>
        private static readonly string[] EndpointPaths = new string[] { "/websocket", "/" };

        private const int RequestTimeoutSeconds = 15;
        private const int ReconnectDelayMilliseconds = 5000;

        /// <summary>
        /// How often the watchdog checks that the socket is still open. Closing VConnect's WebSocket
        /// server is a clean close, which WebSocketBase does not raise a disconnect for, so the event
        /// on its own is not enough to notice the drop.
        /// </summary>
        private const int ConnectionCheckMilliseconds = 2000;

        public string Name { get { return Resources.VConnect; } }

        public bool IsConnected { get; private set; }

        /// <summary>The endpoint currently in use, or null when not connected.</summary>
        public string ConnectedAddress { get; private set; }

        private VConnectWebSocket websocket;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<VConnectPacket>> pendingRequests =
            new ConcurrentDictionary<string, TaskCompletionSource<VConnectPacket>>();

        private List<VConnectTrigger> triggerCache = new List<VConnectTrigger>();
        private DateTimeOffset triggerCacheExpiration = DateTimeOffset.MinValue;

        private List<VConnectAsset> assetCache = new List<VConnectAsset>();
        private DateTimeOffset assetCacheExpiration = DateTimeOffset.MinValue;

        private string resolvedPath;

        private CancellationTokenSource watchdogCancellationTokenSource;
        private readonly object reconnectLock = new object();
        private bool isReconnecting;
        private bool userRequestedDisconnect;

        public Task<Result> Connect()
        {
            this.userRequestedDisconnect = false;
            return this.ConnectInternal();
        }

        public async Task Disconnect()
        {
            Logger.Log(LogLevel.Debug, "VConnect Service - Disconnect requested");

            this.userRequestedDisconnect = true;
            this.StopWatchdog();

            await this.DisconnectSocket();

            this.IsConnected = false;
            this.ConnectedAddress = null;
            this.ClearCaches();
        }

        /// <summary>
        /// Every trigger in the active profile.
        /// </summary>
        public async Task<IEnumerable<VConnectTrigger>> GetTriggers(bool forceRefresh = false)
        {
            try
            {
                if (this.IsConnected && (forceRefresh || this.triggerCacheExpiration <= DateTimeOffset.Now || this.triggerCache.Count == 0))
                {
                    VConnectPacket response = await this.SendRequest(new VConnectPacket(TriggerListRequest));
                    if (response != null && string.Equals(response.messageType, TriggerListResponse) && response.data != null &&
                        response.data.TryGetValue("triggers", out JToken triggers) && triggers is JArray)
                    {
                        this.triggerCache = ((JArray)triggers).ToTypedArray<VConnectTrigger>().Where(t => t != null).ToList();
                        this.triggerCacheExpiration = DateTimeOffset.Now.AddMinutes(MaxCacheMinutes);

                        Logger.Log(LogLevel.Debug, "VConnect Service - " + this.triggerCache.Count + " trigger(s): " +
                            string.Join(", ", this.triggerCache.Select(t => t.uid + " \"" + t.name + "\"")));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return this.triggerCache.ToList();
        }

        /// <summary>
        /// Fires a trigger, the same as clicking Test on it in VConnect. The reply only says the
        /// trigger was found and started, not that the graph it fired has finished.
        /// </summary>
        /// <param name="uid">The trigger uid, checked first when both are given</param>
        /// <param name="name">The trigger name, used when no uid is available</param>
        public async Task<Result> ActivateTrigger(string uid, string name)
        {
            if (string.IsNullOrEmpty(uid) && string.IsNullOrEmpty(name))
            {
                return new Result(Resources.VConnectActionMissingTrigger);
            }

            try
            {
                JObject data = new JObject();
                if (!string.IsNullOrEmpty(uid))
                {
                    data["uid"] = uid;
                }
                if (!string.IsNullOrEmpty(name))
                {
                    data["name"] = name;
                }

                VConnectPacket response = await this.SendRequest(new VConnectPacket(TriggerActivateRequest, data));
                if (response == null)
                {
                    return new Result(Resources.VConnectRequestTimedOut);
                }

                if (string.Equals(response.messageType, ErrorResponse))
                {
                    return new Result(response.GetErrorMessage() ?? Resources.VConnectConnectionFailed);
                }

                if (response.data != null && response.data.TryGetValue("success", out JToken success) && !success.Value<bool>())
                {
                    string error = response.data.TryGetValue("error", out JToken value) && value.Type != JTokenType.Null ? value.ToString() : null;
                    return new Result(error ?? Resources.VConnectConnectionFailed);
                }

                return new Result();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return new Result(Resources.VConnectConnectionFailed);
        }

        /// <summary>
        /// Every asset in the library. Screenshots are left out, because VConnect may have to render a
        /// fresh snapshot per 3D or VRM asset to produce them.
        /// </summary>
        public async Task<IEnumerable<VConnectAsset>> GetAssets(bool forceRefresh = false)
        {
            try
            {
                if (this.IsConnected && (forceRefresh || this.assetCacheExpiration <= DateTimeOffset.Now || this.assetCache.Count == 0))
                {
                    JObject data = new JObject();
                    data["includeScreenshots"] = false;

                    VConnectPacket response = await this.SendRequest(new VConnectPacket(AssetListRequest, data));
                    if (response != null && string.Equals(response.messageType, AssetListResponse) && response.data != null &&
                        response.data.TryGetValue("assets", out JToken assets) && assets is JArray)
                    {
                        this.assetCache = ((JArray)assets).ToTypedArray<VConnectAsset>().Where(a => a != null).ToList();
                        this.assetCacheExpiration = DateTimeOffset.Now.AddMinutes(MaxCacheMinutes);

                        Logger.Log(LogLevel.Debug, "VConnect Service - " + this.assetCache.Count + " asset(s): " +
                            string.Join(", ", this.assetCache.Select(a => a.uid + " \"" + a.name + "\" " + a.type)));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return this.assetCache.ToList();
        }

        /// <summary>
        /// Details for a single asset, optionally with its preview image.
        /// </summary>
        public async Task<VConnectAsset> GetAssetInfo(string uid, bool includeScreenshot)
        {
            if (string.IsNullOrEmpty(uid))
            {
                return null;
            }

            try
            {
                JObject data = new JObject();
                data["uid"] = uid;
                data["includeScreenshot"] = includeScreenshot;

                VConnectPacket response = await this.SendRequest(new VConnectPacket(AssetInfoRequest, data));
                if (response != null && string.Equals(response.messageType, AssetInfoResponse) && response.data != null)
                {
                    return response.data.ToObject<VConnectAsset>();
                }

                if (response != null && string.Equals(response.messageType, ErrorResponse))
                {
                    Logger.Log(LogLevel.Error, "VConnect Service - Asset " + uid + " lookup failed: " + response.GetErrorMessage());
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return null;
        }

        /// <summary>
        /// Sends a custom message for an On WebSocket Receive node to pick up, matched on channel.
        /// </summary>
        public async Task SendCustomMessage(string channel, IEnumerable<JToken> arguments)
        {
            if (string.IsNullOrEmpty(channel))
            {
                return;
            }

            try
            {
                JArray values = new JArray();
                if (arguments != null)
                {
                    foreach (JToken argument in arguments)
                    {
                        values.Add(argument ?? JValue.CreateNull());
                    }
                }

                JObject data = new JObject();
                data["channel"] = channel;
                data["data"] = values;

                await this.SendPacket(new VConnectPacket(CustomMessageType, data));
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public void ClearCaches()
        {
            this.triggerCache.Clear();
            this.triggerCacheExpiration = DateTimeOffset.MinValue;
            this.assetCache.Clear();
            this.assetCacheExpiration = DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Turns one line of user-entered text into the value that goes into the custom message array.
        /// Anything that parses as JSON in its own right is sent as that value, so a graph node
        /// expecting a number or a flag gets one, and everything else is sent as a plain string.
        /// </summary>
        public static JToken ParseCustomMessageArgument(string value)
        {
            if (value == null)
            {
                return JValue.CreateNull();
            }

            string trimmed = value.Trim();
            if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[' || trimmed[0] == '"' ||
                trimmed[0] == '-' || char.IsDigit(trimmed[0]) ||
                string.Equals(trimmed, "true") || string.Equals(trimmed, "false") || string.Equals(trimmed, "null")))
            {
                try
                {
                    return JToken.Parse(trimmed);
                }
                catch (Exception)
                {
                    // Not JSON after all, so it is just text that happened to start with a digit or a brace.
                }
            }
            return new JValue(value);
        }

        private async Task<Result> ConnectInternal()
        {
            int port = ChannelSession.Settings.VConnectPortNumber;
            if (port <= 0 || port > 65535)
            {
                Logger.Log(LogLevel.Error, "VConnect Service - " + port + " is not a usable port number");
                return new Result(Resources.VConnectPortNumberInvalid);
            }

            // A path that has already answered is tried on its own first, so a reconnect does not walk
            // the whole list again.
            IEnumerable<string> paths = string.IsNullOrEmpty(this.resolvedPath) ?
                EndpointPaths : new string[] { this.resolvedPath }.Concat(EndpointPaths.Where(p => !string.Equals(p, this.resolvedPath)));

            foreach (string path in paths)
            {
                string address = string.Format(websocketAddressFormat, port, path);
                if (await this.ConnectToEndpoint(address, path))
                {
                    return new Result();
                }
            }

            return new Result(string.Format(Resources.VConnectConnectionFailedToPort, port));
        }

        private async Task<bool> ConnectToEndpoint(string address, string path)
        {
            try
            {
                Logger.Log(LogLevel.Debug, "VConnect Service - Opening " + address);

                await this.DisconnectSocket();

                VConnectWebSocket socket = new VConnectWebSocket();
                socket.PacketReceived += Websocket_PacketReceived;
                this.websocket = socket;

                if (!await socket.Connect(address))
                {
                    Logger.Log(LogLevel.Error, "VConnect Service - WebSocket handshake failed against " + address);
                    await this.DisconnectSocket();
                    return false;
                }

                this.IsConnected = true;
                this.ConnectedAddress = address;
                this.resolvedPath = path;

                // Nothing in the protocol identifies the server, so the trigger list doubles as the
                // proof that whatever is on that port is actually VConnect rather than something else
                // that accepted the handshake. The expiration is the tell, because an empty profile
                // legitimately answers with an empty list.
                this.ClearCaches();
                await this.GetTriggers(forceRefresh: true);
                if (this.triggerCacheExpiration == DateTimeOffset.MinValue)
                {
                    Logger.Log(LogLevel.Error, "VConnect Service - Connected to " + address + " but no trigger list came back, so whatever is on that port is not answering as VConnect");
                    this.IsConnected = false;
                    this.ConnectedAddress = null;
                    await this.DisconnectSocket();
                    return false;
                }

                socket.OnDisconnectOccurred += Websocket_OnDisconnectOccurred;
                this.StartWatchdog();

                ServiceManager.Get<ITelemetryService>().TrackService("VConnect");

                Logger.Log(LogLevel.Information, "VConnect Service - Connected at " + address);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "VConnect Service - Connection to " + address + " failed: " + ex.GetBaseException().Message);
                this.IsConnected = false;
                this.ConnectedAddress = null;
                await this.DisconnectSocket();
            }
            return false;
        }

        /// <summary>
        /// Sends a request and waits for the reply carrying the same requestID back.
        /// </summary>
        private async Task<VConnectPacket> SendRequest(VConnectPacket packet)
        {
            TaskCompletionSource<VConnectPacket> completionSource = new TaskCompletionSource<VConnectPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.pendingRequests[packet.requestID] = completionSource;

            try
            {
                if (!await this.SendPacket(packet))
                {
                    return null;
                }

                Task completed = await Task.WhenAny(completionSource.Task, Task.Delay(TimeSpan.FromSeconds(RequestTimeoutSeconds)));
                if (ReferenceEquals(completed, completionSource.Task))
                {
                    return await completionSource.Task;
                }

                Logger.Log(LogLevel.Error, "VConnect Service - No reply to " + packet.messageType + " within " + RequestTimeoutSeconds + "s");
            }
            finally
            {
                this.pendingRequests.TryRemove(packet.requestID, out _);
            }
            return null;
        }

        private async Task<bool> SendPacket(VConnectPacket packet)
        {
            VConnectWebSocket socket = this.websocket;
            if (socket == null || !socket.IsOpen())
            {
                return false;
            }

            await socket.Send(packet);
            return true;
        }

        private async void Websocket_PacketReceived(object sender, VConnectPacket packet)
        {
            try
            {
                // A reply carries the requestID the request was sent with. Broadcast events carry a
                // fresh server-generated one, which never matches anything we are waiting on.
                if (!string.IsNullOrEmpty(packet.requestID) &&
                    this.pendingRequests.TryGetValue(packet.requestID, out TaskCompletionSource<VConnectPacket> completionSource))
                {
                    completionSource.TrySetResult(packet);
                    return;
                }

                if (string.Equals(packet.messageType, ErrorResponse))
                {
                    Logger.Log(LogLevel.Error, "VConnect Service - " + (packet.GetErrorMessage() ?? "Unspecified error"));
                    return;
                }

                await this.ProcessBroadcastEvent(packet);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async Task ProcessBroadcastEvent(VConnectPacket packet)
        {
            JObject data = packet.data ?? new JObject();

            if (string.Equals(packet.messageType, TriggerActivatedEvent))
            {
                CommandParametersModel parameters = new CommandParametersModel();
                AddTriggerIdentifiers(parameters, GetString(data, "uid"), GetString(data, "name"));

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VConnectTriggerActivated, parameters);
            }
            else if (string.Equals(packet.messageType, TriggerEndedEvent))
            {
                CommandParametersModel parameters = new CommandParametersModel();
                AddTriggerIdentifiers(parameters, GetString(data, "uid"), GetString(data, "name"));
                parameters.SpecialIdentifiers["vconnecttriggersuccess"] = GetBool(data, "success").ToString();
                parameters.SpecialIdentifiers["vconnecttriggererror"] = GetString(data, "error");

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VConnectTriggerEnded, parameters);
            }
            else if (string.Equals(packet.messageType, AssetSpawnEvent))
            {
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VConnectAssetSpawned, BuildAssetParameters(data));
            }
            else if (string.Equals(packet.messageType, AssetDespawnEvent))
            {
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VConnectAssetDespawned, BuildAssetParameters(data));
            }
            else if (string.Equals(packet.messageType, AssetHitEvent))
            {
                CommandParametersModel parameters = BuildAssetParameters(data);
                AddVectorIdentifiers(parameters, "vconnecthitpoint", data["point"]);
                AddVectorIdentifiers(parameters, "vconnecthitnormal", data["normal"]);
                AddVectorIdentifiers(parameters, "vconnecthitvelocity", data["relativeVelocity"]);
                parameters.SpecialIdentifiers["vconnecthitforce"] = GetDouble(data, "forceEstimate").ToString(CultureInfo.InvariantCulture);

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VConnectAssetHit, parameters);
            }
            else if (string.Equals(packet.messageType, CustomMessageType))
            {
                CommandParametersModel parameters = new CommandParametersModel();
                parameters.SpecialIdentifiers["vconnectmessagechannel"] = GetString(data, "channel");

                JArray arguments = data["data"] as JArray;
                parameters.SpecialIdentifiers["vconnectmessagedata"] = (arguments ?? new JArray()).ToString(Newtonsoft.Json.Formatting.None);
                parameters.SpecialIdentifiers["vconnectmessageargumentcount"] = (arguments?.Count ?? 0).ToString();

                if (arguments != null)
                {
                    for (int i = 0; i < arguments.Count; i++)
                    {
                        JToken argument = arguments[i];

                        // Objects and arrays keep their JSON, everything else reads as its plain value
                        // so a string argument does not arrive wrapped in quotes.
                        parameters.SpecialIdentifiers["vconnectmessageargument" + (i + 1)] =
                            argument is JValue ? argument.ToString() : argument.ToString(Newtonsoft.Json.Formatting.None);
                    }
                }

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VConnectMessageReceived, parameters);
            }
        }

        private static CommandParametersModel BuildAssetParameters(JObject data)
        {
            CommandParametersModel parameters = new CommandParametersModel();
            parameters.SpecialIdentifiers["vconnectassetuid"] = GetString(data, "itemUid");
            parameters.SpecialIdentifiers["vconnectassetname"] = GetString(data, "itemName");
            AddTriggerIdentifiers(parameters, GetString(data, "triggerUid"), null);
            parameters.SpecialIdentifiers["vconnecttriggernodeuid"] = GetString(data, "triggerNodeUid");
            return parameters;
        }

        private static void AddTriggerIdentifiers(CommandParametersModel parameters, string uid, string name)
        {
            parameters.SpecialIdentifiers["vconnecttriggeruid"] = uid ?? string.Empty;
            parameters.SpecialIdentifiers["vconnecttriggername"] = name ?? string.Empty;
        }

        private static void AddVectorIdentifiers(CommandParametersModel parameters, string prefix, JToken vector)
        {
            parameters.SpecialIdentifiers[prefix + "x"] = GetVectorComponent(vector, "x");
            parameters.SpecialIdentifiers[prefix + "y"] = GetVectorComponent(vector, "y");
            parameters.SpecialIdentifiers[prefix + "z"] = GetVectorComponent(vector, "z");
        }

        private static string GetVectorComponent(JToken vector, string component)
        {
            if (vector is JObject && ((JObject)vector).TryGetValue(component, out JToken value) && value.Type != JTokenType.Null)
            {
                return value.Value<double>().ToString(CultureInfo.InvariantCulture);
            }
            return "0";
        }

        private static string GetString(JObject data, string name)
        {
            if (data.TryGetValue(name, out JToken value) && value.Type != JTokenType.Null)
            {
                return value.ToString();
            }
            return string.Empty;
        }

        private static bool GetBool(JObject data, string name)
        {
            return data.TryGetValue(name, out JToken value) && value.Type == JTokenType.Boolean && value.Value<bool>();
        }

        private static double GetDouble(JObject data, string name)
        {
            if (data.TryGetValue(name, out JToken value) && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer))
            {
                return value.Value<double>();
            }
            return 0;
        }

        private async void Websocket_OnDisconnectOccurred(object sender, System.Net.WebSockets.WebSocketCloseStatus e)
        {
            await this.HandleConnectionLost();
        }

        /// <summary>
        /// Polls the socket rather than trusting the disconnect event, which WebSocketBase only raises
        /// when the close was not a normal one. Turning VConnect's WebSocket server off closes the
        /// connection cleanly, and toggling that server is a routine thing for a user to do.
        /// </summary>
        private void StartWatchdog()
        {
            this.StopWatchdog();

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            this.watchdogCancellationTokenSource = cancellationTokenSource;
            CancellationToken token = cancellationTokenSource.Token;

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(ConnectionCheckMilliseconds, token);

                        VConnectWebSocket socket = this.websocket;
                        if (this.IsConnected && (socket == null || !socket.IsOpen()))
                        {
                            Logger.Log(LogLevel.Debug, "VConnect Service - Watchdog found the socket closed, most likely VConnect's WebSocket server was turned off or VConnect was closed");
                            await this.HandleConnectionLost();
                            return;
                        }
                    }
                }
                catch (TaskCanceledException) { }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Logger.Log(ex); }
            });
        }

        private void StopWatchdog()
        {
            CancellationTokenSource cancellationTokenSource = this.watchdogCancellationTokenSource;
            this.watchdogCancellationTokenSource = null;
            if (cancellationTokenSource != null)
            {
                try { cancellationTokenSource.Cancel(); }
                catch { }
                cancellationTokenSource.Dispose();
            }
        }

        private async Task HandleConnectionLost()
        {
            lock (this.reconnectLock)
            {
                // The watchdog and the disconnect event can both notice the same drop.
                if (this.isReconnecting || this.userRequestedDisconnect)
                {
                    return;
                }
                this.isReconnecting = true;
            }

            try
            {
                this.StopWatchdog();
                this.IsConnected = false;

                Logger.Log(LogLevel.Error, "VConnect Service - Connection to " + (this.ConnectedAddress ?? "VConnect") + " lost, starting reconnect loop");
                this.ConnectedAddress = null;

                ChannelSession.DisconnectionOccurred(Resources.VConnect);

                Result result = new Result(false);
                int attempt = 0;
                while (!result.Success && !this.userRequestedDisconnect)
                {
                    await this.DisconnectSocket();

                    await Task.Delay(ReconnectDelayMilliseconds);

                    attempt++;
                    Logger.Log(LogLevel.Debug, "VConnect Service - Reconnect attempt " + attempt);

                    result = await this.ConnectInternal();
                }

                if (result.Success)
                {
                    ChannelSession.ReconnectionOccurred(Resources.VConnect);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                lock (this.reconnectLock)
                {
                    this.isReconnecting = false;
                }
            }
        }

        private async Task DisconnectSocket()
        {
            VConnectWebSocket socket = this.websocket;
            this.websocket = null;

            // Anything still waiting on a reply will never get one now.
            foreach (TaskCompletionSource<VConnectPacket> completionSource in this.pendingRequests.Values)
            {
                completionSource.TrySetResult(null);
            }
            this.pendingRequests.Clear();

            if (socket != null)
            {
                socket.OnDisconnectOccurred -= Websocket_OnDisconnectOccurred;
                socket.PacketReceived -= Websocket_PacketReceived;

                try
                {
                    await socket.Disconnect();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }
    }
}
