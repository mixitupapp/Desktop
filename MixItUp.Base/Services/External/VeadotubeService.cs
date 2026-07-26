using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class VeadotubeInstanceInfo
    {
        public string name { get; set; }
        public string id { get; set; }
        public string version { get; set; }
        public string language { get; set; }
        public string server { get; set; }
        public long time { get; set; }

        /// <summary>
        /// The instance id is "&lt;type&gt;-&lt;launch timestamp&gt;-&lt;process id&gt;", and the leading
        /// segment doubles as the node id for every node the instance exposes.
        /// </summary>
        public string InstanceType { get { return this.id?.Split('-').FirstOrDefault(); } }

        /// <summary>
        /// veadotube mini 2.0a and older leave the version out entirely or send the literal string
        /// "undefined". Toggle state and every push-to-talk operation are broken on those builds.
        /// </summary>
        public bool IsPre2Point1 { get { return string.IsNullOrEmpty(this.version) || string.Equals(this.version, "undefined", StringComparison.OrdinalIgnoreCase); } }
    }

    public class VeadotubeNodeEntry
    {
        public string type { get; set; }
        public string id { get; set; }
        public string name { get; set; }
    }

    public class VeadotubeState
    {
        public string id { get; set; }
        public string name { get; set; }
        public string thumbHash { get; set; }

        public string DisplayName
        {
            get
            {
                return !string.IsNullOrEmpty(this.name) ? this.name : this.id;
            }
        }

        // The action editor binds these into a ComboBox, and without this a screen reader and UI
        // Automation both read the row as the class name rather than the state.
        public override string ToString() { return this.DisplayName; }
    }

    /// <summary>
    /// A frame on the nodes channel. The payload is deliberately untyped because its shape depends on
    /// the node: the state node sends an object, the push-to-talk node sends a bare true or false.
    /// </summary>
    public class VeadotubeNodeFrame
    {
        public string @event { get; set; }
        public string type { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public JToken payload { get; set; }
        public List<VeadotubeNodeEntry> entries { get; set; }
    }

    public class VeadotubeStatePayload
    {
        public string @event { get; set; }
        public string state { get; set; }
        public List<VeadotubeState> states { get; set; }
    }

    public class VeadotubeWebSocket : ClientWebSocketBase
    {
        public event EventHandler<VeadotubeInstanceInfo> OnInstanceInfoReceived = delegate { };
        public event EventHandler<VeadotubeNodeFrame> OnNodeFrameReceived = delegate { };

        public const string InstanceChannel = "instance";
        public const string NodesChannel = "nodes";

        public async Task SendFrame(string channel, object contents)
        {
            string frame = channel + ":" + JSONSerializerHelper.SerializeToString(contents);
            Logger.Log(LogLevel.Debug, "veadotube Packet Sent - " + frame);
            await this.Send(frame);
        }

        protected override Task ProcessReceivedPacket(string packet)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packet))
                {
                    return Task.CompletedTask;
                }

                packet = Sanitize(packet);
                if (string.IsNullOrWhiteSpace(packet))
                {
                    return Task.CompletedTask;
                }

                Logger.Log(LogLevel.Debug, "veadotube Packet Received - " + packet);

                // Frames are a channel name, a colon, then JSON, so the whole frame is not valid JSON
                // on its own. Only "nodes" and "instance" exist today, so route on the name rather
                // than assuming there will never be a third.
                int separator = packet.IndexOf(':');
                if (separator <= 0)
                {
                    return Task.CompletedTask;
                }

                string channel = packet.Substring(0, separator).Trim();
                string json = packet.Substring(separator + 1).Trim();
                if (string.IsNullOrEmpty(json))
                {
                    return Task.CompletedTask;
                }

                if (string.Equals(channel, InstanceChannel, StringComparison.OrdinalIgnoreCase))
                {
                    VeadotubeInstanceInfo info = JSONSerializerHelper.DeserializeFromString<VeadotubeInstanceInfo>(json);
                    if (info != null)
                    {
                        this.OnInstanceInfoReceived(this, info);
                    }
                }
                else if (string.Equals(channel, NodesChannel, StringComparison.OrdinalIgnoreCase))
                {
                    VeadotubeNodeFrame frame = JSONSerializerHelper.DeserializeFromString<VeadotubeNodeFrame>(json);
                    if (frame != null)
                    {
                        this.OnNodeFrameReceived(this, frame);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log("veadotube Service - Failed Packet Processing: " + packet);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Older veadotube builds trail stray null bytes after the JSON. Stripping control characters
        /// costs nothing on current builds and keeps users who have not updated working.
        /// </summary>
        private static string Sanitize(string packet)
        {
            StringBuilder builder = new StringBuilder(packet.Length);
            foreach (char c in packet)
            {
                if (c > 0x1F && (c < 0x7F || c > 0x9F))
                {
                    builder.Append(c);
                }
            }
            return builder.ToString().Trim();
        }
    }

    /// <summary>
    /// https://veado.tube/docs/tech/api/
    /// </summary>
    /// <remarks>
    /// veadotube has no request id, so the reference clients all carry a FIFO queue of pending
    /// requests keyed by node and inner event to match responses back to the call that caused them.
    /// None of that is needed here. Our actions are fire and forget, the subscription is the single
    /// source of truth for current state, and only the state list needs a reply, with at most one
    /// outstanding at a time. Please do not helpfully add the queue back.
    /// </remarks>
    public class VeadotubeService : IExternalService
    {
        public const string InstancesFolder = @".veadotube\instances";

        /// <summary>
        /// veadotube rewrites its instance file as a heartbeat, so anything older than this is a dead
        /// instance whose file was never cleaned up.
        /// </summary>
        public const int InstanceStaleSeconds = 10;

        public const int MaxCacheDuration = 30;

        public const string StateEventsNodeType = "stateEvents";
        public const string BooleanNodeType = "boolean";

        /// <summary>
        /// A stable token rather than a fresh GUID, so reconnecting is idempotent and logs stay readable.
        /// </summary>
        public const string SubscriptionToken = "mixitup";

        private const string websocketAddress = "ws://{0}?n=Mix%20It%20Up";

        private const int ConnectTimeoutSeconds = 10;
        private const int ReconnectDelayMilliseconds = 5000;

        /// <summary>
        /// How often the watchdog checks that the socket is still open. See StartWatchdog for why we
        /// cannot rely on the disconnect event alone.
        /// </summary>
        private const int ConnectionCheckMilliseconds = 2000;

        /// <summary>
        /// How long after one of our own writes an inbound frame reporting that same value is treated
        /// as the echo of the write rather than something the streamer did.
        /// </summary>
        private const int SelfWriteSuppressionMilliseconds = 1000;

        public string Name { get { return Resources.Veadotube; } }

        public bool IsConnected { get; private set; }

        /// <summary>The instance we are attached to, or null when not connected.</summary>
        public VeadotubeInstanceInfo InstanceInfo { get; private set; }

        /// <summary>The avatar state veadotube last reported, or null if we have not seen one yet.</summary>
        public VeadotubeState CurrentState { get; private set; }

        /// <summary>The push-to-talk gate, or null if veadotube has not reported it yet.</summary>
        public bool? PushToTalkActive { get; private set; }

        private VeadotubeWebSocket websocket;

        private string stateEventsNodeID;
        private string booleanNodeID;

        private List<VeadotubeState> stateCache = new List<VeadotubeState>();
        private DateTimeOffset stateCacheExpiration = DateTimeOffset.MinValue;

        private TaskCompletionSource<VeadotubeInstanceInfo> instanceInfoCompletionSource;
        private TaskCompletionSource<IEnumerable<VeadotubeNodeEntry>> nodeListCompletionSource;
        private TaskCompletionSource<IEnumerable<VeadotubeState>> stateListCompletionSource;

        private string lastWrittenStateID;
        private DateTimeOffset lastStateWrite = DateTimeOffset.MinValue;

        private bool? lastWrittenPushToTalk;
        private DateTimeOffset lastPushToTalkWrite = DateTimeOffset.MinValue;

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
            this.userRequestedDisconnect = true;
            this.StopWatchdog();

            if (this.websocket != null)
            {
                // Drop the subscriptions before closing so veadotube is not left holding them.
                await this.Unsubscribe();
            }

            await this.DisconnectSocket();

            this.IsConnected = false;
            this.InstanceInfo = null;
            this.CurrentState = null;
            this.PushToTalkActive = null;
            this.ClearCaches();

            // The auto-connect flag deliberately lives on the view model's commands, not here.
            // Shutdown disconnects every connected service, so clearing it here would turn
            // auto-connect off every time the app closed cleanly.
        }

        public async Task<IEnumerable<VeadotubeState>> GetStates()
        {
            try
            {
                if (this.IsConnected && (this.stateCacheExpiration <= DateTimeOffset.Now || this.stateCache.Count == 0))
                {
                    IEnumerable<VeadotubeState> states = await this.RequestStateList();
                    if (states != null)
                    {
                        return states;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return this.stateCache.ToList();
        }

        public async Task SetState(string stateID)
        {
            this.MarkStateWrite(stateID);
            await this.SendStatePayload("set", stateID);
        }

        public async Task PushState(string stateID)
        {
            this.MarkStateWrite(stateID);
            await this.SendStatePayload("push", stateID);
        }

        public async Task PopState(string stateID)
        {
            // Where a pop lands depends on veadotube's own stack, so we cannot predict the resulting
            // state id. Mark the write with no expected id and suppress on the window alone.
            this.MarkStateWrite(null);
            await this.SendStatePayload("pop", stateID);
        }

        public async Task ToggleState(string stateID)
        {
            // A toggle either turns the state on or reverts to whatever was underneath it, so the
            // resulting id is equally unpredictable.
            this.MarkStateWrite(null);
            await this.SendStatePayload("toggle", stateID);
        }

        /// <summary>
        /// Picks a state at random, never the one already showing. Switching to the state we are on
        /// looks broken to the streamer even though it did exactly what it was told.
        /// </summary>
        public async Task SetRandomState()
        {
            IEnumerable<VeadotubeState> states = await this.GetStates();
            if (states != null)
            {
                List<VeadotubeState> candidates = states.Where(s => !string.Equals(s.id, this.CurrentState?.id)).ToList();
                if (candidates.Count == 0)
                {
                    candidates = states.ToList();
                }

                if (candidates.Count > 0)
                {
                    await this.SetState(candidates[RandomHelper.GenerateRandomNumber(candidates.Count)].id);
                }
            }
        }

        /// <summary>
        /// Sets the push-to-talk gate, or toggles it when no value is given.
        /// </summary>
        public async Task SetPushToTalk(bool? value)
        {
            if (this.websocket == null || string.IsNullOrEmpty(this.booleanNodeID))
            {
                return;
            }

            JObject payload = new JObject();
            if (value.HasValue)
            {
                payload["event"] = "set";
                payload["value"] = value.Value;

                this.lastWrittenPushToTalk = value.Value;
                this.lastPushToTalkWrite = DateTimeOffset.Now;
            }
            else
            {
                payload["event"] = "toggle";

                // A toggle lands on the opposite of whatever we last saw, so we can still predict the
                // echo and keep it from firing the event.
                this.lastWrittenPushToTalk = this.PushToTalkActive.HasValue ? !this.PushToTalkActive.Value : (bool?)null;
                this.lastPushToTalkWrite = DateTimeOffset.Now;
            }

            await this.SendNodePayload(BooleanNodeType, this.booleanNodeID, payload);
        }

        public void ClearCaches()
        {
            this.stateCache.Clear();
            this.stateCacheExpiration = DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Finds the newest live instance and returns its server address, or null when veadotube is
        /// not running with its WebSocket server on.
        /// </summary>
        public string ResolveEndpoint()
        {
            if (ChannelSession.Settings != null && !string.IsNullOrEmpty(ChannelSession.Settings.VeadotubeManualAddress))
            {
                return ChannelSession.Settings.VeadotubeManualAddress.Trim();
            }

            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), InstancesFolder);
                if (!Directory.Exists(folder))
                {
                    return null;
                }

                long cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - InstanceStaleSeconds;
                VeadotubeInstanceInfo newest = null;

                // The files carry no extension, the filename is the instance id, so a *.json search
                // finds nothing. Read every file in the folder.
                foreach (string file in Directory.GetFiles(folder))
                {
                    try
                    {
                        VeadotubeInstanceInfo info = JSONSerializerHelper.DeserializeFromString<VeadotubeInstanceInfo>(File.ReadAllText(file));
                        if (info != null && info.time >= cutoff && !string.IsNullOrEmpty(info.server) && (newest == null || info.time > newest.time))
                        {
                            newest = info;
                        }
                    }
                    catch (Exception ex)
                    {
                        // veadotube may be part way through writing the file, so a failed parse here
                        // is routine rather than a problem worth shouting about.
                        Logger.Log(LogLevel.Debug, "Skipped veadotube instance file: " + ex.Message);
                    }
                }

                return newest?.server;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return null;
        }

        private async Task<Result> ConnectInternal()
        {
            try
            {
                string endpoint = this.ResolveEndpoint();
                if (string.IsNullOrEmpty(endpoint))
                {
                    return new Result(Resources.VeadotubeInstanceNotFound);
                }

                await this.DisconnectSocket();

                this.instanceInfoCompletionSource = new TaskCompletionSource<VeadotubeInstanceInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
                this.nodeListCompletionSource = new TaskCompletionSource<IEnumerable<VeadotubeNodeEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);

                this.websocket = new VeadotubeWebSocket();
                this.websocket.OnInstanceInfoReceived += Websocket_OnInstanceInfoReceived;
                this.websocket.OnNodeFrameReceived += Websocket_OnNodeFrameReceived;

                if (!await this.websocket.Connect(string.Format(websocketAddress, endpoint)))
                {
                    await this.DisconnectSocket();
                    return new Result(Resources.VeadotubeConnectionFailed);
                }

                // veadotube sends the instance info unprompted on connect, so it doubles as the ack.
                VeadotubeInstanceInfo info = await WaitFor(this.instanceInfoCompletionSource);
                if (info == null)
                {
                    await this.DisconnectSocket();
                    return new Result(Resources.VeadotubeConnectionFailed);
                }
                this.InstanceInfo = info;

                await this.websocket.SendFrame(VeadotubeWebSocket.NodesChannel, new { @event = "list" });

                IEnumerable<VeadotubeNodeEntry> nodes = await WaitFor(this.nodeListCompletionSource);
                if (nodes == null)
                {
                    await this.DisconnectSocket();
                    return new Result(Resources.VeadotubeConnectionFailed);
                }

                // Read the node ids off the entries. They are "mini" on veadotube mini but not on live
                // or the avatar editor, and hardcoding it is the bug every other client had to fix.
                this.stateEventsNodeID = nodes.FirstOrDefault(n => string.Equals(n.type, StateEventsNodeType, StringComparison.OrdinalIgnoreCase))?.id;
                this.booleanNodeID = nodes.FirstOrDefault(n => string.Equals(n.type, BooleanNodeType, StringComparison.OrdinalIgnoreCase))?.id;

                if (string.IsNullOrEmpty(this.stateEventsNodeID))
                {
                    await this.DisconnectSocket();
                    return new Result(Resources.VeadotubeConnectionFailed);
                }

                await this.Subscribe();

                this.IsConnected = true;
                this.websocket.OnDisconnectOccurred += Websocket_OnDisconnectOccurred;
                this.StartWatchdog();

                ServiceManager.Get<ITelemetryService>().TrackService("veadotube");

                return new Result();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await this.DisconnectSocket();
            }
            return new Result(Resources.VeadotubeConnectionFailed);
        }

        /// <summary>
        /// Subscribing returns the state list and the current value of each node straight away, which
        /// is where the initial cache comes from. The subscriptions have to be re-sent on every
        /// reconnect or events quietly stop arriving.
        /// </summary>
        private async Task Subscribe()
        {
            this.stateListCompletionSource = new TaskCompletionSource<IEnumerable<VeadotubeState>>(TaskCreationOptions.RunContinuationsAsynchronously);

            JObject listen = new JObject();
            listen["event"] = "listen";
            listen["token"] = SubscriptionToken;

            await this.SendNodePayload(StateEventsNodeType, this.stateEventsNodeID, listen);

            if (!string.IsNullOrEmpty(this.booleanNodeID))
            {
                await this.SendNodePayload(BooleanNodeType, this.booleanNodeID, listen);
            }
        }

        private async Task Unsubscribe()
        {
            try
            {
                JObject unlisten = new JObject();
                unlisten["event"] = "unlisten";
                unlisten["token"] = SubscriptionToken;

                if (!string.IsNullOrEmpty(this.stateEventsNodeID))
                {
                    await this.SendNodePayload(StateEventsNodeType, this.stateEventsNodeID, unlisten);
                }
                if (!string.IsNullOrEmpty(this.booleanNodeID))
                {
                    await this.SendNodePayload(BooleanNodeType, this.booleanNodeID, unlisten);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async Task<IEnumerable<VeadotubeState>> RequestStateList()
        {
            if (this.websocket == null || string.IsNullOrEmpty(this.stateEventsNodeID))
            {
                return null;
            }

            this.stateListCompletionSource = new TaskCompletionSource<IEnumerable<VeadotubeState>>(TaskCreationOptions.RunContinuationsAsynchronously);

            JObject payload = new JObject();
            payload["event"] = "list";
            await this.SendNodePayload(StateEventsNodeType, this.stateEventsNodeID, payload);

            return await WaitFor(this.stateListCompletionSource);
        }

        private async Task SendStatePayload(string action, string stateID)
        {
            if (this.websocket == null || string.IsNullOrEmpty(this.stateEventsNodeID) || string.IsNullOrEmpty(stateID))
            {
                return;
            }

            JObject payload = new JObject();
            payload["event"] = action;
            payload["state"] = stateID;

            await this.SendNodePayload(StateEventsNodeType, this.stateEventsNodeID, payload);
        }

        private async Task SendNodePayload(string type, string nodeID, JObject payload)
        {
            if (this.websocket == null || !this.websocket.IsOpen())
            {
                return;
            }

            JObject frame = new JObject();
            frame["event"] = "payload";
            frame["type"] = type;
            frame["id"] = nodeID;
            frame["payload"] = payload;

            await this.websocket.SendFrame(VeadotubeWebSocket.NodesChannel, frame);
        }

        private void MarkStateWrite(string stateID)
        {
            this.lastWrittenStateID = stateID;
            this.lastStateWrite = DateTimeOffset.Now;
        }

        private void Websocket_OnInstanceInfoReceived(object sender, VeadotubeInstanceInfo info)
        {
            this.InstanceInfo = info;
            this.instanceInfoCompletionSource?.TrySetResult(info);
        }

        private async void Websocket_OnNodeFrameReceived(object sender, VeadotubeNodeFrame frame)
        {
            try
            {
                if (string.Equals(frame.@event, "list", StringComparison.OrdinalIgnoreCase) && frame.entries != null)
                {
                    this.nodeListCompletionSource?.TrySetResult(frame.entries);
                    return;
                }

                if (!string.Equals(frame.@event, "payload", StringComparison.OrdinalIgnoreCase) || frame.payload == null)
                {
                    return;
                }

                if (string.Equals(frame.type, BooleanNodeType, StringComparison.OrdinalIgnoreCase))
                {
                    // The push-to-talk node sends its value as a bare true or false, not as an object.
                    if (frame.payload.Type == JTokenType.Boolean)
                    {
                        await this.ProcessPushToTalk(frame.payload.Value<bool>());
                    }
                    return;
                }

                if (string.Equals(frame.type, StateEventsNodeType, StringComparison.OrdinalIgnoreCase) && frame.payload.Type == JTokenType.Object)
                {
                    VeadotubeStatePayload payload = frame.payload.ToObject<VeadotubeStatePayload>();
                    if (payload == null)
                    {
                        return;
                    }

                    if (string.Equals(payload.@event, "list", StringComparison.OrdinalIgnoreCase) && payload.states != null)
                    {
                        this.stateCache = payload.states;
                        this.stateCacheExpiration = DateTimeOffset.Now.AddMinutes(MaxCacheDuration);
                        this.stateListCompletionSource?.TrySetResult(this.stateCache.ToList());
                    }
                    else if (string.Equals(payload.@event, "peek", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(payload.state))
                    {
                        await this.ProcessStateChange(payload.state);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async Task ProcessStateChange(string incomingStateID)
        {
            VeadotubeState previous = this.CurrentState;

            // Every write answers with a peek that is byte identical to the ones the subscription
            // pushes when the streamer clicks a state themselves, so there is nothing in the frame to
            // tell them apart. Dedupe on the id and ignore the echo of a write we just made.
            // A null lastWrittenStateID means we wrote something whose resulting state we could not
            // predict, so anything arriving inside the window is ours.
            bool withinWriteWindow = this.lastStateWrite.AddMilliseconds(SelfWriteSuppressionMilliseconds) > DateTimeOffset.Now;
            bool isSelfWrite = withinWriteWindow &&
                (this.lastWrittenStateID == null || string.Equals(this.lastWrittenStateID, incomingStateID));
            bool isSameState = string.Equals(previous?.id, incomingStateID);

            VeadotubeState state = await this.ResolveState(incomingStateID);
            this.CurrentState = state;

            if (isSameState || isSelfWrite)
            {
                return;
            }

            CommandParametersModel parameters = new CommandParametersModel();
            parameters.SpecialIdentifiers["veadotubestateid"] = state.id ?? string.Empty;
            parameters.SpecialIdentifiers["veadotubestatename"] = state.DisplayName ?? string.Empty;
            parameters.SpecialIdentifiers["veadotubepreviousstateid"] = previous?.id ?? string.Empty;
            parameters.SpecialIdentifiers["veadotubepreviousstatename"] = previous?.DisplayName ?? string.Empty;

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeadotubeAvatarStateChanged, parameters);
        }

        /// <summary>
        /// Looks the reported state up by id first, then by name. veadotube reports an id, but one
        /// reference client reads it as a name, and covering both costs a single extra lookup.
        /// </summary>
        private async Task<VeadotubeState> ResolveState(string incomingStateID)
        {
            VeadotubeState state = this.FindState(incomingStateID);
            if (state == null)
            {
                // The streamer can add a state without telling us, so refresh once before giving up.
                await this.RequestStateList();
                state = this.FindState(incomingStateID);
            }
            return state ?? new VeadotubeState() { id = incomingStateID, name = incomingStateID };
        }

        private VeadotubeState FindState(string incomingStateID)
        {
            List<VeadotubeState> states = this.stateCache;
            return states.FirstOrDefault(s => string.Equals(s.id, incomingStateID)) ??
                states.FirstOrDefault(s => string.Equals(s.name, incomingStateID));
        }

        private async Task ProcessPushToTalk(bool value)
        {
            bool? previous = this.PushToTalkActive;
            this.PushToTalkActive = value;

            // The first value arrives with the subscription and is the baseline, not a change.
            if (!previous.HasValue || previous.Value == value)
            {
                return;
            }

            bool isSelfWrite = this.lastWrittenPushToTalk.HasValue && this.lastWrittenPushToTalk.Value == value &&
                this.lastPushToTalkWrite.AddMilliseconds(SelfWriteSuppressionMilliseconds) > DateTimeOffset.Now;
            if (isSelfWrite)
            {
                return;
            }

            CommandParametersModel parameters = new CommandParametersModel();
            parameters.SpecialIdentifiers["veadotubepushtotalk"] = value.ToString();

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeadotubePushToTalkChanged, parameters);
        }

        private async void Websocket_OnDisconnectOccurred(object sender, System.Net.WebSockets.WebSocketCloseStatus e)
        {
            await this.HandleConnectionLost();
        }

        /// <summary>
        /// Polls the socket rather than trusting the disconnect event.
        /// </summary>
        /// <remarks>
        /// WebSocketBase only raises OnDisconnectOccurred when the close was not a normal closure
        /// (WebSocketBase.cs, end of the receive loop). Turning veadotube's WebSocket server off closes
        /// the connection cleanly, so that event never fires, and toggling that server is a routine
        /// thing for a user to do. Without this the service sits believing it is still connected and
        /// the reconnect loop is never entered.
        /// </remarks>
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

                        VeadotubeWebSocket socket = this.websocket;
                        if (this.IsConnected && (socket == null || !socket.IsOpen()))
                        {
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

                ChannelSession.DisconnectionOccurred(Resources.Veadotube);

                Result result = new Result(false);
                while (!result.Success && !this.userRequestedDisconnect)
                {
                    await this.DisconnectSocket();

                    await Task.Delay(ReconnectDelayMilliseconds);

                    // ConnectInternal resolves the endpoint again on every attempt. veadotube binds a
                    // new ephemeral port each launch and on every server toggle, and one of those is
                    // the most likely reason we dropped, so reusing the old address would never work.
                    result = await this.ConnectInternal();
                }

                if (result.Success)
                {
                    ChannelSession.ReconnectionOccurred(Resources.Veadotube);
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
            if (this.websocket != null)
            {
                VeadotubeWebSocket socket = this.websocket;
                this.websocket = null;

                socket.OnDisconnectOccurred -= Websocket_OnDisconnectOccurred;
                socket.OnInstanceInfoReceived -= Websocket_OnInstanceInfoReceived;
                socket.OnNodeFrameReceived -= Websocket_OnNodeFrameReceived;

                try
                {
                    await socket.Disconnect();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }

            this.stateEventsNodeID = null;
            this.booleanNodeID = null;
        }

        private static async Task<T> WaitFor<T>(TaskCompletionSource<T> completionSource)
            where T : class
        {
            Task completed = await Task.WhenAny(completionSource.Task, Task.Delay(TimeSpan.FromSeconds(ConnectTimeoutSeconds)));
            if (ReferenceEquals(completed, completionSource.Task))
            {
                return await completionSource.Task;
            }
            return null;
        }
    }
}
