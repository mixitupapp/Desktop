using MixItUp.Base;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
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

        private readonly OBSWebsocket OBSWebsocket = new OBSWebsocket();
        private readonly SemaphoreSlim operationSemaphore = new SemaphoreSlim(1, 1);

        private int reconnectLoopRunning = 0;
        private int hasEverConnected = 0;
        private int disconnectionNotified = 0;
        private int manualDisconnectRequested = 0;

        public WindowsOBSService()
        {
            this.OBSWebsocket.WSTimeout = TimeSpan.FromMilliseconds(ConnectTimeoutInMilliseconds);
            this.OBSWebsocket.Disconnected += this.OBSWebsocket_Disconnected;
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
            await this.DisconnectInternal(notifyDisconnected: true);
        }

        public Task<bool> TestConnection() { return Task.FromResult(true); }

        public async Task ShowScene(string sceneName)
        {
            Logger.Log(LogLevel.Debug, "Showing OBS Scene - " + sceneName);
            await this.ExecuteOBSCommand(() =>
            {
                this.OBSWebsocket.SetCurrentProgramScene(sceneName);
                return true;
            });
        }

        public async Task<string> GetCurrentScene()
        {
            string sceneName = await this.ExecuteOBSCommand(() => this.OBSWebsocket.GetCurrentProgramScene(), defaultValue: "Unknown");
            Logger.Log(LogLevel.Debug, "Current OBS Scene - " + sceneName);
            return sceneName;
        }

        public async Task SetSourceVisibility(string sceneName, string sourceName, bool visibility)
        {
            Logger.Log(LogLevel.Debug, "Setting source visibility - " + sourceName);
            await this.ExecuteOBSCommand(() =>
            {
                if (this.TryGetSceneItemReference(this.ResolveSceneName(sceneName), sourceName, out string targetSceneName, out int sceneItemId))
                {
                    this.OBSWebsocket.SetSceneItemEnabled(targetSceneName, sceneItemId, visibility);
                }
                return true;
            });
        }

        public async Task SetSourceFilterVisibility(string sourceName, string filterName, bool visibility)
        {
            Logger.Log(LogLevel.Debug, "Setting source filter visibility - " + sourceName + " - " + filterName);
            await this.ExecuteOBSCommand(() =>
            {
                this.OBSWebsocket.SetSourceFilterEnabled(sourceName, filterName, visibility);
                return true;
            });
        }

        public async Task SetImageSourceFilePath(string sceneName, string sourceName, string filePath)
        {
            Logger.Log(LogLevel.Debug, "Setting image source file path - " + sourceName);
            await this.ExecuteOBSCommand(() =>
            {
                InputSettings inputSettings = this.OBSWebsocket.GetInputSettings(sourceName);
                if (inputSettings?.Settings != null)
                {
                    inputSettings.Settings["file"] = filePath;
                    this.OBSWebsocket.SetInputSettings(sourceName, inputSettings.Settings, overlay: true);
                }
                return true;
            });
        }

        public async Task SetMediaSourceFilePath(string sceneName, string sourceName, string filePath)
        {
            Logger.Log(LogLevel.Debug, "Setting media source file path - " + sourceName);
            await this.ExecuteOBSCommand(() =>
            {
                InputSettings inputSettings = this.OBSWebsocket.GetInputSettings(sourceName);
                if (inputSettings?.Settings != null)
                {
                    inputSettings.Settings["local_file"] = filePath;
                    this.OBSWebsocket.SetInputSettings(sourceName, inputSettings.Settings, overlay: true);
                }
                return true;
            });
        }

        public async Task SetWebBrowserSourceURL(string sceneName, string sourceName, string url)
        {
            Logger.Log(LogLevel.Debug, "Setting web browser URL - " + sourceName);

            await this.SetSourceVisibility(sceneName, sourceName, visibility: false);

            await this.ExecuteOBSCommand(() =>
            {
                InputSettings inputSettings = this.OBSWebsocket.GetInputSettings(sourceName);
                JObject settings = inputSettings?.Settings ?? new JObject();
                settings["is_local_file"] = false;
                settings["url"] = url;
                this.OBSWebsocket.SetInputSettings(sourceName, settings, overlay: true);
                return true;
            });
        }

        public async Task SetSourceDimensions(string sceneName, string sourceName, StreamingSoftwareSourceDimensionsModel dimensions)
        {
            Logger.Log(LogLevel.Debug, "Setting source dimensions - " + sourceName);
            await this.ExecuteOBSCommand(() =>
            {
                if (this.TryGetSceneItemReference(this.ResolveSceneName(sceneName), sourceName, out string targetSceneName, out int sceneItemId))
                {
                    JObject transform = new JObject
                    {
                        ["positionX"] = dimensions.X,
                        ["positionY"] = dimensions.Y,
                        ["scaleX"] = dimensions.XScale,
                        ["scaleY"] = dimensions.YScale,
                        ["rotation"] = dimensions.Rotation,
                    };

                    this.OBSWebsocket.SetSceneItemTransform(targetSceneName, sceneItemId, transform);
                }
                return true;
            });
        }

        public async Task<StreamingSoftwareSourceDimensionsModel> GetSourceDimensions(string sceneName, string sourceName)
        {
            return await this.ExecuteOBSCommand(() =>
            {
                if (this.TryGetSceneItemReference(this.ResolveSceneName(sceneName), sourceName, out string targetSceneName, out int sceneItemId))
                {
                    SceneItemTransformInfo transform = this.OBSWebsocket.GetSceneItemTransform(targetSceneName, sceneItemId);
                    return new StreamingSoftwareSourceDimensionsModel()
                    {
                        X = (int)transform.X,
                        Y = (int)transform.Y,
                        Rotation = (int)transform.Rotation,
                        XScale = (float)transform.ScaleX,
                        YScale = (float)transform.ScaleY,
                    };
                }

                return null;
            });
        }

        public async Task StartStopStream()
        {
            await this.ExecuteOBSCommand(() =>
            {
                this.OBSWebsocket.ToggleStream();
                return true;
            });
        }

        public async Task StartStopRecording()
        {
            await this.ExecuteOBSCommand(() =>
            {
                this.OBSWebsocket.ToggleRecord();
                return true;
            });
        }

        public async Task<bool> StartReplayBuffer()
        {
            return await this.ExecuteOBSCommand(() =>
            {
                try
                {
                    if (this.OBSWebsocket.GetReplayBufferStatus())
                    {
                        return true;
                    }

                    this.OBSWebsocket.StartReplayBuffer();
                    return true;
                }
                catch (ErrorResponseException ex)
                {
                    if (ex.ErrorCode == 604)
                    {
                        return true;
                    }
                    if (ex.ErrorCode == 500 && this.OBSWebsocket.GetReplayBufferStatus())
                    {
                        return true;
                    }
                    throw;
                }
            }, defaultValue: false);
        }

        public async Task SaveReplayBuffer()
        {
            await this.ExecuteOBSCommand(() =>
            {
                this.OBSWebsocket.SaveReplayBuffer();
                return true;
            });
        }

        public async Task SetSceneCollection(string sceneCollectionName)
        {
            await this.ExecuteOBSCommand(() =>
            {
                this.OBSWebsocket.SetCurrentSceneCollection(sceneCollectionName);
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
                if (!this.OBSWebsocket.IsConnected)
                {
                    try
                    {
                        this.OBSWebsocket.ConnectAsync(ChannelSession.Settings.OBSStudioServerIP, ChannelSession.Settings.OBSStudioServerPassword);
                        attemptedConnect = true;
                    }
                    catch (Exception ex)
                    {
                        this.LogConnectFailure(ex);
                    }
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
                    await this.WaitForConnectedState(TimeSpan.FromMilliseconds(ConnectTimeoutInMilliseconds));
                }
                catch (Exception ex)
                {
                    this.LogConnectFailure(ex);
                }
            }

            await this.operationSemaphore.WaitAsync();
            try
            {
                this.IsConnected = this.OBSWebsocket.IsConnected;
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
            bool wasConnected = this.IsConnected || this.OBSWebsocket.IsConnected;

            await this.operationSemaphore.WaitAsync();
            try
            {
                this.IsConnected = false;

                if (this.OBSWebsocket.IsConnected)
                {
                    this.OBSWebsocket.Disconnect();
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

        private void OBSWebsocket_Disconnected(object sender, ObsDisconnectionInfo e)
        {
            bool wasConnected = this.IsConnected;
            this.IsConnected = false;

            if (Interlocked.CompareExchange(ref this.manualDisconnectRequested, 0, 0) != 0)
            {
                return;
            }

            string disconnectDetails = $"OBS disconnect - Code: {e?.ObsCloseCode}, Reason: {e?.DisconnectReason}";
            if (e?.WebsocketDisconnectionInfo?.Exception != null)
            {
                disconnectDetails += $", Exception: {e.WebsocketDisconnectionInfo.Exception.Message}";
            }
            Logger.Log(LogLevel.Warning, disconnectDetails);

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
                catch (TaskCanceledException)
                {
                }
                catch (OperationCanceledException)
                {
                }
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

        private async Task<T> ExecuteOBSCommand<T>(Func<T> command, int timeout = CommandTimeoutInMilliseconds, T defaultValue = default)
        {
            if (!this.IsConnected || !this.OBSWebsocket.IsConnected)
            {
                this.IsConnected = false;
                return defaultValue;
            }

            await this.operationSemaphore.WaitAsync();
            try
            {
                if (!this.OBSWebsocket.IsConnected)
                {
                    this.IsConnected = false;
                    return defaultValue;
                }

                return await Task.Run(command).WaitAsync(TimeSpan.FromMilliseconds(timeout));
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

                if (this.IsConnectionException(ex))
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

        private string ResolveSceneName(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                return sceneName;
            }
            return this.OBSWebsocket.GetCurrentProgramScene();
        }

        private bool TryGetSceneItemReference(string sceneName, string sourceName, out string targetSceneName, out int sceneItemId)
        {
            targetSceneName = sceneName;
            sceneItemId = 0;

            if (this.TryGetSceneItemId(sceneName, sourceName, out sceneItemId))
            {
                return true;
            }

            foreach (SceneItemDetails sceneItem in this.OBSWebsocket.GetSceneItemList(sceneName) ?? new List<SceneItemDetails>())
            {
                if (!string.Equals(sceneItem.SourceKind, "group", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (this.TryGetSceneItemId(sceneItem.SourceName, sourceName, out sceneItemId))
                {
                    targetSceneName = sceneItem.SourceName;
                    return true;
                }
            }

            foreach (string groupName in this.OBSWebsocket.GetGroupList() ?? new List<string>())
            {
                if (this.TryGetSceneItemId(groupName, sourceName, out sceneItemId))
                {
                    targetSceneName = groupName;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetSceneItemId(string sceneName, string sourceName, out int sceneItemId)
        {
            sceneItemId = 0;

            try
            {
                sceneItemId = this.OBSWebsocket.GetSceneItemId(sceneName, sourceName, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void LogConnectFailure(Exception ex)
        {
            Exception current = this.GetInnermostException(ex);

            if (this.IsConnectionException(ex))
            {
                Logger.Log(LogLevel.Warning, "OBS Studio connection failed: " + current.Message);
                Logger.Log(LogLevel.Warning, ex, includeStackTrace: true);
            }
            else
            {
                Logger.Log(ex);
            }
        }

        private bool IsConnectionException(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            Exception current = this.GetInnermostException(ex);

            if (current is TimeoutException || current is OperationCanceledException || current is WebSocketException)
            {
                return true;
            }

            if (current is InvalidOperationException && current.Message.IndexOf("WebSocket", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private Exception GetInnermostException(Exception ex)
        {
            Exception current = ex;
            if (current is AggregateException aggregate)
            {
                aggregate = aggregate.Flatten();
                if (aggregate.InnerExceptions.Count > 0)
                {
                    current = aggregate.InnerExceptions[0];
                }
            }

            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current;
        }

        private async Task WaitForConnectedState(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (this.OBSWebsocket.IsConnected)
                {
                    return;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException("OBS Studio connection timed out");
        }
    }
}
