using MixItUp.Base;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services
{
    public class WindowsOBSService : IOBSStudioService
    {
        private const int CommandTimeoutInMilliseconds = 2500;
        private const int ConnectTimeoutInMilliseconds = 5000;

        public event EventHandler Connected = delegate { };
        public event EventHandler Disconnected = delegate { };

        private readonly OBSWebsocketV5 OBSWebsocketV5 = new OBSWebsocketV5();
        private readonly SemaphoreSlim operationSemaphore = new SemaphoreSlim(1, 1);

        private int reconnectLoopRunning = 0;
        private int hasEverConnected = 0;
        private int disconnectionNotified = 0;
        private int manualDisconnectRequested = 0;

        public WindowsOBSService()
        {
            this.OBSWebsocketV5.Disconnected += this.OBSWebsocketV5_Disconnected;
        }

        public string Name { get { return "OBS Studio"; } }

        public bool IsEnabled { get { return !string.IsNullOrEmpty(ChannelSession.Settings.OBSStudioServerIP); } }

        public bool IsConnected { get; private set; }

        public async Task<Result> Connect()
        {
            Interlocked.Exchange(ref this.manualDisconnectRequested, 0);
            return await this.ConnectInternal();
        }

        public async Task Disconnect()
        {
            Interlocked.Exchange(ref this.manualDisconnectRequested, 1);
            await this.DisconnectInternal(notifyDisconnected: false);
        }

        public Task<bool> TestConnection() { return Task.FromResult(true); }

        public async Task ShowScene(string sceneName)
        {
            Logger.Log(LogLevel.Debug, "Showing OBS Scene - " + sceneName);
            await this.ExecuteOBSCommand(async () =>
            {
                await this.OBSWebsocketV5.SetCurrentScene(sceneName);
                return true;
            });
        }

        public async Task<string> GetCurrentScene()
        {
            string sceneName = await this.ExecuteOBSCommand(async () =>
            {
                return await this.OBSWebsocketV5.GetCurrentSceneName();
            }, defaultValue: "Unknown");
            Logger.Log(LogLevel.Debug, "Current OBS Scene - " + sceneName);
            return sceneName;
        }

        public async Task SetSourceVisibility(string sceneName, string sourceName, bool visibility)
        {
            Logger.Log(LogLevel.Debug, "Setting source visibility - " + sourceName);
            await this.ExecuteOBSCommand(async () =>
            {
                if (string.IsNullOrEmpty(sceneName))
                {
                    sceneName = await this.OBSWebsocketV5.GetCurrentSceneName();
                }
                await this.OBSWebsocketV5.SetSourceRender(sourceName, visibility, sceneName);
                return true;
            });
        }

        public async Task SetSourceFilterVisibility(string sourceName, string filterName, bool visibility)
        {
            Logger.Log(LogLevel.Debug, "Setting source filter visibility - " + sourceName + " - " + filterName);
            await this.ExecuteOBSCommand(async () =>
            {
                await this.OBSWebsocketV5.SetSourceFilterVisibility(sourceName, filterName, visibility);
                return true;
            });
        }

        public async Task SetImageSourceFilePath(string sceneName, string sourceName, string filePath)
        {
            Logger.Log(LogLevel.Debug, "Setting image source file path - " + sourceName);
            await this.ExecuteOBSCommand(async () =>
            {
                JObject settings = await this.OBSWebsocketV5.GetSourceSettings(sourceName);
                if (settings != null)
                {
                    settings["file"] = filePath;
                    await this.OBSWebsocketV5.SetSourceSettings(sourceName, settings);
                }
                return true;
            });
        }

        public async Task SetMediaSourceFilePath(string sceneName, string sourceName, string filePath)
        {
            Logger.Log(LogLevel.Debug, "Setting media source file path - " + sourceName);
            await this.ExecuteOBSCommand(async () =>
            {
                JObject settings = await this.OBSWebsocketV5.GetSourceSettings(sourceName);
                if (settings != null)
                {
                    settings["local_file"] = filePath;
                    await this.OBSWebsocketV5.SetSourceSettings(sourceName, settings);
                }
                return true;
            });
        }

        public async Task SetWebBrowserSourceURL(string sceneName, string sourceName, string url)
        {
            Logger.Log(LogLevel.Debug, "Setting web browser URL - " + sourceName);

            await this.SetSourceVisibility(sceneName, sourceName, visibility: false);

            await this.ExecuteOBSCommand(async () =>
            {
                JObject settings = new JObject { ["url"] = url };
                await this.OBSWebsocketV5.SetSourceSettings(sourceName, settings);
                return true;
            });
        }

        public async Task SetSourceDimensions(string sceneName, string sourceName, StreamingSoftwareSourceDimensionsModel dimensions)
        {
            Logger.Log(LogLevel.Debug, "Setting source dimensions - " + sourceName);
            await this.ExecuteOBSCommand(async () =>
            {
                if (string.IsNullOrEmpty(sceneName))
                {
                    sceneName = await this.OBSWebsocketV5.GetCurrentSceneName();
                }
                await this.OBSWebsocketV5.SetSceneItemProperties(sceneName, sourceName, dimensions.X, dimensions.Y, dimensions.XScale, dimensions.YScale, dimensions.Rotation);
                return true;
            });
        }

        public async Task<StreamingSoftwareSourceDimensionsModel> GetSourceDimensions(string sceneName, string sourceName)
        {
            return await this.ExecuteOBSCommand(async () =>
            {
                if (string.IsNullOrEmpty(sceneName))
                {
                    sceneName = await this.OBSWebsocketV5.GetCurrentSceneName();
                }

                var response = await this.OBSWebsocketV5.GetSceneItemTransform(sceneName, sourceName);
                if (response.HasValue)
                {
                    return new StreamingSoftwareSourceDimensionsModel()
                    {
                        X = (int)response.Value.X,
                        Y = (int)response.Value.Y,
                        XScale = (response.Value.Width / response.Value.SourceWidth),
                        YScale = (response.Value.Height / response.Value.SourceHeight),
                    };
                }
                return null;
            });
        }

        public async Task StartStopStream()
        {
            await this.ExecuteOBSCommand(async () =>
            {
                await this.OBSWebsocketV5.StartStopStreaming();
                return true;
            });
        }

        public async Task StartStopRecording()
        {
            await this.ExecuteOBSCommand(async () =>
            {
                await this.OBSWebsocketV5.StartStopRecording();
                return true;
            });
        }

        public async Task<bool> StartReplayBuffer()
        {
            return await this.ExecuteOBSCommand(async () =>
            {
                try
                {
                    await this.OBSWebsocketV5.StartReplayBuffer();
                    return true;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Equals("replay buffer already active") || ex.Message.Equals("replay buffer disabled in settings"))
                    {
                        return true;
                    }
                    Logger.Log(ex);
                }
                return false;
            }, defaultValue: false);
        }

        public async Task SaveReplayBuffer()
        {
            await this.ExecuteOBSCommand(async () =>
            {
                await this.OBSWebsocketV5.SaveReplayBuffer();
                return true;
            });
        }

        public async Task SetSceneCollection(string sceneCollectionName)
        {
            await this.ExecuteOBSCommand(async () =>
            {
                await this.OBSWebsocketV5.SetCurrentSceneCollection(sceneCollectionName);
                return true;
            });
        }

        private async Task<Result> ConnectInternal()
        {
            bool attemptedConnect = false;
            bool isConnected = false;
            bool firstConnect = false;

            await this.operationSemaphore.WaitAsync();
            try
            {
                if (!this.OBSWebsocketV5.IsConnected)
                {
                    attemptedConnect = true;
                }
            }
            finally
            {
                this.operationSemaphore.Release();
            }

            if (attemptedConnect)
            {
                try
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource(ConnectTimeoutInMilliseconds))
                    {
                        string connectError = await this.OBSWebsocketV5.Connect(
                            ChannelSession.Settings.OBSStudioServerIP,
                            ChannelSession.Settings.OBSStudioServerPassword,
                            cts.Token);

                        if (connectError != null)
                        {
                            Logger.Log(LogLevel.Warning, "OBS Studio connection failed: " + connectError);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warning, "OBS Studio connection failed: " + ex.Message);
                }
            }

            await this.operationSemaphore.WaitAsync();
            try
            {
                this.IsConnected = this.OBSWebsocketV5.IsConnected;
                if (this.IsConnected)
                {
                    if (attemptedConnect)
                    {
                        firstConnect = Interlocked.Exchange(ref this.hasEverConnected, 1) == 0;
                        Interlocked.Exchange(ref this.disconnectionNotified, 0);
                    }
                    isConnected = true;
                }
            }
            finally
            {
                this.operationSemaphore.Release();
            }

            if (isConnected)
            {
                if (attemptedConnect)
                {
                    await this.StartReplayBuffer();
                    this.Connected(this, new EventArgs());
                    if (!firstConnect)
                    {
                        ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.OBSStudio);
                    }
                    ServiceManager.Get<ITelemetryService>().TrackService("OBS Studio");
                }
                return new Result();
            }

            return new Result(Resources.OBSWebSocketFailed);
        }

        private async Task DisconnectInternal(bool notifyDisconnected)
        {
            bool wasConnected = this.IsConnected || this.OBSWebsocketV5.IsConnected;

            await this.operationSemaphore.WaitAsync();
            try
            {
                this.IsConnected = false;
                if (this.OBSWebsocketV5.IsConnected)
                {
                    await this.OBSWebsocketV5.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.operationSemaphore.Release();
            }

            if (notifyDisconnected && wasConnected)
            {
                this.NotifyDisconnected();
            }
        }

        private void OBSWebsocketV5_Disconnected(object sender, EventArgs e)
        {
            bool wasConnected = this.IsConnected;
            this.IsConnected = false;

            if (Interlocked.CompareExchange(ref this.manualDisconnectRequested, 0, 0) != 0)
            {
                return;
            }

            if (!wasConnected)
            {
                return;
            }

            this.NotifyDisconnected();
            this.TryStartReconnectLoop();
        }

        private void TryStartReconnectLoop()
        {
            if (Interlocked.CompareExchange(ref this.reconnectLoopRunning, 1, 0) != 0)
            {
                return;
            }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
            {
                try
                {
                    while (Interlocked.CompareExchange(ref this.manualDisconnectRequested, 0, 0) == 0)
                    {
                        await Task.Delay(5000, cancellationToken);

                        Result result = await this.ConnectInternal();
                        if (result.Success)
                        {
                            break;
                        }
                    }
                }
                catch (TaskCanceledException) { }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                finally
                {
                    Interlocked.Exchange(ref this.reconnectLoopRunning, 0);
                }

                return true;
            }, CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void NotifyDisconnected()
        {
            if (Interlocked.Exchange(ref this.disconnectionNotified, 1) == 1)
            {
                return;
            }

            this.Disconnected(this, new EventArgs());
            ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.OBSStudio);
        }

        private async Task<T> ExecuteOBSCommand<T>(Func<Task<T>> command, int timeout = CommandTimeoutInMilliseconds, T defaultValue = default)
        {
            if (!this.IsConnected || !this.OBSWebsocketV5.IsConnected)
            {
                this.IsConnected = false;
                return defaultValue;
            }

            await this.operationSemaphore.WaitAsync();
            try
            {
                if (!this.OBSWebsocketV5.IsConnected)
                {
                    this.IsConnected = false;
                    return defaultValue;
                }

                return await command().WaitAsync(TimeSpan.FromMilliseconds(timeout));
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException)
                {
                    Logger.Log(LogLevel.Warning, "OBS Studio command timed out and the service will attempt reconnect");
                }
                else
                {
                    Logger.Log(ex);
                }

                if (ex is TimeoutException || ex is WebSocketException || ex is OperationCanceledException)
                {
                    this.IsConnected = false;
                    if (Interlocked.CompareExchange(ref this.manualDisconnectRequested, 0, 0) == 0)
                    {
                        this.NotifyDisconnected();
                        this.TryStartReconnectLoop();
                    }
                }

                return defaultValue;
            }
            finally
            {
                this.operationSemaphore.Release();
            }
        }
    }

    public class OBSWebsocketV5 : AdvancedClientWebSocket
    {
        private const string SceneNameChangedEvent = "SceneNameChanged";
        private const string SceneItemRemovedEvent = "SceneItemRemoved";
        private const string InputRemovedEvent = "InputRemoved";
        private const string InputNameChangedEvent = "InputNameChanged";

        private string password;
        private bool identified = false;
        private string identifyError = null;
        private ConcurrentDictionary<Guid, string> responses = new ConcurrentDictionary<Guid, string>();
        private SemaphoreSlim sendSemaphore = new SemaphoreSlim(1);
        private ConcurrentDictionary<string, ConcurrentDictionary<string, SceneItem>> sceneSourceNameToSceneItemDictionary = new ConcurrentDictionary<string, ConcurrentDictionary<string, SceneItem>>(StringComparer.OrdinalIgnoreCase);

        public new event EventHandler Disconnected;

        public bool IsConnected => this.identified;

        public OBSWebsocketV5()
        {
            base.PacketReceived += async (sender, packet) => await ProcessReceivedPacket(packet);
            base.Disconnected += (sender, closeStatus) =>
            {
                this.identified = false;
                this.Disconnected?.Invoke(this, EventArgs.Empty);
            };
        }


        public async Task<string> Connect(string endpoint, string password, CancellationToken cancellationToken)
        {
            this.password = password;
            this.identified = false;
            this.identifyError = null;

            try
            {
                await base.Connect(endpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                string detail = base.LastConnectHttpStatusCode.HasValue
                    ? $"HTTP {base.LastConnectHttpStatusCode.Value} - {ex.Message}"
                    : ex.Message;
                return detail;
            }

            if (!base.IsOpen())
            {
                string detail = "WebSocket failed to open";
                if (base.LastConnectHttpStatusCode.HasValue)
                {
                    detail += $" (HTTP {base.LastConnectHttpStatusCode.Value})";
                }
                return detail;
            }

            while (!this.identified && this.identifyError == null && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await base.Disconnect();
                return "Connection timed out during OBS identification handshake";
            }

            if (this.identifyError != null)
            {
                await base.Disconnect();
                return this.identifyError;
            }

            return null;
        }

        public new async Task Disconnect(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure)
        {
            this.identified = false;
            await base.Disconnect(closeStatus);
        }

        public async Task SetCurrentScene(string sceneName)
        {
            await Send(new OBSMessageSetCurrentProgramSceneRequest(sceneName));
        }

        public async Task SetCurrentSceneCollection(string sceneCollectionName)
        {
            await Send(new OBSMessageSetCurrentSceneCollectionRequest(sceneCollectionName));
        }

        public async Task SaveReplayBuffer()
        {
            await Send(new OBSMessageSaveReplayBufferRequest());
        }

        public async Task StartReplayBuffer()
        {
            await Send(new OBSMessageStartReplayBufferRequest());
        }

        public async Task StartStopRecording()
        {
            await Send(new OBSMessageToggleRecordRequest());
        }

        public async Task StartStopStreaming()
        {
            await Send(new OBSMessageToggleStreamRequest());
        }

        public async Task<string> GetCurrentSceneName()
        {
            string packet = await SendAndWait(new OBSMessageGetCurrentProgramSceneRequest());
            if (!string.IsNullOrEmpty(packet))
            {
                OBSMessageGetCurrentProgramSceneResponse response = JSONSerializerHelper.DeserializeFromString<OBSMessageGetCurrentProgramSceneResponse>(packet);
                return response?.Data?.Data?.CurrentProgramSceneName ?? "Unknown";
            }
            return "Unknown";
        }

        public async Task<(float X, float Y, float Width, float Height, float SourceWidth, float SourceHeight)?> GetSceneItemTransform(string sceneName, string sourceName)
        {
            string packet = await SendAndWait(new OBSMessageGetSceneItemListRequest(sceneName));
            if (!string.IsNullOrEmpty(packet))
            {
                OBSMessageGetSceneItemListResponse response = JSONSerializerHelper.DeserializeFromString<OBSMessageGetSceneItemListResponse>(packet);
                if (response?.Data?.Data != null)
                {
                    foreach (SceneItem sceneItem in response.Data.Data.SceneItems)
                    {
                        if (string.Equals(sceneItem.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
                        {
                            return TransformToTuple(sceneItem.SceneItemTransform);
                        }
                    }

                    foreach (SceneItem sceneItem in response.Data.Data.SceneItems)
                    {
                        if (sceneItem.IsGroup.GetValueOrDefault())
                        {
                            string groupPacket = await SendAndWait(new OBSMessageGetGroupSceneItemListRequest(sceneItem.SourceName));
                            if (!string.IsNullOrEmpty(groupPacket))
                            {
                                OBSMessageGetGroupSceneItemListResponse groupResponse = JSONSerializerHelper.DeserializeFromString<OBSMessageGetGroupSceneItemListResponse>(groupPacket);
                                if (groupResponse?.Data?.Data != null)
                                {
                                    foreach (SceneItem groupSceneItem in groupResponse.Data.Data.SceneItems)
                                    {
                                        if (string.Equals(groupSceneItem.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            return TransformToTuple(groupSceneItem.SceneItemTransform);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static (float X, float Y, float Width, float Height, float SourceWidth, float SourceHeight) TransformToTuple(SceneItemTransform t)
        {
            return (t.PositionX, t.PositionY, t.Width, t.Height, t.SourceWidth, t.SourceHeight);
        }

        public async Task SetSceneItemProperties(string sceneName, string sourceName, int x, int y, float xScale, float yScale, float rotation)
        {
            string packet = await SendAndWait(new OBSMessageGetSceneItemListRequest(sceneName));
            if (!string.IsNullOrEmpty(packet))
            {
                OBSMessageGetSceneItemListResponse response = JSONSerializerHelper.DeserializeFromString<OBSMessageGetSceneItemListResponse>(packet);
                if (response?.Data?.Data != null)
                {
                    foreach (SceneItem scene in response.Data.Data.SceneItems)
                    {
                        if (string.Equals(scene.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
                        {
                            JObject newTransform = new JObject
                            {
                                ["positionX"] = x,
                                ["positionY"] = y,
                                ["scaleX"] = xScale,
                                ["scaleY"] = yScale,
                                ["rotation"] = rotation
                            };
                            await Send(new OBSMessageSetSceneItemTransformRequest(sceneName, scene.SceneItemId, newTransform));
                            return;
                        }
                    }
                }
            }
        }

        public async Task<JObject> GetSourceSettings(string sourceName)
        {
            string packet = await SendAndWait(new OBSMessageGetInputSettingsRequest(sourceName));
            if (!string.IsNullOrEmpty(packet))
            {
                OBSMessageGetInputSettingsResponse response = JSONSerializerHelper.DeserializeFromString<OBSMessageGetInputSettingsResponse>(packet);
                return response?.Data?.Data?.InputSettings;
            }
            return null;
        }

        public async Task SetSourceSettings(string sourceName, JObject settings)
        {
            await Send(new OBSMessageSetInputSettingsRequest(sourceName, settings));
        }

        public async Task SetSourceFilterVisibility(string sourceName, string filterName, bool visibility)
        {
            await Send(new OBSMessageSetSourceFilterEnabledRequest(sourceName, filterName, visibility));
        }

        public async Task SetSourceRender(string sourceName, bool visibility, string sceneName)
        {
            SceneItem sceneItem = await this.SearchForSceneItem(sourceName, sceneName);
            if (sceneItem != null)
            {
                await Send(new OBSMessageSetSceneItemEnabledRequest(sceneItem.GroupName ?? sceneName, sceneItem.SceneItemId, visibility));
            }
        }

        private async Task<SceneItem> SearchForSceneItem(string sourceName, string sceneName)
        {
            // Check our scene cache first
            if (sceneSourceNameToSceneItemDictionary.TryGetValue(sceneName, out var sceneItems))
            {
                if (sceneItems.TryGetValue(sourceName, out var cachedItem))
                {
                    return cachedItem;
                }
            }
            else
            {
                sceneSourceNameToSceneItemDictionary[sceneName] = new ConcurrentDictionary<string, SceneItem>(StringComparer.OrdinalIgnoreCase);
            }

            // Failed hit, invalid the scene's cache
            sceneSourceNameToSceneItemDictionary[sceneName].Clear();

            string packet = await SendAndWait(new OBSMessageGetSceneItemListRequest(sceneName));
            if (!string.IsNullOrEmpty(packet))
            {
                OBSMessageGetSceneItemListResponse response = JSONSerializerHelper.DeserializeFromString<OBSMessageGetSceneItemListResponse>(packet);
                if (response?.Data?.Data != null)
                {
                    // Cache all items first
                    foreach (SceneItem sceneItem in response.Data.Data.SceneItems)
                    {
                        sceneSourceNameToSceneItemDictionary[sceneName][sceneItem.SourceName] = sceneItem;
                    }

                    foreach (SceneItem sceneItem in response.Data.Data.SceneItems)
                    {
                        if (string.Equals(sceneItem.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
                        {
                            return sceneItem;
                        }
                    }

                    // If we got here, then the item is not found, search groups (this is slow)
                    foreach (SceneItem sceneItem in response.Data.Data.SceneItems)
                    {
                        if (sceneItem.IsGroup.GetValueOrDefault())
                        {
                            string groupPacket = await SendAndWait(new OBSMessageGetGroupSceneItemListRequest(sceneItem.SourceName));
                            if (!string.IsNullOrEmpty(groupPacket))
                            {
                                OBSMessageGetGroupSceneItemListResponse groupResponse = JSONSerializerHelper.DeserializeFromString<OBSMessageGetGroupSceneItemListResponse>(groupPacket);
                                if (groupResponse?.Data?.Data != null)
                                {
                                    // Cache all items first
                                    foreach (SceneItem groupSceneItem in groupResponse.Data.Data.SceneItems)
                                    {
                                        groupSceneItem.GroupName = sceneItem.SourceName;
                                        sceneSourceNameToSceneItemDictionary[sceneName][groupSceneItem.SourceName] = groupSceneItem;
                                    }

                                    foreach (SceneItem groupSceneItem in groupResponse.Data.Data.SceneItems)
                                    {
                                        if (string.Equals(groupSceneItem.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            return groupSceneItem;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private async Task ProcessReceivedPacket(string packet)
        {
            try
            {
                Logger.Log(LogLevel.Debug, "OBS Studio packet received: " + packet);

                OBSMessage message = JSONSerializerHelper.DeserializeFromString<OBSMessage>(packet);
                switch (message.OpCode)
                {
                    case 0: // Hello
                        await HandleHello(JSONSerializerHelper.DeserializeFromString<OBSMessageHello>(packet));
                        break;
                    case 2: // Identified
                        this.identified = true;
                        break;
                    case 5: // Event
                        HandleEvent(JSONSerializerHelper.DeserializeFromString<OBSMessageEvent>(packet));
                        break;
                    case 7: // Response
                        HandleResponse(packet);
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine(packet);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void HandleResponse(string packet)
        {
            OBSMessageResponse response = JSONSerializerHelper.DeserializeFromString<OBSMessageResponse>(packet);
            if (this.responses.TryRemove(response.Data.RequestId, out _))
            {
                this.responses.TryAdd(response.Data.RequestId, packet);
            }
        }

        private void HandleEvent(OBSMessageEvent message)
        {
            switch (message.Data.EventType)
            {
                case SceneNameChangedEvent:
                    if (message.Data.Data.TryGetValue("oldSceneName", out var oldSceneName))
                    {
                        sceneSourceNameToSceneItemDictionary.TryRemove(oldSceneName?.ToString(), out _);
                    }
                    break;
                case SceneItemRemovedEvent:
                    if (message.Data.Data.TryGetValue("sceneName", out var removedSceneName) && message.Data.Data.TryGetValue("sourceName", out var removedSourceName))
                    {
                        if (sceneSourceNameToSceneItemDictionary.TryGetValue(removedSceneName?.ToString(), out var items))
                        {
                            items.TryRemove(removedSourceName?.ToString(), out _);
                        }
                    }
                    break;
                case InputRemovedEvent:
                    if (message.Data.Data.TryGetValue("inputName", out var inputName))
                    {
                        foreach (var scene in sceneSourceNameToSceneItemDictionary.Keys.ToList())
                        {
                            if (sceneSourceNameToSceneItemDictionary.TryGetValue(scene, out var items))
                            {
                                items.TryRemove(inputName?.ToString(), out _);
                            }
                        }
                    }
                    break;
                case InputNameChangedEvent:
                    if (message.Data.Data.TryGetValue("oldInputName", out var oldInputName))
                    {
                        foreach (var scene in sceneSourceNameToSceneItemDictionary.Keys.ToList())
                        {
                            if (sceneSourceNameToSceneItemDictionary.TryGetValue(scene, out var items))
                            {
                                items.TryRemove(oldInputName?.ToString(), out _);
                            }
                        }
                    }
                    break;
            }
        }

        private async Task Send(OBSMessage message)
        {
            try
            {
                await this.sendSemaphore.WaitAsync();
                await base.Send(JsonConvert.SerializeObject(message));
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sendSemaphore.Release();
            }
        }

        private async Task<string> SendAndWait(OBSMessageRequest request)
        {
            this.responses.TryAdd(request.Data.RequestId, null);
            await Send(request);

            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                while (!cts.IsCancellationRequested)
                {
                    if (this.responses.TryGetValue(request.Data.RequestId, out string packet) && !string.IsNullOrEmpty(packet))
                    {
                        this.responses.TryRemove(request.Data.RequestId, out _);
                        return packet;
                    }
                    await Task.Delay(100);
                }
            }

            this.responses.TryRemove(request.Data.RequestId, out _);
            return null;
        }

        private async Task HandleHello(OBSMessageHello message)
        {
            OBSMessageIdentify identify = new OBSMessageIdentify();
            if (message?.Data?.Authentication != null)
            {
                // To generate the authentication string, follow these steps:
                // Concatenate the websocket password with the salt provided by the server(password + salt)
                string passwordAndSalt = this.password + message.Data.Authentication.Salt;

                // Generate an SHA256 binary hash of the result and base64 encode it, known as a base64 secret.
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    byte[] bytes = sha256Hash.ComputeHash(Encoding.ASCII.GetBytes(passwordAndSalt));
                    string base64Secret = Convert.ToBase64String(bytes);

                    // Concatenate the base64 secret with the challenge sent by the server(base64_secret + challenge)
                    string base64SecretAndChallenge = base64Secret + message.Data.Authentication.Challenge;

                    // Generate a binary SHA256 hash of that result and base64 encode it. You now have your authentication string.
                    bytes = sha256Hash.ComputeHash(Encoding.ASCII.GetBytes(base64SecretAndChallenge));
                    identify.Data.Authentication = Convert.ToBase64String(bytes);
                }
            }
            await Send(identify);
        }

        private class OBSMessage
        {
            [JsonProperty("op")]
            public int OpCode { get; protected set; }
        }

        private class OBSMessage<T> : OBSMessage
        {
            [JsonProperty("d")]
            public T Data { get; set; }
        }

        private class OBSMessageHello : OBSMessage<HelloData> { }

        private class HelloData
        {
            [JsonProperty("obsWebSocketVersion")]
            public string OBSWebSocketVersion { get; set; }

            [JsonProperty("rpcVersion")]
            public int RPCVersion { get; set; }

            [JsonProperty("authentication")]
            public HelloDataAuthentication Authentication { get; set; }
        }

        private class HelloDataAuthentication
        {
            [JsonProperty("challenge")]
            public string Challenge { get; set; }

            [JsonProperty("salt")]
            public string Salt { get; set; }
        }

        private class OBSMessageIdentify : OBSMessage<IdentifyData>
        {
            public OBSMessageIdentify()
            {
                OpCode = 1;
                Data = new IdentifyData
                {
                    RPCVersion = 1,
                    EventSubscriptions = (1 << 2) | (1 << 3) | (1 << 7),   // Scene, Input, Scene Items events
                };
            }
        }

        private class IdentifyData
        {
            [JsonProperty("rpcVersion")]
            public int RPCVersion { get; set; }

            [JsonProperty("authentication")]
            public string Authentication { get; set; }

            [JsonProperty("eventSubscriptions")]
            public ulong EventSubscriptions { get; set; }
        }

        private class OBSMessageIdentified : OBSMessage<IdentifiedData> { }

        private class IdentifiedData
        {
            [JsonProperty("negotiatedRpcVersion")]
            public string NegotiatedRpcVersion { get; set; }
        }

        private class OBSMessageEvent : OBSMessage<EventData> { }

        private class EventData
        {
            [JsonProperty("eventType")]
            public string EventType { get; set; }

            [JsonProperty("eventIntent")]
            public int EventIntent { get; set; }

            [JsonProperty("eventData")]
            public JObject Data { get; set; }
        }

        private class OBSMessageToggleStreamRequest : OBSMessageRequest
        {
            public OBSMessageToggleStreamRequest() : base() { this.Data.RequestType = "ToggleStream"; }
        }

        private class OBSMessageToggleRecordRequest : OBSMessageRequest
        {
            public OBSMessageToggleRecordRequest() : base() { this.Data.RequestType = "ToggleRecord"; }
        }

        private class OBSMessageStartReplayBufferRequest : OBSMessageRequest
        {
            public OBSMessageStartReplayBufferRequest() : base() { this.Data.RequestType = "StartReplayBuffer"; }
        }

        private class OBSMessageSaveReplayBufferRequest : OBSMessageRequest
        {
            public OBSMessageSaveReplayBufferRequest() : base() { this.Data.RequestType = "SaveReplayBuffer"; }
        }

        private class OBSMessageGetCurrentProgramSceneRequest : OBSMessageRequest
        {
            public OBSMessageGetCurrentProgramSceneRequest() : base() { this.Data.RequestType = "GetCurrentProgramScene"; }
        }

        private class OBSMessageGetCurrentProgramSceneResponse : OBSMessageResponse<GetCurrentProgramSceneData> { }

        private class GetCurrentProgramSceneData
        {
            [JsonProperty("currentProgramSceneName")]
            public string CurrentProgramSceneName { get; set; }
        }

        private class OBSMessageSetCurrentProgramSceneRequest : OBSMessageRequest<SetCurrentProgramSceneRequestData>
        {
            public OBSMessageSetCurrentProgramSceneRequest(string sceneName) : base()
            {
                this.Data.RequestType = "SetCurrentProgramScene";
                this.Data.Data = new SetCurrentProgramSceneRequestData { SceneName = sceneName };
            }
        }

        private class SetCurrentProgramSceneRequestData
        {
            [JsonProperty("sceneName")]
            public string SceneName { get; set; }
        }

        private class OBSMessageSetCurrentSceneCollectionRequest : OBSMessageRequest<SetCurrentSceneCollectionData>
        {
            public OBSMessageSetCurrentSceneCollectionRequest(string sceneCollectionName) : base()
            {
                this.Data.RequestType = "SetCurrentSceneCollection";
                this.Data.Data = new SetCurrentSceneCollectionData { SceneCollectionName = sceneCollectionName };
            }
        }

        private class SetCurrentSceneCollectionData
        {
            [JsonProperty("sceneCollectionName")]
            public string SceneCollectionName { get; set; }
        }

        private class OBSMessageGetSceneItemListRequest : OBSMessageRequest<GetSceneItemListRequestData>
        {
            public OBSMessageGetSceneItemListRequest(string sceneName) : base()
            {
                this.Data.RequestType = "GetSceneItemList";
                this.Data.Data = new GetSceneItemListRequestData { SceneName = sceneName };
            }
        }

        private class GetSceneItemListRequestData
        {
            [JsonProperty("sceneName")]
            public string SceneName { get; set; }
        }

        private class OBSMessageGetSceneItemListResponse : OBSMessageResponse<GetSceneItemListResponseData> { }

        private class GetSceneItemListResponseData
        {
            [JsonProperty("sceneItems")]
            public SceneItem[] SceneItems { get; set; }
        }

        private class OBSMessageGetGroupSceneItemListRequest : OBSMessageRequest<GetGroupSceneItemListRequestData>
        {
            public OBSMessageGetGroupSceneItemListRequest(string groupName) : base()
            {
                this.Data.RequestType = "GetGroupSceneItemList";
                this.Data.Data = new GetGroupSceneItemListRequestData { SceneName = groupName };
            }
        }

        private class GetGroupSceneItemListRequestData
        {
            [JsonProperty("sceneName")]
            public string SceneName { get; set; }
        }

        private class OBSMessageGetGroupSceneItemListResponse : OBSMessageResponse<GetGroupSceneItemListResponseData> { }

        private class GetGroupSceneItemListResponseData
        {
            [JsonProperty("sceneItems")]
            public SceneItem[] SceneItems { get; set; }
        }

        private class SceneItem
        {
            [JsonProperty("sceneItemId")]
            public int SceneItemId { get; set; }

            [JsonProperty("sourceName")]
            public string SourceName { get; set; }

            [JsonProperty("sceneItemTransform")]
            public SceneItemTransform SceneItemTransform { get; set; }

            [JsonProperty("isGroup")]
            public bool? IsGroup { get; set; }

            public string GroupName { get; set; }
        }

        private class SceneItemTransform
        {
            [JsonProperty("height")]
            public float Height { get; set; }

            [JsonProperty("positionX")]
            public float PositionX { get; set; }

            [JsonProperty("positionY")]
            public float PositionY { get; set; }

            [JsonProperty("rotation")]
            public float Rotation { get; set; }

            [JsonProperty("scaleX")]
            public float ScaleX { get; set; }

            [JsonProperty("scaleY")]
            public float ScaleY { get; set; }

            [JsonProperty("sourceHeight")]
            public float SourceHeight { get; set; }

            [JsonProperty("sourceWidth")]
            public float SourceWidth { get; set; }

            [JsonProperty("width")]
            public float Width { get; set; }
        }

        private class OBSMessageSetSceneItemTransformRequest : OBSMessageRequest<SetSceneItemTransformRequestData>
        {
            public OBSMessageSetSceneItemTransformRequest(string sceneName, int sceneItemId, JObject sceneItemTransform) : base()
            {
                this.Data.RequestType = "SetSceneItemTransform";
                this.Data.Data = new SetSceneItemTransformRequestData
                {
                    SceneName = sceneName,
                    SceneItemId = sceneItemId,
                    SceneItemTransform = sceneItemTransform,
                };
            }
        }

        private class SetSceneItemTransformRequestData
        {
            [JsonProperty("sceneName")]
            public string SceneName { get; set; }

            [JsonProperty("sceneItemId")]
            public int SceneItemId { get; set; }

            [JsonProperty("sceneItemTransform")]
            public JObject SceneItemTransform { get; set; }
        }

        private class OBSMessageSetSceneItemEnabledRequest : OBSMessageRequest<SetSceneItemEnabledData>
        {
            public OBSMessageSetSceneItemEnabledRequest(string sceneName, int sceneItemId, bool sceneItemEnabled) : base()
            {
                this.Data.RequestType = "SetSceneItemEnabled";
                this.Data.Data = new SetSceneItemEnabledData
                {
                    SceneName = sceneName,
                    SceneItemId = sceneItemId,
                    SceneItemEnabled = sceneItemEnabled,
                };
            }
        }

        private class SetSceneItemEnabledData
        {
            [JsonProperty("sceneName")]
            public string SceneName { get; set; }

            [JsonProperty("sceneItemId")]
            public int SceneItemId { get; set; }

            [JsonProperty("sceneItemEnabled")]
            public bool SceneItemEnabled { get; set; }
        }

        private class OBSMessageGetInputSettingsRequest : OBSMessageRequest<GetInputSettingsData>
        {
            public OBSMessageGetInputSettingsRequest(string sourceName) : base()
            {
                this.Data.RequestType = "GetInputSettings";
                this.Data.Data = new GetInputSettingsData { InputName = sourceName };
            }
        }

        private class GetInputSettingsData
        {
            [JsonProperty("inputName")]
            public string InputName { get; set; }
        }

        private class OBSMessageGetInputSettingsResponse : OBSMessageResponse<GetInputSettingsResponseData> { }

        private class GetInputSettingsResponseData
        {
            [JsonProperty("inputSettings")]
            public JObject InputSettings { get; set; }

            [JsonProperty("inputKind")]
            public string InputKind { get; set; }
        }

        private class OBSMessageSetInputSettingsRequest : OBSMessageRequest<SetInputSettingsData>
        {
            public OBSMessageSetInputSettingsRequest(string sourceName, JObject settings) : base()
            {
                this.Data.RequestType = "SetInputSettings";
                this.Data.Data = new SetInputSettingsData
                {
                    InputName = sourceName,
                    InputSettings = settings,
                };
            }
        }

        private class SetInputSettingsData
        {
            [JsonProperty("inputName")]
            public string InputName { get; set; }

            [JsonProperty("inputSettings")]
            public JObject InputSettings { get; set; }
        }

        private class OBSMessageSetSourceFilterEnabledRequest : OBSMessageRequest<SetSourceFilterEnabledData>
        {
            public OBSMessageSetSourceFilterEnabledRequest(string sourceName, string filterName, bool filterEnabled) : base()
            {
                this.Data.RequestType = "SetSourceFilterEnabled";
                this.Data.Data = new SetSourceFilterEnabledData
                {
                    SourceName = sourceName,
                    FilterName = filterName,
                    FilterEnabled = filterEnabled,
                };
            }
        }

        private class SetSourceFilterEnabledData
        {
            [JsonProperty("sourceName")]
            public string SourceName { get; set; }

            [JsonProperty("filterName")]
            public string FilterName { get; set; }

            [JsonProperty("filterEnabled")]
            public bool FilterEnabled { get; set; }
        }

        private class OBSMessageRequest : OBSMessage<RequestData>
        {
            public OBSMessageRequest()
            {
                OpCode = 6;
                this.Data = new RequestData { RequestId = Guid.NewGuid() };
            }
        }

        private class OBSMessageRequest<T> : OBSMessageRequest
        {
            public new RequestData<T> Data
            {
                get => (RequestData<T>)base.Data;
                private set => base.Data = value;
            }

            public OBSMessageRequest()
            {
                this.Data = new RequestData<T> { RequestId = Guid.NewGuid() };
            }
        }

        private class RequestData
        {
            [JsonProperty("requestType")]
            public string RequestType { get; set; }

            [JsonProperty("requestId")]
            public Guid RequestId { get; set; }
        }

        private class RequestData<T> : RequestData
        {
            [JsonProperty("requestData")]
            public T Data { get; set; }
        }

        private class OBSMessageResponse : OBSMessage<ResponseData> { }

        private class OBSMessageResponse<T> : OBSMessage<ResponseData<T>> { }

        private class ResponseData
        {
            [JsonProperty("requestType")]
            public string RequestType { get; set; }

            [JsonProperty("requestId")]
            public Guid RequestId { get; set; }

            [JsonProperty("requestStatus")]
            public RequestStatus Status { get; set; }
        }

        private class ResponseData<T> : ResponseData
        {
            [JsonProperty("responseData")]
            public T Data { get; set; }
        }

        private class RequestStatus
        {
            [JsonProperty("result")]
            public bool Result { get; set; }

            [JsonProperty("code")]
            public int Code { get; set; }

            [JsonProperty("comment")]
            public string Comment { get; set; }
        }
    }
}