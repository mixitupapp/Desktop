using MixItUp.Base.Model;
using MixItUp.Base.Model.Kick.Chat;
using MixItUp.Base.Model.Kick.Channels;
using MixItUp.Base.Model.Kick.Common;
using MixItUp.Base.Model.Kick.Users;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Kick.New
{
    public class KickService : StreamingPlatformServiceBaseNew
    {
        private const string OAuthBaseAddress = "https://id.kick.com/oauth/authorize";
        private const string OAuthTokenAddress = "https://id.kick.com/oauth/token";

        private const string BaseAddressFormat = "https://api.kick.com/public/v1/";

        public override string Name { get { return "Kick"; } }

        public override string ClientID { get { return "01KQGG89AA8B3ZB39D4NRWW7PD"; } }
        public override string ClientSecret { get { return ServiceManager.Get<SecretsService>().GetSecret("KickSecret"); } }

        public override StreamingPlatformTypeEnum Platform { get { return StreamingPlatformTypeEnum.Kick; } }

        public override bool IsConnected { get; protected set; }

        private Dictionary<string, string> stateToCodeVerifier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public KickService(IEnumerable<string> scopes, bool isBotService = false)
            : base(BaseAddressFormat, scopes, isBotService) { }

        public async Task<KickUserModel> GetCurrentUser()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                KickResponseModel<List<KickUserModel>> result = await this.HttpClient.GetAsync<KickResponseModel<List<KickUserModel>>>("users");
                return result?.Data?.FirstOrDefault();
            });
        }

        public async Task<KickChannelModel> GetCurrentChannel()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                KickResponseModel<List<KickChannelModel>> result = await this.HttpClient.GetAsync<KickResponseModel<List<KickChannelModel>>>("channels");
                return result?.Data?.FirstOrDefault();
            });
        }

        public async Task<KickChatMessageResultModel> SendChatMessage(string content, bool isBot, long broadcasterUserID, string replyMessageID = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject payload = new JObject
                {
                    ["content"] = content,
                    ["type"] = isBot ? "bot" : "user",
                };

                if (!isBot)
                {
                    payload["broadcaster_user_id"] = broadcasterUserID;
                }

                if (!string.IsNullOrWhiteSpace(replyMessageID))
                {
                    payload["reply_to_message_id"] = replyMessageID;
                }

                KickResponseModel<KickChatMessageResultModel> result = await this.HttpClient.PostAsync<KickResponseModel<KickChatMessageResultModel>>("chat", AdvancedHttpClient.CreateContentFromObject(payload));
                return result?.Data;
            });
        }

        public async Task UpdateChannel(string title = null, long? categoryID = null)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                JObject payload = new JObject();
                if (!string.IsNullOrWhiteSpace(title)) { payload["stream_title"] = title; }
                if (categoryID.HasValue) { payload["category_id"] = categoryID.Value; }
                if (payload.Count > 0)
                {
                    await this.HttpClient.PatchAsync("channels", AdvancedHttpClient.CreateContentFromObject(payload));
                }
            });
        }

        public async Task TimeoutUser(long broadcasterUserID, long userID, int durationInMinutes, string reason = null)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                JObject payload = new JObject
                {
                    ["broadcaster_user_id"] = broadcasterUserID,
                    ["user_id"] = userID,
                    ["duration"] = durationInMinutes,
                };
                if (!string.IsNullOrWhiteSpace(reason)) { payload["reason"] = reason; }
                await this.HttpClient.PostAsync("moderation/bans", AdvancedHttpClient.CreateContentFromObject(payload));
            });
        }

        public async Task BanUser(long broadcasterUserID, long userID, string reason = null)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                JObject payload = new JObject
                {
                    ["broadcaster_user_id"] = broadcasterUserID,
                    ["user_id"] = userID,
                };
                if (!string.IsNullOrWhiteSpace(reason)) { payload["reason"] = reason; }
                await this.HttpClient.PostAsync("moderation/bans", AdvancedHttpClient.CreateContentFromObject(payload));
            });
        }

        public async Task UnbanUser(long broadcasterUserID, long userID)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                JObject payload = new JObject
                {
                    ["broadcaster_user_id"] = broadcasterUserID,
                    ["user_id"] = userID,
                };
                await this.HttpClient.DeleteAsync("moderation/bans", AdvancedHttpClient.CreateContentFromObject(payload));
            });
        }

        protected override async Task<string> GetAuthorizationCodeURL(IEnumerable<string> scopes, string state, bool forceApprovalPrompt = false)
        {
            string codeVerifier = CreateCodeVerifier();
            string codeChallenge = CreateCodeChallenge(codeVerifier);
            this.stateToCodeVerifier[state] = codeVerifier;

            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                { "client_id", this.ClientID },
                { "scope", string.Join(" ", scopes) },
                { "response_type", "code" },
                { "redirect_uri", LocalOAuthKestrelServer.REDIRECT_URL },
                { "state", state },
                { "code_challenge", codeChallenge },
                { "code_challenge_method", "S256" },
            };

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters.AsEnumerable());
            return OAuthBaseAddress + "?" + await content.ReadAsStringAsync();
        }

        protected override async Task<OAuthTokenModel> RequestOAuthToken(string authorizationCode, IEnumerable<string> scopes, string state)
        {
            this.stateToCodeVerifier.TryGetValue(state, out string codeVerifier);
            this.stateToCodeVerifier.Remove(state);

            if (string.IsNullOrWhiteSpace(codeVerifier))
            {
                Logger.Log(LogLevel.Error, "Kick OAuth PKCE verifier missing for token exchange");
                return null;
            }

            OAuthTokenModel token = await this.RequestWWWFormUrlEncodedOAuthToken(OAuthTokenAddress,
                new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", this.ClientID),
                    new KeyValuePair<string, string>("client_secret", this.ClientSecret),
                    new KeyValuePair<string, string>("redirect_uri", LocalOAuthKestrelServer.REDIRECT_URL),
                    new KeyValuePair<string, string>("code", authorizationCode),
                    new KeyValuePair<string, string>("code_verifier", codeVerifier),
                });

            if (token != null)
            {
                token.clientID = this.ClientID;
                token.ScopeList = OAuthTokenModel.GenerateScopeList(scopes);
            }
            return token;
        }

        protected override async Task RefreshOAuthToken()
        {
            OAuthTokenModel newToken = await this.RequestWWWFormUrlEncodedOAuthToken(OAuthTokenAddress,
                new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("client_id", this.ClientID),
                    new KeyValuePair<string, string>("client_secret", this.ClientSecret),
                    new KeyValuePair<string, string>("refresh_token", this.OAuthToken.refreshToken),
                });

            if (newToken != null)
            {
                newToken.clientID = this.OAuthToken.clientID;
                newToken.ScopeList = this.OAuthToken.ScopeList;
                this.OAuthToken = newToken;
            }
        }

        private static string CreateCodeVerifier()
        {
            byte[] randomBytes = new byte[64];
            RandomNumberGenerator.Fill(randomBytes);
            return Base64UrlEncode(randomBytes);
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.ASCII.GetBytes(codeVerifier);
                byte[] hash = sha256.ComputeHash(bytes);
                return Base64UrlEncode(hash);
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}


