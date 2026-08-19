using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    /// <summary>
    /// https://rahisaurus.itch.io/rahituber
    /// </summary>
    /// <remarks>
    /// The whole integration surface is one route. RahiTuber listens on
    /// GET /state?[state,active], where state is either the state's name as a JSON string or its
    /// zero-based index as a number, and active is 1 to trigger the state or 0 to stop it. The query
    /// string is the JSON array itself rather than a set of named parameters. Every other path
    /// answers 400, which is what tells us the listener on that port is RahiTuber rather than
    /// something else. There is no way to ask it what states exist and nothing is ever pushed back,
    /// so the action editor takes the state as text and there are no events to raise.
    /// </remarks>
    public class RahiTuberService : IExternalService
    {
        /// <summary>
        /// The port RahiTuber's Integration tab starts on. The services page carries a port box for
        /// the case where it has been moved.
        /// </summary>
        public const int DefaultPortNumber = 8000;

        private const string addressFormat = "http://127.0.0.1:{0}";

        private const string stateRoute = "/state";

        private const int RequestTimeoutSeconds = 5;
        private const int ReconnectDelayMilliseconds = 5000;

        /// <summary>
        /// How often the connection is checked. Nothing is held open between requests, so the only
        /// way to notice RahiTuber closing is to ask. The check is a request to the root path, which
        /// RahiTuber rejects without touching any state.
        /// </summary>
        private const int ConnectionCheckMilliseconds = 15000;

        public string Name { get { return Resources.RahiTuber; } }

        public bool IsConnected { get; private set; }

        /// <summary>The address currently in use, or null when not connected.</summary>
        public string ConnectedAddress { get; private set; }

        private AdvancedHttpClient client;

        private CancellationTokenSource watchdogCancellationTokenSource;
        private readonly object reconnectLock = new object();
        private bool isReconnecting;
        private bool userRequestedDisconnect;

        public Task<Result> Connect()
        {
            this.userRequestedDisconnect = false;
            return this.ConnectInternal();
        }

        public Task Disconnect()
        {
            Logger.Log(LogLevel.Debug, "RahiTuber Service - Disconnect requested");

            this.userRequestedDisconnect = true;
            this.StopWatchdog();
            this.DisposeClient();

            this.IsConnected = false;
            this.ConnectedAddress = null;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Triggers or stops a state, the same as pressing and releasing its hotkey. A Toggle state
        /// flips on each trigger and a Permanent state applies on the trigger alone, so only a state
        /// set to While Held needs the matching stop.
        /// </summary>
        /// <param name="state">The state's name, or its zero-based index</param>
        /// <param name="active">True to trigger the state, false to stop it</param>
        public async Task<Result> SetState(string state, bool active)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return new Result(Resources.RahiTuberActionMissingState);
            }

            // Both read once. A reconnect running alongside this replaces the client and blanks the
            // address, and half of each is not something to build a request out of.
            AdvancedHttpClient httpClient = this.client;
            string address = this.ConnectedAddress;
            if (httpClient == null || address == null || !this.IsConnected)
            {
                return new Result(Resources.RahiTuberNotConnected);
            }

            string url = BuildStateRequest(address, state, active);
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url);

                // Only the status code is worth reading. RahiTuber builds its reply with a format
                // string that hands the state id to an integer conversion, so the body it sends back
                // is not the state that was asked for and cannot be relied on.
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return new Result();
                }

                Logger.Log(LogLevel.Error, "RahiTuber Service - State \"" + state + "\" returned " + (int)response.StatusCode + " " + response.StatusCode);
                return new Result(Resources.RahiTuberStateRequestFailed);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "RahiTuber Service - State \"" + state + "\" failed: " + ex.GetBaseException().Message);

                // A state request is the most frequent thing we do, so a failure here is the earliest
                // sign that RahiTuber has gone away rather than something to wait for the check on.
                // Deliberately not awaited: the reconnect loop runs until RahiTuber comes back, and
                // the command that happened to notice the drop must not sit inside it.
                _ = Task.Run(this.HandleConnectionLost);
            }
            return new Result(Resources.RahiTuberStateRequestFailed);
        }

        /// <summary>
        /// Builds the request for one state change. The query is a JSON array rather than named
        /// parameters, and the name goes in as a JSON string so quotes and backslashes survive.
        /// </summary>
        /// <remarks>
        /// The escaping has to be percent encoding rather than form encoding. RahiTuber decodes the
        /// query with form decoding turned off, so a plus sign stays a plus sign and a state name
        /// with a space in it would never match.
        /// </remarks>
        public static string BuildStateRequest(string address, string state, bool active)
        {
            // Sending the state as a JSON string covers both forms. RahiTuber compares the decoded
            // value against each state's index rendered as text and against its name, so "3" matches
            // the fourth state exactly as the bare number 3 would, and a state actually named "3"
            // matches either way round.
            string query = "[" + JsonConvert.ToString(state) + "," + (active ? "1" : "0") + "]";
            return address + stateRoute + "?" + Uri.EscapeDataString(query);
        }

        private async Task<Result> ConnectInternal()
        {
            int port = ChannelSession.Settings.RahiTuberPortNumber;
            if (port <= 0 || port > 65535)
            {
                Logger.Log(LogLevel.Error, "RahiTuber Service - " + port + " is not a usable port number");
                return new Result(Resources.RahiTuberPortNumberInvalid);
            }

            string address = string.Format(addressFormat, port);

            this.DisposeClient();
            AdvancedHttpClient httpClient = new AdvancedHttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds);
            this.client = httpClient;

            try
            {
                Logger.Log(LogLevel.Debug, "RahiTuber Service - Checking " + address);

                // Nothing in the protocol names the server, so the shape of the routing stands in for
                // it. RahiTuber answers the state route and rejects everything else, and a server
                // that accepts both is something other than RahiTuber sitting on that port.
                HttpResponseMessage rootResponse = await httpClient.GetAsync(address + "/");
                if (rootResponse.StatusCode != HttpStatusCode.BadRequest)
                {
                    Logger.Log(LogLevel.Error, "RahiTuber Service - " + address + "/ returned " + (int)rootResponse.StatusCode +
                        " " + rootResponse.StatusCode + " rather than the 400 RahiTuber answers unknown paths with, so whatever is on that port is not RahiTuber");
                    this.DisposeClient();
                    return new Result(string.Format(Resources.RahiTuberConnectionFailedToPort, port));
                }

                // No query at all leaves RahiTuber reading the state id as -1, which matches no state
                // and is discarded on its next frame, so this proves the route is there without
                // changing the avatar.
                HttpResponseMessage stateResponse = await httpClient.GetAsync(address + stateRoute);
                if (stateResponse.StatusCode != HttpStatusCode.OK)
                {
                    Logger.Log(LogLevel.Error, "RahiTuber Service - " + address + stateRoute + " returned " + (int)stateResponse.StatusCode +
                        " " + stateResponse.StatusCode + " rather than 200, so whatever is on that port is not RahiTuber");
                    this.DisposeClient();
                    return new Result(string.Format(Resources.RahiTuberConnectionFailedToPort, port));
                }

                this.IsConnected = true;
                this.ConnectedAddress = address;
                this.StartWatchdog();

                ServiceManager.Get<ITelemetryService>().TrackService("RahiTuber");

                Logger.Log(LogLevel.Information, "RahiTuber Service - Connected at " + address);

                return new Result();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "RahiTuber Service - Connection to " + address + " failed: " + ex.GetBaseException().Message);
                this.IsConnected = false;
                this.ConnectedAddress = null;
                this.DisposeClient();
            }
            return new Result(string.Format(Resources.RahiTuberConnectionFailedToPort, port));
        }

        /// <summary>
        /// Polls the root path so a RahiTuber that has been closed, or had its HTTP listener turned
        /// back off, does not leave the services page and every action editor claiming otherwise.
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

                        AdvancedHttpClient httpClient = this.client;
                        string address = this.ConnectedAddress;
                        if (!this.IsConnected || httpClient == null || address == null)
                        {
                            return;
                        }

                        try
                        {
                            // Any answer at all is enough. The status only has to come from RahiTuber
                            // rather than from a refused connection.
                            await httpClient.GetAsync(address + "/");
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Debug, "RahiTuber Service - Check against " + address + " failed, most likely RahiTuber was closed or Control States via HTTP was turned off: " + ex.GetBaseException().Message);
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
                // A failed state request and the connection check can both notice the same drop.
                if (this.isReconnecting || this.userRequestedDisconnect || !this.IsConnected)
                {
                    return;
                }
                this.isReconnecting = true;
            }

            try
            {
                this.StopWatchdog();
                this.IsConnected = false;

                Logger.Log(LogLevel.Error, "RahiTuber Service - Connection to " + (this.ConnectedAddress ?? "RahiTuber") + " lost, starting reconnect loop");
                this.ConnectedAddress = null;

                ChannelSession.DisconnectionOccurred(Resources.RahiTuber);

                Result result = new Result(false);
                int attempt = 0;
                while (!result.Success && !this.userRequestedDisconnect)
                {
                    await Task.Delay(ReconnectDelayMilliseconds);

                    attempt++;
                    Logger.Log(LogLevel.Debug, "RahiTuber Service - Reconnect attempt " + attempt);

                    result = await this.ConnectInternal();
                }

                if (result.Success)
                {
                    ChannelSession.ReconnectionOccurred(Resources.RahiTuber);
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

        private void DisposeClient()
        {
            AdvancedHttpClient httpClient = this.client;
            this.client = null;
            if (httpClient != null)
            {
                try { httpClient.Dispose(); }
                catch (Exception ex) { Logger.Log(ex); }
            }
        }
    }
}
