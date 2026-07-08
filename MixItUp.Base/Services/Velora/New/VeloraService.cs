using MixItUp.Base.Model;
using MixItUp.Base.Model.Velora.ChannelPoints;
using MixItUp.Base.Model.Velora.Chat;
using MixItUp.Base.Model.Velora.Emotes;
using MixItUp.Base.Model.Velora.Streams;
using MixItUp.Base.Model.Velora.Subscriptions;
using MixItUp.Base.Model.Velora.Users;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Velora.New
{
    public class VeloraService : StreamingPlatformServiceBaseNew
    {
        private const string OAuthBaseAddress = "https://velora.tv/oauth/authorize";
        private const string OAuthTokenAddress = "https://api.velora.tv/api/developer/oauth/token";

        // Velora can not register http://localhost:8919/ as a valid redirect URI, so the Mix It Up
        // Desktop API catches the OAuth redirect and relays it down to the local OAuth server.
        public const string OAuthRedirectAddress = "https://desktop.api.mixitup.bot/api/v2/user/velora/callback";

        private const string BaseAddressFormat = "https://api.velora.tv/api/";

        public override string Name { get { return "Velora"; } }

        public override string ClientID { get { return "velora_03e45173125ad314"; } }
        public override string ClientSecret { get { return ServiceManager.Get<SecretsService>().GetSecret("VeloraSecret"); } }

        public override StreamingPlatformTypeEnum Platform { get { return StreamingPlatformTypeEnum.Velora; } }

        public override bool IsConnected { get; protected set; }

        private Dictionary<string, string> stateToCodeVerifier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public VeloraService(IEnumerable<string> scopes, bool isBotService = false)
            : base(BaseAddressFormat, scopes, isBotService) { }

        public async Task<UserModel> GetCurrentUser()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<UserModel>("users/me");
            });
        }

        public async Task<UserModel> GetUserByUsername(string username)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<UserModel>("users/" + AdvancedHttpClient.URLEncodeString(username));
            });
        }

        public async Task<StreamInfoModel> GetStreamInfo()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<StreamInfoModel>("integrations/oauth/stream/info");
            });
        }

        public async Task<Result> UpdateStreamInfo(string title = null, string categorySlug = null, IEnumerable<string> tags = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(title)) { jobj["title"] = title; }
                if (!string.IsNullOrEmpty(categorySlug)) { jobj["categorySlug"] = categorySlug; }
                if (tags != null) { jobj["tags"] = JArray.FromObject(tags.ToArray()); }
                if (jobj.Count > 0)
                {
                    HttpResponseMessage response = await this.HttpClient.PutAsync("integrations/oauth/stream/info", AdvancedHttpClient.CreateContentFromObject(jobj));
                    if (!response.IsSuccessStatusCode)
                    {
                        return new Result(await response.Content.ReadAsStringAsync());
                    }
                }

                return new Result();
            });
        }

        public async Task<IEnumerable<CategoryModel>> GetStreamCategories()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                CategoriesResponseModel response = await this.HttpClient.GetAsync<CategoriesResponseModel>("integrations/oauth/stream/categories");
                return response?.Categories ?? new List<CategoryModel>();
            });
        }

        public async Task<SendChatMessageResponseModel> SendChatMessage(string channelID, string message, string replyToMessageID = null, string replyToUsername = null, string replyToSnippet = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["message"] = message;

                if (!string.IsNullOrEmpty(replyToMessageID))
                {
                    JObject replyTo = new JObject();
                    replyTo["messageId"] = replyToMessageID;
                    if (!string.IsNullOrEmpty(replyToUsername)) { replyTo["username"] = replyToUsername; }
                    if (!string.IsNullOrEmpty(replyToSnippet)) { replyTo["snippet"] = replyToSnippet; }
                    jobj["replyTo"] = replyTo;
                }

                return await this.HttpClient.PostAsync<SendChatMessageResponseModel>($"integrations/oauth/chat/channels/{AdvancedHttpClient.URLEncodeString(channelID)}/messages", AdvancedHttpClient.CreateContentFromObject(jobj));
            });
        }

        public async Task<Result> ModerateUser(string channelID, string action, string userID = null, string username = null, int? durationSeconds = null, string reason = null, string messageID = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["action"] = action;
                if (!string.IsNullOrEmpty(userID)) { jobj["userId"] = userID; }
                if (!string.IsNullOrEmpty(username)) { jobj["username"] = username; }
                if (durationSeconds.HasValue) { jobj["durationSeconds"] = durationSeconds.Value; }
                if (!string.IsNullOrEmpty(reason)) { jobj["reason"] = reason; }
                if (!string.IsNullOrEmpty(messageID)) { jobj["messageId"] = messageID; }

                HttpResponseMessage response = await this.HttpClient.PostAsync($"integrations/oauth/chat/channels/{AdvancedHttpClient.URLEncodeString(channelID)}/moderate", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        public async Task<IEnumerable<ChannelPointRewardModel>> GetChannelPointRewards(string channelID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                ChannelPointRewardsResponseModel response = await this.HttpClient.GetAsync<ChannelPointRewardsResponseModel>($"channel-points/{AdvancedHttpClient.URLEncodeString(channelID)}/items/with-built-in");
                return response?.AllRewards ?? new List<ChannelPointRewardModel>();
            });
        }

        public async Task<IEnumerable<EmoteModel>> GetEmotes(string channelUsername = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                string requestUri = "emotes";
                if (!string.IsNullOrEmpty(channelUsername))
                {
                    requestUri += "?channel=" + AdvancedHttpClient.URLEncodeString(channelUsername);
                }

                JToken response = await this.HttpClient.GetAsync<JToken>(requestUri);
                return EmoteModel.ParseEmotes(response);
            });
        }

        public async Task<int?> GetSubscriberCount()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("developer/subscriptions/count");
                if (response == null)
                {
                    return (int?)null;
                }
                return VeloraSubscriberCount.Parse(response);
            });
        }

        public async Task<UserStreamModel> GetUserStream(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("streams/user/" + AdvancedHttpClient.URLEncodeString(username));
                return UserStreamModel.Parse(response);
            });
        }

        public async Task<IEnumerable<ChannelSubscriptionBadgeModel>> GetChannelSubscriptionBadges(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new List<ChannelSubscriptionBadgeModel>();
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("badges/channel/" + AdvancedHttpClient.URLEncodeString(username));
                return ChannelSubscriptionBadgeModel.ParseList(response);
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
                { "redirect_uri", OAuthRedirectAddress },
                { "response_type", "code" },
                { "scope", string.Join(" ", scopes) },
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
                Logger.Log(LogLevel.Error, "Velora OAuth PKCE verifier missing for token exchange");
                return null;
            }

            JObject body = new JObject();
            body["grant_type"] = "authorization_code";
            body["client_id"] = this.ClientID;
            body["client_secret"] = this.ClientSecret;
            body["code"] = authorizationCode;
            body["redirect_uri"] = OAuthRedirectAddress;
            body["code_verifier"] = codeVerifier;

            OAuthTokenModel token = await this.RequestJSONOAuthToken(body);
            if (token != null)
            {
                token.clientID = this.ClientID;
                token.ScopeList = OAuthTokenModel.GenerateScopeList(scopes);
            }
            return token;
        }

        protected override async Task RefreshOAuthToken()
        {
            JObject body = new JObject();
            body["grant_type"] = "refresh_token";
            body["client_id"] = this.ClientID;
            body["client_secret"] = this.ClientSecret;
            body["refresh_token"] = this.OAuthToken.refreshToken;

            OAuthTokenModel newToken = await this.RequestJSONOAuthToken(body);
            if (newToken != null)
            {
                newToken.clientID = this.OAuthToken.clientID;
                newToken.ScopeList = this.OAuthToken.ScopeList;
                this.OAuthToken = newToken;
            }
        }

        private async Task<OAuthTokenModel> RequestJSONOAuthToken(JObject body)
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    return await client.PostAsync<OAuthTokenModel>(OAuthTokenAddress, AdvancedHttpClient.CreateContentFromObject(body));
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
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
