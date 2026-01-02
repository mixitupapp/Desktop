using MixItUp.Base.Model;
using Newtonsoft.Json.Linq;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class EdgeTTSService : ITextToSpeechService
    {
        public static readonly IEnumerable<TextToSpeechVoice> AvailableVoices = new List<TextToSpeechVoice>()
        {
            // Afrikaans (South Africa)
            new TextToSpeechVoice("af-ZA-AdriNeural", "Adri (Female) - Afrikaans"),
            new TextToSpeechVoice("af-ZA-WillemNeural", "Willem (Male) - Afrikaans"),

            // Amharic (Ethiopia)
            new TextToSpeechVoice("am-ET-MekdesNeural", "Mekdes (Female) - Amharic"),
            new TextToSpeechVoice("am-ET-AmehaNeural", "Ameha (Male) - Amharic"),

            // Arabic (United Arab Emirates)
            new TextToSpeechVoice("ar-AE-FatimaNeural", "Fatima (Female) - Arabic"),
            new TextToSpeechVoice("ar-AE-HamdanNeural", "Hamdan (Male) - Arabic"),

            // Arabic (Bahrain)
            new TextToSpeechVoice("ar-BH-LailaNeural", "Laila (Female) - Arabic"),

            // Arabic (Algeria)
            new TextToSpeechVoice("ar-DZ-AminaNeural", "Amina (Female) - Arabic"),
            new TextToSpeechVoice("ar-DZ-IsmaelNeural", "Ismael (Male) - Arabic"),

            // Arabic (Egypt)
            new TextToSpeechVoice("ar-EG-SalmaNeural", "Salma (Female) - Arabic"),

            // Arabic (Iraq)
            new TextToSpeechVoice("ar-IQ-RanaNeural", "Rana (Female) - Arabic"),

            // Arabic (Jordan)
            new TextToSpeechVoice("ar-JO-SanaNeural", "Sana (Female) - Arabic"),

            // Arabic (Kuwait)
            new TextToSpeechVoice("ar-KW-NouraNeural", "Noura (Female) - Arabic"),

            // Arabic (Lebanon)
            new TextToSpeechVoice("ar-LB-LaylaNeural", "Layla (Female) - Arabic"),
            new TextToSpeechVoice("ar-LB-RamiNeural", "Rami (Male) - Arabic"),

            // Arabic (Libya)
            new TextToSpeechVoice("ar-LY-ImanNeural", "Iman (Female) - Arabic"),

            // Arabic (Qatar)
            new TextToSpeechVoice("ar-QA-AmalNeural", "Amal (Female) - Arabic"),
            new TextToSpeechVoice("ar-QA-MoazNeural", "Moaz (Male) - Arabic"),

            // Arabic (Saudi Arabia)
            new TextToSpeechVoice("ar-SA-ZariyahNeural", "Zariyah (Female) - Arabic"),
            new TextToSpeechVoice("ar-SA-HamedNeural", "Hamed (Male) - Arabic"),

            // Arabic (Tunisia)
            new TextToSpeechVoice("ar-TN-ReemNeural", "Reem (Female) - Arabic"),
            new TextToSpeechVoice("ar-TN-HediNeural", "Hedi (Male) - Arabic"),

            // Arabic (Yemen)
            new TextToSpeechVoice("ar-YE-MaryamNeural", "Maryam (Female) - Arabic"),
            new TextToSpeechVoice("ar-YE-SalehNeural", "Saleh (Male) - Arabic"),

            // Bulgarian (Bulgaria)
            new TextToSpeechVoice("bg-BG-KalinaNeural", "Kalina (Female) - Bulgarian"),
            new TextToSpeechVoice("bg-BG-BorislavNeural", "Borislav (Male) - Bulgarian"),

            // Catalan
            new TextToSpeechVoice("ca-ES-JoanaNeural", "Joana (Female) - Catalan"),
            new TextToSpeechVoice("ca-ES-EnricNeural", "Enric (Male) - Catalan"),

            // Czech (Czechia)
            new TextToSpeechVoice("cs-CZ-VlastaNeural", "Vlasta (Female) - Czech"),
            new TextToSpeechVoice("cs-CZ-AntoninNeural", "Antonin (Male) - Czech"),

            // Danish (Denmark)
            new TextToSpeechVoice("da-DK-ChristelNeural", "Christel (Female) - Danish"),
            new TextToSpeechVoice("da-DK-JeppeNeural", "Jeppe (Male) - Danish"),

            // German (Austria)
            new TextToSpeechVoice("de-AT-IngridNeural", "Ingrid (Female) - German"),
            new TextToSpeechVoice("de-AT-JonasNeural", "Jonas (Male) - German"),

            // German (Switzerland)
            new TextToSpeechVoice("de-CH-LeniNeural", "Leni (Female) - German"),
            new TextToSpeechVoice("de-CH-JanNeural", "Jan (Male) - German"),

            // German (Germany)
            new TextToSpeechVoice("de-DE-KatjaNeural", "Katja (Female) - German"),
            new TextToSpeechVoice("de-DE-ConradNeural", "Conrad (Male) - German"),
            new TextToSpeechVoice("de-DE-AmalaNeural", "Amala (Female) - German"),

            // Greek (Greece)
            new TextToSpeechVoice("el-GR-AthinaNeural", "Athina (Female) - Greek"),
            new TextToSpeechVoice("el-GR-NestorasNeural", "Nestoras (Male) - Greek"),

            // English (Australia)
            new TextToSpeechVoice("en-AU-NatashaNeural", "Natasha (Female) - English"),
            new TextToSpeechVoice("en-AU-WilliamNeural", "William (Male) - English"),

            // English (Canada)
            new TextToSpeechVoice("en-CA-ClaraNeural", "Clara (Female) - English"),
            new TextToSpeechVoice("en-CA-LiamNeural", "Liam (Male) - English"),

            // English (United Kingdom)
            new TextToSpeechVoice("en-GB-SoniaNeural", "Sonia (Female) - English"),
            new TextToSpeechVoice("en-GB-RyanNeural", "Ryan (Male) - English"),
            new TextToSpeechVoice("en-GB-LibbyNeural", "Libby (Female) - English"),
            new TextToSpeechVoice("en-GB-MaisieNeural", "Maisie (Female, Child) - English"),
            new TextToSpeechVoice("en-GB-ThomasNeural", "Thomas (Male) - English"),

            // English (Hong Kong SAR)
            new TextToSpeechVoice("en-HK-YanNeural", "Yan (Female) - English"),
            new TextToSpeechVoice("en-HK-SamNeural", "Sam (Male) - English"),

            // English (Ireland)
            new TextToSpeechVoice("en-IE-EmilyNeural", "Emily (Female) - English"),
            new TextToSpeechVoice("en-IE-ConnorNeural", "Connor (Male) - English"),

            // English (India)
            new TextToSpeechVoice("en-IN-NeerjaNeural", "Neerja (Female) - English"),
            new TextToSpeechVoice("en-IN-PrabhatNeural", "Prabhat (Male) - English"),

            // English (Kenya)
            new TextToSpeechVoice("en-KE-AsiliaNeural", "Asilia (Female) - English"),
            new TextToSpeechVoice("en-KE-ChilembaNeural", "Chilemba (Male) - English"),

            // English (Nigeria)
            new TextToSpeechVoice("en-NG-EzinneNeural", "Ezinne (Female) - English"),
            new TextToSpeechVoice("en-NG-AbeoNeural", "Abeo (Male) - English"),

            // English (New Zealand)
            new TextToSpeechVoice("en-NZ-MollyNeural", "Molly (Female) - English"),
            new TextToSpeechVoice("en-NZ-MitchellNeural", "Mitchell (Male) - English"),

            // English (Philippines)
            new TextToSpeechVoice("en-PH-RosaNeural", "Rosa (Female) - English"),
            new TextToSpeechVoice("en-PH-JamesNeural", "James (Male) - English"),

            // English (Singapore)
            new TextToSpeechVoice("en-SG-LunaNeural", "Luna (Female) - English"),
            new TextToSpeechVoice("en-SG-WayneNeural", "Wayne (Male) - English"),

            // English (Tanzania)
            new TextToSpeechVoice("en-TZ-ImaniNeural", "Imani (Female) - English"),
            new TextToSpeechVoice("en-TZ-ElimuNeural", "Elimu (Male) - English"),

            // English (United States)
            new TextToSpeechVoice("en-US-AvaNeural", "Ava (Female) - English"),
            new TextToSpeechVoice("en-US-AndrewNeural", "Andrew (Male) - English"),
            new TextToSpeechVoice("en-US-EmmaNeural", "Emma (Female) - English"),
            new TextToSpeechVoice("en-US-BrianNeural", "Brian (Male) - English"),
            new TextToSpeechVoice("en-US-JennyNeural", "Jenny (Female) - English"),
            new TextToSpeechVoice("en-US-GuyNeural", "Guy (Male) - English"),
            new TextToSpeechVoice("en-US-AriaNeural", "Aria (Female) - English"),
            new TextToSpeechVoice("en-US-AnaNeural", "Ana (Female, Child) - English"),
            new TextToSpeechVoice("en-US-ChristopherNeural", "Christopher (Male) - English"),
            new TextToSpeechVoice("en-US-EricNeural", "Eric (Male) - English"),
            new TextToSpeechVoice("en-US-MichelleNeural", "Michelle (Female) - English"),
            new TextToSpeechVoice("en-US-RogerNeural", "Roger (Male) - English"),
            new TextToSpeechVoice("en-US-SteffanNeural", "Steffan (Male) - English"),

            // English (South Africa)
            new TextToSpeechVoice("en-ZA-LeahNeural", "Leah (Female) - English"),
            new TextToSpeechVoice("en-ZA-LukeNeural", "Luke (Male) - English"),

            // Spanish (Argentina)
            new TextToSpeechVoice("es-AR-ElenaNeural", "Elena (Female) - Spanish"),
            new TextToSpeechVoice("es-AR-TomasNeural", "Tomas (Male) - Spanish"),

            // Spanish (Bolivia)
            new TextToSpeechVoice("es-BO-SofiaNeural", "Sofia (Female) - Spanish"),
            new TextToSpeechVoice("es-BO-MarceloNeural", "Marcelo (Male) - Spanish"),

            // Spanish (Chile)
            new TextToSpeechVoice("es-CL-CatalinaNeural", "Catalina (Female) - Spanish"),
            new TextToSpeechVoice("es-CL-LorenzoNeural", "Lorenzo (Male) - Spanish"),

            // Spanish (Colombia)
            new TextToSpeechVoice("es-CO-SalomeNeural", "Salome (Female) - Spanish"),
            new TextToSpeechVoice("es-CO-GonzaloNeural", "Gonzalo (Male) - Spanish"),

            // Spanish (Costa Rica)
            new TextToSpeechVoice("es-CR-MariaNeural", "Maria (Female) - Spanish"),
            new TextToSpeechVoice("es-CR-JuanNeural", "Juan (Male) - Spanish"),

            // Spanish (Cuba)
            new TextToSpeechVoice("es-CU-BelkysNeural", "Belkys (Female) - Spanish"),
            new TextToSpeechVoice("es-CU-ManuelNeural", "Manuel (Male) - Spanish"),

            // Spanish (Dominican Republic)
            new TextToSpeechVoice("es-DO-RamonaNeural", "Ramona (Female) - Spanish"),
            new TextToSpeechVoice("es-DO-EmilioNeural", "Emilio (Male) - Spanish"),

            // Spanish (Ecuador)
            new TextToSpeechVoice("es-EC-AndreaNeural", "Andrea (Female) - Spanish"),
            new TextToSpeechVoice("es-EC-LuisNeural", "Luis (Male) - Spanish"),

            // Spanish (Spain)
            new TextToSpeechVoice("es-ES-ElviraNeural", "Elvira (Female) - Spanish"),
            new TextToSpeechVoice("es-ES-AlvaroNeural", "Alvaro (Male) - Spanish"),
            new TextToSpeechVoice("es-ES-XimenaNeural", "Ximena (Female) - Spanish"),

            // Spanish (Equatorial Guinea)
            new TextToSpeechVoice("es-GQ-TeresaNeural", "Teresa (Female) - Spanish"),
            new TextToSpeechVoice("es-GQ-JavierNeural", "Javier (Male) - Spanish"),

            // Spanish (Guatemala)
            new TextToSpeechVoice("es-GT-MartaNeural", "Marta (Female) - Spanish"),
            new TextToSpeechVoice("es-GT-AndresNeural", "Andres (Male) - Spanish"),

            // Spanish (Honduras)
            new TextToSpeechVoice("es-HN-KarlaNeural", "Karla (Female) - Spanish"),
            new TextToSpeechVoice("es-HN-CarlosNeural", "Carlos (Male) - Spanish"),

            // Spanish (Mexico)
            new TextToSpeechVoice("es-MX-DaliaNeural", "Dalia (Female) - Spanish"),
            new TextToSpeechVoice("es-MX-JorgeNeural", "Jorge (Male) - Spanish"),

            // Spanish (Nicaragua)
            new TextToSpeechVoice("es-NI-YolandaNeural", "Yolanda (Female) - Spanish"),
            new TextToSpeechVoice("es-NI-FedericoNeural", "Federico (Male) - Spanish"),

            // Spanish (Panama)
            new TextToSpeechVoice("es-PA-MargaritaNeural", "Margarita (Female) - Spanish"),
            new TextToSpeechVoice("es-PA-RobertoNeural", "Roberto (Male) - Spanish"),

            // Spanish (Peru)
            new TextToSpeechVoice("es-PE-CamilaNeural", "Camila (Female) - Spanish"),
            new TextToSpeechVoice("es-PE-AlexNeural", "Alex (Male) - Spanish"),

            // Spanish (Puerto Rico)
            new TextToSpeechVoice("es-PR-KarinaNeural", "Karina (Female) - Spanish"),
            new TextToSpeechVoice("es-PR-VictorNeural", "Victor (Male) - Spanish"),

            // Spanish (Paraguay)
            new TextToSpeechVoice("es-PY-TaniaNeural", "Tania (Female) - Spanish"),
            new TextToSpeechVoice("es-PY-MarioNeural", "Mario (Male) - Spanish"),

            // Spanish (El Salvador)
            new TextToSpeechVoice("es-SV-LorenaNeural", "Lorena (Female) - Spanish"),
            new TextToSpeechVoice("es-SV-RodrigoNeural", "Rodrigo (Male) - Spanish"),

            // Spanish (United States)
            new TextToSpeechVoice("es-US-PalomaNeural", "Paloma (Female) - Spanish"),
            new TextToSpeechVoice("es-US-AlonsoNeural", "Alonso (Male) - Spanish"),

            // Spanish (Uruguay)
            new TextToSpeechVoice("es-UY-ValentinaNeural", "Valentina (Female) - Spanish"),
            new TextToSpeechVoice("es-UY-MateoNeural", "Mateo (Male) - Spanish"),

            // Spanish (Venezuela)
            new TextToSpeechVoice("es-VE-PaolaNeural", "Paola (Female) - Spanish"),
            new TextToSpeechVoice("es-VE-SebastianNeural", "Sebastian (Male) - Spanish"),

            // Finnish (Finland)
            new TextToSpeechVoice("fi-FI-HarriNeural", "Harri (Male) - Finnish"),
            new TextToSpeechVoice("fi-FI-NooraNeural", "Noora (Female) - Finnish"),

            // French (Belgium)
            new TextToSpeechVoice("fr-BE-CharlineNeural", "Charline (Female) - French"),
            new TextToSpeechVoice("fr-BE-GerardNeural", "Gerard (Male) - French"),

            // French (Canada)
            new TextToSpeechVoice("fr-CA-SylvieNeural", "Sylvie (Female) - French"),
            new TextToSpeechVoice("fr-CA-JeanNeural", "Jean (Male) - French"),
            new TextToSpeechVoice("fr-CA-AntoineNeural", "Antoine (Male) - French"),
            new TextToSpeechVoice("fr-CA-ThierryNeural", "Thierry (Male) - French"),

            // French (Switzerland)
            new TextToSpeechVoice("fr-CH-ArianeNeural", "Ariane (Female) - French"),
            new TextToSpeechVoice("fr-CH-FabriceNeural", "Fabrice (Male) - French"),

            // French (France)
            new TextToSpeechVoice("fr-FR-DeniseNeural", "Denise (Female) - French"),
            new TextToSpeechVoice("fr-FR-HenriNeural", "Henri (Male) - French"),
            new TextToSpeechVoice("fr-FR-EloiseNeural", "Eloise (Female, Child) - French"),

            // Hindi (India)
            new TextToSpeechVoice("hi-IN-SwaraNeural", "Swara (Female) - Hindi"),
            new TextToSpeechVoice("hi-IN-MadhurNeural", "Madhur (Male) - Hindi"),

            // Croatian (Croatia)
            new TextToSpeechVoice("hr-HR-GabrijelaNeural", "Gabrijela (Female) - Croatian"),
            new TextToSpeechVoice("hr-HR-SreckoNeural", "Srecko (Male) - Croatian"),

            // Hungarian (Hungary)
            new TextToSpeechVoice("hu-HU-NoemiNeural", "Noemi (Female) - Hungarian"),
            new TextToSpeechVoice("hu-HU-TamasNeural", "Tamas (Male) - Hungarian"),

            // Indonesian (Indonesia)
            new TextToSpeechVoice("id-ID-GadisNeural", "Gadis (Female) - Indonesian"),
            new TextToSpeechVoice("id-ID-ArdiNeural", "Ardi (Male) - Indonesian"),

            // Italian (Italy)
            new TextToSpeechVoice("it-IT-IsabellaNeural", "Isabella (Female) - Italian"),
            new TextToSpeechVoice("it-IT-DiegoNeural", "Diego (Male) - Italian"),
            new TextToSpeechVoice("it-IT-GiuseppeNeural", "Giuseppe (Male) - Italian"),

            // Japanese (Japan)
            new TextToSpeechVoice("ja-JP-NanamiNeural", "Nanami (Female) - Japanese"),
            new TextToSpeechVoice("ja-JP-KeitaNeural", "Keita (Male) - Japanese"),

            // Korean (Korea)
            new TextToSpeechVoice("ko-KR-SunHiNeural", "SunHi (Female) - Korean"),
            new TextToSpeechVoice("ko-KR-InJoonNeural", "InJoon (Male) - Korean"),
            new TextToSpeechVoice("ko-KR-HyunsuNeural", "Hyunsu (Male) - Korean"),

            // Norwegian Bokmål (Norway)
            new TextToSpeechVoice("nb-NO-PernilleNeural", "Pernille (Female) - Norwegian"),
            new TextToSpeechVoice("nb-NO-FinnNeural", "Finn (Male) - Norwegian"),

            // Dutch (Belgium)
            new TextToSpeechVoice("nl-BE-DenaNeural", "Dena (Female) - Dutch"),
            new TextToSpeechVoice("nl-BE-ArnaudNeural", "Arnaud (Male) - Dutch"),

            // Dutch (Netherlands)
            new TextToSpeechVoice("nl-NL-FennaNeural", "Fenna (Female) - Dutch"),
            new TextToSpeechVoice("nl-NL-MaartenNeural", "Maarten (Male) - Dutch"),
            new TextToSpeechVoice("nl-NL-ColetteNeural", "Colette (Female) - Dutch"),

            // Polish (Poland)
            new TextToSpeechVoice("pl-PL-MarekNeural", "Marek (Male) - Polish"),
            new TextToSpeechVoice("pl-PL-ZofiaNeural", "Zofia (Female) - Polish"),

            // Portuguese (Brazil)
            new TextToSpeechVoice("pt-BR-FranciscaNeural", "Francisca (Female) - Portuguese"),
            new TextToSpeechVoice("pt-BR-AntonioNeural", "Antonio (Male) - Portuguese"),
            new TextToSpeechVoice("pt-BR-ThalitaNeural", "Thalita (Female) - Portuguese"),

            // Portuguese (Portugal)
            new TextToSpeechVoice("pt-PT-RaquelNeural", "Raquel (Female) - Portuguese"),
            new TextToSpeechVoice("pt-PT-DuarteNeural", "Duarte (Male) - Portuguese"),

            // Romanian (Romania)
            new TextToSpeechVoice("ro-RO-AlinaNeural", "Alina (Female) - Romanian"),
            new TextToSpeechVoice("ro-RO-EmilNeural", "Emil (Male) - Romanian"),

            // Russian (Russia)
            new TextToSpeechVoice("ru-RU-SvetlanaNeural", "Svetlana (Female) - Russian"),
            new TextToSpeechVoice("ru-RU-DmitryNeural", "Dmitry (Male) - Russian"),

            // Slovak (Slovakia)
            new TextToSpeechVoice("sk-SK-ViktoriaNeural", "Viktoria (Female) - Slovak"),
            new TextToSpeechVoice("sk-SK-LukasNeural", "Lukas (Male) - Slovak"),

            // Slovenian (Slovenia)
            new TextToSpeechVoice("sl-SI-PetraNeural", "Petra (Female) - Slovenian"),
            new TextToSpeechVoice("sl-SI-RokNeural", "Rok (Male) - Slovenian"),

            // Swedish (Sweden)
            new TextToSpeechVoice("sv-SE-SofieNeural", "Sofie (Female) - Swedish"),
            new TextToSpeechVoice("sv-SE-MattiasNeural", "Mattias (Male) - Swedish"),

            // Kiswahili (Tanzania)
            new TextToSpeechVoice("sw-TZ-RehemaNeural", "Rehema (Female) - Kiswahili"),
            new TextToSpeechVoice("sw-TZ-DaudiNeural", "Daudi (Male) - Kiswahili"),

            // Tamil (India)
            new TextToSpeechVoice("ta-IN-PallaviNeural", "Pallavi (Female) - Tamil"),
            new TextToSpeechVoice("ta-IN-ValluvarNeural", "Valluvar (Male) - Tamil"),

            // Telugu (India)
            new TextToSpeechVoice("te-IN-ShrutiNeural", "Shruti (Female) - Telugu"),
            new TextToSpeechVoice("te-IN-MohanNeural", "Mohan (Male) - Telugu"),

            // Thai (Thailand)
            new TextToSpeechVoice("th-TH-PremwadeeNeural", "Premwadee (Female) - Thai"),
            new TextToSpeechVoice("th-TH-NiwatNeural", "Niwat (Male) - Thai"),
            new TextToSpeechVoice("th-TH-AcharaNeural", "Achara (Female) - Thai"),

            // Turkish (Türkiye)
            new TextToSpeechVoice("tr-TR-EmelNeural", "Emel (Female) - Turkish"),
            new TextToSpeechVoice("tr-TR-AhmetNeural", "Ahmet (Male) - Turkish"),

            // Ukrainian (Ukraine)
            new TextToSpeechVoice("uk-UA-PolinaNeural", "Polina (Female) - Ukrainian"),

            // Urdu (Pakistan)
            new TextToSpeechVoice("ur-PK-UzmaNeural", "Uzma (Female) - Urdu"),
            new TextToSpeechVoice("ur-PK-AsadNeural", "Asad (Male) - Urdu"),

            // Vietnamese (Vietnam)
            new TextToSpeechVoice("vi-VN-HoaiMyNeural", "HoaiMy (Female) - Vietnamese"),
            new TextToSpeechVoice("vi-VN-NamMinhNeural", "NamMinh (Male) - Vietnamese"),

            // Chinese (Mandarin, Simplified)
            new TextToSpeechVoice("zh-CN-XiaoxiaoNeural", "Xiaoxiao (Female) - Chinese"),
            new TextToSpeechVoice("zh-CN-YunxiNeural", "Yunxi (Male) - Chinese"),
            new TextToSpeechVoice("zh-CN-YunjianNeural", "Yunjian (Male) - Chinese"),
            new TextToSpeechVoice("zh-CN-XiaoyiNeural", "Xiaoyi (Female) - Chinese"),
            new TextToSpeechVoice("zh-CN-YunxiaNeural", "Yunxia (Male) - Chinese"),

            // Chinese (Cantonese, Traditional)
            new TextToSpeechVoice("zh-HK-HiuMaanNeural", "HiuMaan (Female) - Chinese"),
            new TextToSpeechVoice("zh-HK-WanLungNeural", "WanLung (Male) - Chinese"),
            new TextToSpeechVoice("zh-HK-HiuGaaiNeural", "HiuGaai (Female) - Chinese"),

            // Chinese (Taiwanese Mandarin, Traditional)
            new TextToSpeechVoice("zh-TW-HsiaoChenNeural", "HsiaoChen (Female) - Chinese"),
            new TextToSpeechVoice("zh-TW-YunJheNeural", "YunJhe (Male) - Chinese"),
            new TextToSpeechVoice("zh-TW-HsiaoYuNeural", "HsiaoYu (Female) - Chinese")
        };

        public TextToSpeechProviderType ProviderType { get { return TextToSpeechProviderType.EdgeTTS; } }

        public int VolumeMinimum { get { return 0; } }

        public int VolumeMaximum { get { return 100; } }

        public int VolumeDefault { get { return 100; } }

        public int PitchMinimum { get { return -50; } }

        public int PitchMaximum { get { return 50; } }

        public int PitchDefault { get { return 0; } }

        public int RateMinimum { get { return -50; } }

        public int RateMaximum { get { return 50; } }

        public int RateDefault { get { return 0; } }

        public IEnumerable<TextToSpeechVoice> GetVoices() { return EdgeTTSService.AvailableVoices; }

        public async Task Speak(string outputDevice, Guid overlayEndpointID, string text, string voice, int volume, int pitch, int rate, bool ssml, bool waitForFinish)
        {
            using (AdvancedHttpClient client = new AdvancedHttpClient())
            {
                client.Timeout = new TimeSpan(0, 0, 10);
                client.DefaultRequestHeaders.Add("User-Agent", $"MixItUp/{Assembly.GetEntryAssembly().GetName().Version.ToString()} (Web call from Mix It Up; https://mixitupapp.com; support@mixitupapp.com)");
                client.DefaultRequestHeaders.Add("Client-Key", UtilServiceHelper.GenerateClientKey());

                JObject body = new JObject();
                body["text"] = text;
                body["voice"] = voice;

                // Convert pitch and rate to percentage format (e.g., +10%, -10%)
                if (pitch != 0)
                {
                    body["pitch"] = pitch > 0 ? $"+{pitch}%" : $"{pitch}%";
                }
                if (rate != 0)
                {
                    body["rate"] = rate > 0 ? $"+{rate}%" : $"{rate}%";
                }

                // Extract language from voice ID (e.g., "en-US-AvaNeural" -> "en-US")
                string[] voiceParts = voice.Split('-');
                if (voiceParts.Length >= 2)
                {
                    body["lang"] = $"{voiceParts[0]}-{voiceParts[1]}";
                }

                HttpResponseMessage response = await client.PostAsync("https://util.mixitupapp.com/api/services/external/edgetts/tts/generate", AdvancedHttpClient.CreateContentFromObject(body));
                if (response.IsSuccessStatusCode)
                {
                    MemoryStream stream = new MemoryStream();
                    using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        responseStream.CopyTo(stream);
                        stream.Position = 0;
                    }

                    await ServiceManager.Get<IAudioService>().PlayMP3Stream(stream, volume, outputDevice, waitForFinish: waitForFinish);
                }
                else
                {
                    string content = await response.Content.ReadAsStringAsync();
                    Logger.Log(LogLevel.Error, "Edge TTS Error: " + content);
                    await ServiceManager.Get<ChatService>().SendMessage("Edge TTS Error: " + content, StreamingPlatformTypeEnum.All);
                }
            }
        }
    }
}