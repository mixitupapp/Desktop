using MixItUp.Base.Model;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class GoogleCloudTTSService : ITextToSpeechConnectableService
    {
        public static readonly IEnumerable<TextToSpeechVoice> AvailableVoices = new List<TextToSpeechVoice>()
        {
            new TextToSpeechVoice("en-US-Neural2-A", "Neural2-A (Male) - English (US)"),
            new TextToSpeechVoice("en-US-Neural2-C", "Neural2-C (Female) - English (US)"),
            new TextToSpeechVoice("en-US-Neural2-D", "Neural2-D (Male) - English (US)"),
            new TextToSpeechVoice("en-US-Neural2-E", "Neural2-E (Female) - English (US)"),
            new TextToSpeechVoice("en-US-Neural2-F", "Neural2-F (Female) - English (US)"),
            new TextToSpeechVoice("en-US-Neural2-G", "Neural2-G (Female) - English (US)"),
            new TextToSpeechVoice("en-US-Neural2-H", "Neural2-H (Female) - English (US)"),
            new TextToSpeechVoice("en-US-Neural2-I", "Neural2-I (Male) - English (US)"),
            new TextToSpeechVoice("en-US-Neural2-J", "Neural2-J (Male) - English (US)"),
            // to do: add voices
        };

        public TextToSpeechProviderType ProviderType { get { return TextToSpeechProviderType.GoogleCloudTTS; } }

        public int VolumeMinimum { get { return 0; } }
        public int VolumeMaximum { get { return 100; } }
        public int VolumeDefault { get { return 100; } }

        public int PitchMinimum { get { return -20; } }
        public int PitchMaximum { get { return 20; } }
        public int PitchDefault { get { return 0; } }

        public int RateMinimum { get { return 25; } }
        public int RateMaximum { get { return 200; } }
        public int RateDefault { get { return 100; } }

        public string Name { get { return Resources.GoogleCloudTTS; } }

        public bool IsConnected { get; private set; }

        public IEnumerable<TextToSpeechVoice> GetVoices()
        {
            return GoogleCloudTTSService.AvailableVoices;
        }

        public async Task<Result> TestAccess()
        {
            try
            {
                if (string.IsNullOrEmpty(ChannelSession.Settings.GoogleCloudTTSCustomKey))
                {
                    return new Result(Resources.GoogleCloudTTSNoAPIKey);
                }

                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync($"https://texttospeech.googleapis.com/v1/voices?key={ChannelSession.Settings.GoogleCloudTTSCustomKey}");

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        return new Result($"API request failed: {response.StatusCode} - {error}");
                    }

                    string content = await response.Content.ReadAsStringAsync();
                    JObject jsonResponse = JObject.Parse(content);

                    if (jsonResponse.ContainsKey("voices") && jsonResponse["voices"] is JArray voices && voices.Count > 0)
                    {
                        return new Result();
                    }

                    return new Result(Resources.GoogleCloudTTSNoVoicesReturned);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result(ex);
            }
        }

        public async Task Speak(string outputDevice, Guid overlayEndpointID, string text, string voice, int volume, int pitch, int rate, bool ssml, bool waitForFinish)
        {
            try
            {
                if (string.IsNullOrEmpty(ChannelSession.Settings.GoogleCloudTTSCustomKey))
                {
                    Logger.Log(LogLevel.Error, "Google Cloud TTS requires API key.");
                    await ServiceManager.Get<ChatService>().SendMessage("Google Cloud TTS Error: API key required", StreamingPlatformTypeEnum.All);
                    return;
                }

                string[] voiceParts = voice.Split('-');
                string languageCode = voiceParts.Length >= 2 ? $"{voiceParts[0]}-{voiceParts[1]}" : "en-US";

                JObject requestBody = new JObject();

                if (ssml)
                {
                    requestBody["input"] = new JObject { { "ssml", text } };
                }
                else
                {
                    requestBody["input"] = new JObject { { "text", text } };
                }

                requestBody["voice"] = new JObject
                {
                    { "languageCode", languageCode },
                    { "name", voice }
                };

                requestBody["audioConfig"] = new JObject
                {
                    { "audioEncoding", "MP3" },
                    { "pitch", pitch },
                    { "speakingRate", rate / 100.0 }
                };

                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    HttpResponseMessage response = await client.PostAsync($"https://texttospeech.googleapis.com/v1/text:synthesize?key={ChannelSession.Settings.GoogleCloudTTSCustomKey}", AdvancedHttpClient.CreateContentFromObject(requestBody));

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        JObject jsonResponse = JObject.Parse(responseContent);

                        if (jsonResponse.ContainsKey("audioContent"))
                        {
                            string base64Audio = jsonResponse["audioContent"].ToString();
                            byte[] audioBytes = Convert.FromBase64String(base64Audio);

                            MemoryStream stream = new MemoryStream(audioBytes);
                            await ServiceManager.Get<IAudioService>().PlayMP3Stream(stream, volume, outputDevice, waitForFinish: waitForFinish);
                        }
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        Logger.Log(LogLevel.Error, "Google Cloud TTS Error: " + error);
                        // await ServiceManager.Get<ChatService>().SendMessage("Google Cloud TTS Error: " + error, StreamingPlatformTypeEnum.All);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}