using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    public class WebhookHubConnection
    {
        private sealed class ListenerRegistration
        {
            public Type[] ParameterTypes { get; set; }
            public Action<object[]> Handler { get; set; }
        }

        private readonly AdvancedClientWebSocket webSocket = new AdvancedClientWebSocket();
        private readonly Dictionary<string, List<ListenerRegistration>> listeners = new Dictionary<string, List<ListenerRegistration>>(StringComparer.OrdinalIgnoreCase);
        private readonly object listenerLock = new object();

        private int disconnectRequested = 0;

        public string Address { get; private set; }

        public event EventHandler Connected;
        public event EventHandler<Exception> Disconnected;

        public WebhookHubConnection(string address)
        {
            this.Address = address;

            this.webSocket.PacketReceived += this.WebSocket_PacketReceived;
            this.webSocket.Disconnected += this.WebSocket_Disconnected;
        }

        public void Listen(string methodName, Action handler)
        {
            this.RegisterListener(methodName, Array.Empty<Type>(), args => handler?.Invoke());
        }

        public void Listen<T1>(string methodName, Action<T1> handler)
        {
            this.RegisterListener(methodName, new Type[] { typeof(T1) }, args => handler?.Invoke((T1)args[0]));
        }

        public void Listen<T1, T2>(string methodName, Action<T1, T2> handler)
        {
            this.RegisterListener(methodName, new Type[] { typeof(T1), typeof(T2) }, args => handler?.Invoke((T1)args[0], (T2)args[1]));
        }

        public void Listen<T1, T2, T3>(string methodName, Action<T1, T2, T3> handler)
        {
            this.RegisterListener(methodName, new Type[] { typeof(T1), typeof(T2), typeof(T3) }, args => handler?.Invoke((T1)args[0], (T2)args[1], (T3)args[2]));
        }

        public void Listen<T1, T2, T3, T4>(string methodName, Action<T1, T2, T3, T4> handler)
        {
            this.RegisterListener(methodName, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, args => handler?.Invoke((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]));
        }

        public async Task<bool> Connect()
        {
            try
            {
                Interlocked.Exchange(ref this.disconnectRequested, 0);

                bool connected = await this.webSocket.Connect(this.Address, CancellationToken.None);
                if (connected)
                {
                    this.Connected?.Invoke(this, EventArgs.Empty);
                }
                return connected;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return false;
            }
        }

        public bool IsConnected()
        {
            return this.webSocket.IsOpen();
        }

        public Task Send(string methodName)
        {
            return this.SendInternal(methodName);
        }

        public Task Send(string methodName, object item1)
        {
            return this.SendInternal(methodName, item1);
        }

        public Task Send(string methodName, object item1, object item2)
        {
            return this.SendInternal(methodName, item1, item2);
        }

        public Task Send(string methodName, object item1, object item2, object item3)
        {
            return this.SendInternal(methodName, item1, item2, item3);
        }

        public async Task Disconnect()
        {
            Interlocked.Exchange(ref this.disconnectRequested, 1);
            await this.webSocket.Disconnect();
        }

        private void RegisterListener(string methodName, Type[] parameterTypes, Action<object[]> handler)
        {
            if (string.IsNullOrWhiteSpace(methodName) || handler == null)
            {
                return;
            }

            lock (this.listenerLock)
            {
                if (!this.listeners.TryGetValue(methodName, out List<ListenerRegistration> methodListeners))
                {
                    methodListeners = new List<ListenerRegistration>();
                    this.listeners[methodName] = methodListeners;
                }

                methodListeners.Add(new ListenerRegistration()
                {
                    ParameterTypes = parameterTypes,
                    Handler = handler,
                });
            }
        }

        private Task SendInternal(string methodName, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return Task.CompletedTask;
            }

            return this.webSocket.Send(new JObject()
            {
                ["method"] = methodName,
                ["arguments"] = JArray.FromObject(args ?? Array.Empty<object>()),
            });
        }

        private void WebSocket_PacketReceived(object sender, string packet)
        {
            _ = Task.Run(() => this.ProcessPacket(packet));
        }

        private void ProcessPacket(string packet)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packet))
                {
                    return;
                }

                JObject message = JObject.Parse(packet);
                string methodName = message["method"]?.Value<string>() ?? message["target"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(methodName))
                {
                    return;
                }

                JArray arguments = message["arguments"] as JArray ?? new JArray();

                List<ListenerRegistration> methodListeners = null;
                lock (this.listenerLock)
                {
                    if (this.listeners.TryGetValue(methodName, out List<ListenerRegistration> listenersForMethod))
                    {
                        methodListeners = new List<ListenerRegistration>(listenersForMethod);
                    }
                }

                if (methodListeners == null || methodListeners.Count == 0)
                {
                    return;
                }

                foreach (ListenerRegistration listener in methodListeners)
                {
                    if (!this.TryConvertArguments(arguments, listener.ParameterTypes, out object[] convertedArguments))
                    {
                        continue;
                    }

                    try
                    {
                        listener.Handler(convertedArguments);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private bool TryConvertArguments(JArray sourceArguments, Type[] parameterTypes, out object[] convertedArguments)
        {
            convertedArguments = new object[parameterTypes.Length];

            if (sourceArguments.Count < parameterTypes.Length)
            {
                return false;
            }

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                if (!this.TryConvertArgument(sourceArguments[i], parameterTypes[i], out object convertedArgument))
                {
                    return false;
                }
                convertedArguments[i] = convertedArgument;
            }
            return true;
        }

        private bool TryConvertArgument(JToken token, Type type, out object convertedArgument)
        {
            convertedArgument = null;

            try
            {
                if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                {
                    convertedArgument = type.IsValueType ? Activator.CreateInstance(type) : null;
                    return true;
                }

                convertedArgument = token.ToObject(type);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return false;
            }
        }

        private void WebSocket_Disconnected(object sender, WebSocketCloseStatus closeStatus)
        {
            if (Interlocked.CompareExchange(ref this.disconnectRequested, 0, 0) == 1)
            {
                return;
            }

            this.Disconnected?.Invoke(this, new WebSocketException($"Webhook hub disconnected: {closeStatus}"));
        }
    }
}