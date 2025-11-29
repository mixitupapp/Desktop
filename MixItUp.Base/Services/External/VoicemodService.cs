using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class VoicemodWebSocketRequestPacket
    {
        public string action { get; set; }
        public string id { get; set; } = Guid.NewGuid().ToString();
        public JObject payload { get; set; } = new JObject();

        private VoicemodWebSocketRequestPacket() { }

        public VoicemodWebSocketRequestPacket(string action)
        {
            this.action = action;
        }

        public VoicemodWebSocketRequestPacket(string action, JObject payload)
            : this(action)
        {
            this.payload = payload;
        }
    }

    public class VoicemodWebSocket : ClientWebSocketBase
    {
        private Dictionary<string, JObject> responses = new Dictionary<string, JObject>();

        public event EventHandler<JObject> OnVoiceChangedEvent;

        public override Task<bool> Connect(string endpoint)
        {
            this.responses.Clear();
            return base.Connect(endpoint);
        }

        public async Task<JObject> SendAndReceive(VoicemodWebSocketRequestPacket packet, int delaySeconds = 5)
        {
            Logger.Log(LogLevel.Debug, "Voicemod Packet Sent - " + JSONSerializerHelper.SerializeToString(packet));

            this.responses[packet.id] = null;

            await this.Send(JSONSerializerHelper.SerializeToString(packet));

            int cycles = delaySeconds * 10;
            JObject response = null;
            for (int i = 0; i < cycles && response == null; i++)
            {
                this.responses.TryGetValue(packet.id, out response);
                await Task.Delay(100);
            }

            this.responses.Remove(packet.id);
            return response;
        }

        protected override Task ProcessReceivedPacket(string packet)
        {
            try
            {
                Logger.Log(LogLevel.Debug, "Voicemod Packet Received - " + packet);

                JObject response = JObject.Parse(packet);
                if (response != null)
                {
                    string actionType = response["actionType"]?.ToString();
                    if (actionType == "voiceChangedEvent")
                    {
                        this.OnVoiceChangedEvent?.Invoke(this, response);
                    }

                    string responseId = response["id"]?.ToString();
                    if (!string.IsNullOrEmpty(responseId) && this.responses.ContainsKey(responseId))
                    {
                        this.responses[responseId] = response;
                    }
                    else if (this.responses.Keys.Count > 0)
                    {
                        this.responses[this.responses.Keys.First()] = response;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return Task.FromResult(0);
        }
    }

    public class VoicemodService : IVoicemodService
    {
        private static readonly List<int> AvailablePorts = new List<int>() { 59129, 20000, 39273, 42152, 43782, 46667, 35679, 37170, 38501, 33952, 30546 };

        public string Name { get { return MixItUp.Base.Resources.Voicemod; } }
        public bool IsConnected { get { return this.WebSocketConnected; } }
        public bool WebSocketConnected { get; private set; }

        private VoicemodWebSocket websocket = new VoicemodWebSocket();
        private string currentVoiceID = null;
        private string previousVoiceID = null;

        public VoicemodService() { }

        public async Task<Result> Connect()
        {
            try
            {
                return await this.ConnectWebSocket();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return new Result(MixItUp.Base.Resources.VoicemodConnectionFailed);
        }

        public async Task Disconnect()
        {
            this.WebSocketConnected = false;
            this.websocket.OnDisconnectOccurred -= Websocket_OnDisconnectOccurred;
            this.websocket.OnVoiceChangedEvent -= Websocket_OnVoiceChangedEvent;
            await this.websocket.Disconnect();
        }

        public async Task<IEnumerable<VoicemodVoiceModel>> GetVoices()
        {
            var results = new Dictionary<string, VoicemodVoiceModel>();

            JObject response = await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("getVoices"));
            if (response != null)
            {
                JToken actionObj = response["actionObject"];
                if (actionObj != null && actionObj["voices"] is JArray voices)
                {
                    foreach (JToken v in voices)
                    {
                        var voice = new VoicemodVoiceModel
                        {
                            voiceID = v["id"]?.ToString(),
                            friendlyName = v["friendlyName"]?.ToString(),
                            Enabled = v["enabled"]?.ToObject<bool>() ?? false,
                            IsFavorite = v["favorited"]?.ToObject<bool>() ?? false,
                            IsCustom = v["isCustom"]?.ToObject<bool>() ?? false
                        };

                        if (!string.IsNullOrEmpty(voice.voiceID) && voice.Enabled)
                        {
                            results[voice.voiceID] = voice;
                        }
                    }
                }
            }

            return results.Values.ToList();
        }

        public async Task VoiceChangerOnOff(bool state)
        {
            JObject response = await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("getVoiceChangerStatus"));
            if (response != null)
            {
                JToken actionObj = response["actionObject"];
                if (actionObj != null && actionObj["value"] != null)
                {
                    bool current = actionObj["value"].ToObject<bool>();
                    if (current != state)
                    {
                        await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("toggleVoiceChanger"));
                    }
                }
            }
        }

        public async Task SelectVoice(string voiceID)
        {
            await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("loadVoice", new JObject()
            {
                { "voiceID", voiceID }
            }));
        }

        public async Task SelectPreviousVoice()
        {
            if (!string.IsNullOrEmpty(this.previousVoiceID))
            {
                await this.SelectVoice(this.previousVoiceID);
            }
        }

        public async Task RandomVoice(VoicemodRandomVoiceType voiceType)
        {
            await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("selectRandomVoice", new JObject()
            {
                { "mode", voiceType.ToString() }
            }));
        }

        public async Task BeepSoundOnOff(bool state)
        {
            await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("setBeepSound", new JObject()
            {
                { "badLanguage", state ? 1 : 0 }
            }));
        }

        public async Task HearMyselfOnOff(bool state)
        {
            JObject response = await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("getHearMyselfStatus"));
            if (response != null)
            {
                JToken actionObj = response["actionObject"];
                if (actionObj != null && actionObj["value"] != null)
                {
                    bool current = actionObj["value"].ToObject<bool>();
                    if (current != state)
                    {
                        await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("toggleHearMyVoice"));
                    }
                }
            }
        }

        public async Task MuteOnOff(bool state)
        {
            JObject response = await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("getMuteMicStatus"));
            if (response != null)
            {
                JToken actionObj = response["actionObject"];
                if (actionObj != null && actionObj["value"] != null)
                {
                    bool current = actionObj["value"].ToObject<bool>();
                    if (current != state)
                    {
                        await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("toggleMuteMic"));
                    }
                }
            }
        }

        public async Task<IEnumerable<VoicemodMemeModel>> GetMemeSounds()
        {
            List<VoicemodMemeModel> results = new List<VoicemodMemeModel>();

            JObject response = await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("getMemes"));
            if (response != null)
            {
                JToken actionObj = response["actionObject"];
                if (actionObj != null && actionObj["listOfMemes"] is JArray memeSounds)
                {
                    foreach (VoicemodMemeModel memeSound in memeSounds.ToTypedArray<VoicemodMemeModel>())
                    {
                        results.Add(memeSound);
                    }
                }
            }

            return results;
        }

        public async Task PlayMemeSound(string fileName)
        {
            await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("playMeme", new JObject()
            {
                { "FileName", fileName },
                { "IsKeyDown", true }
            }));
        }

        public async Task StopAllMemeSounds()
        {
            await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("stopAllMemeSounds"));
        }

        private async Task<Result> ConnectWebSocket()
        {
            this.websocket.OnDisconnectOccurred -= Websocket_OnDisconnectOccurred;
            this.websocket.OnVoiceChangedEvent -= Websocket_OnVoiceChangedEvent;

            string clientKey = ServiceManager.Get<SecretsService>().GetSecret("VoicemodV3ClientKey");

            foreach (int port in VoicemodService.AvailablePorts)
            {
                try
                {
                    if (await this.websocket.Connect(string.Format("ws://localhost:{0}/v1/", port)))
                    {
                        JObject response = await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("registerClient", new JObject()
                        {
                            { "clientKey", clientKey }
                        }));

                        if (response != null && response["payload"] != null)
                        {
                            var payload = response["payload"] as JObject;
                            var status = payload["status"] as JObject;

                            if (status != null && status["code"]?.ToObject<int>() == 200)
                            {
                                this.WebSocketConnected = true;
                                this.websocket.OnDisconnectOccurred += Websocket_OnDisconnectOccurred;
                                this.websocket.OnVoiceChangedEvent += Websocket_OnVoiceChangedEvent;

                                await this.GetCurrentVoice();

                                ServiceManager.Get<ITelemetryService>().TrackService("Voicemod");
                                return new Result();
                            }
                        }
                    }
                    await this.websocket.Disconnect(WebSocketCloseStatus.NormalClosure);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }

            return new Result(MixItUp.Base.Resources.VoicemodConnectionFailed);
        }

        private async Task GetCurrentVoice()
        {
            JObject response = await this.websocket.SendAndReceive(new VoicemodWebSocketRequestPacket("getCurrentVoice"));
            if (response != null)
            {
                JToken actionObj = response["actionObject"];
                if (actionObj != null && actionObj["voiceID"] != null)
                {
                    this.currentVoiceID = actionObj["voiceID"].ToString();
                }
            }
        }

        private void Websocket_OnVoiceChangedEvent(object sender, JObject eventData)
        {
            try
            {
                JToken actionObj = eventData["actionObject"];
                if (actionObj != null && actionObj["voiceID"] != null)
                {
                    string newVoiceID = actionObj["voiceID"].ToString();

                    if (newVoiceID != this.currentVoiceID)
                    {
                        this.previousVoiceID = this.currentVoiceID;
                        this.currentVoiceID = newVoiceID;
                        Logger.Log(LogLevel.Debug, $"Voicemod - Voice changed from {this.previousVoiceID} to {this.currentVoiceID}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async void Websocket_OnDisconnectOccurred(object sender, System.Net.WebSockets.WebSocketCloseStatus e)
        {
            ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.Voicemod);

            Result result = new Result();
            do
            {
                await this.Disconnect();
                await Task.Delay(5000);
                result = await this.ConnectWebSocket();
            }
            while (!result.Success);

            ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.Voicemod);
        }
    }
}