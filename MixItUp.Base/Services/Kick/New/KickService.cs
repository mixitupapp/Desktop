using MixItUp.Base.Model;
using MixItUp.Base.Model.Kick.Categories;
using MixItUp.Base.Model.Kick.ChannelRewards;
using MixItUp.Base.Model.Kick.Chat;
using MixItUp.Base.Model.Kick.Channels;
using MixItUp.Base.Model.Kick.Common;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Kick.Livestreams;
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

        public async Task<UserModel> GetCurrentUser()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                IEnumerable<UserModel> users = await this.GetDataResultAsync<UserModel>("users");
                return users?.FirstOrDefault();
            });
        }

        public async Task<UserModel> GetUserByID(string userID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                IEnumerable<UserModel> users = await this.GetDataResultAsync<UserModel>("users?id=" + userID);
                return users?.FirstOrDefault();
            });
        }

        public async Task<IEnumerable<UserModel>> GetUsersByIDs(IEnumerable<string> userIDs)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.GetDataResultAsync<UserModel>("users?id=" + string.Join("&id=", userIDs));
            });
        }

        public async Task<TokenIntrospectionModel> GetTokenIntrospection()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                ResponseModel<TokenIntrospectionModel> result = await this.HttpClient.PostAsync<ResponseModel<TokenIntrospectionModel>>("https://id.kick.com/oauth/token/introspect");
                return result?.Data;
            });
        }

        public async Task<ChannelModel> GetCurrentChannel()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                IEnumerable<ChannelModel> channels = await this.GetDataResultAsync<ChannelModel>("channels");
                return channels?.FirstOrDefault();
            });
        }

        public async Task<ChannelModel> GetChannelByUserID(string broadcasterUserID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                IEnumerable<ChannelModel> channels = await this.GetDataResultAsync<ChannelModel>("channels?broadcaster_user_id=" + broadcasterUserID);
                return channels?.FirstOrDefault();
            });
        }

        public async Task<IEnumerable<ChannelModel>> GetChannelsByUserIDs(IEnumerable<string> broadcasterUserIDs)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.GetDataResultAsync<ChannelModel>("channels?broadcaster_user_id=" + string.Join("&broadcaster_user_id=", broadcasterUserIDs.Take(50)));
            });
        }

        public async Task<ChannelModel> GetChannelBySlug(string slug)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                IEnumerable<ChannelModel> channels = await this.GetDataResultAsync<ChannelModel>("channels?slug=" + AdvancedHttpClient.URLEncodeString(slug));
                return channels?.FirstOrDefault();
            });
        }

        public async Task<IEnumerable<ChannelModel>> GetChannelsBySlugs(IEnumerable<string> slugs)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.GetDataResultAsync<ChannelModel>("channels?slug=" + string.Join("&slug=", slugs.Take(50).Select(s => AdvancedHttpClient.URLEncodeString(s))));
            });
        }

        public async Task<UserModel> GetUserByChannelSlug(string slug)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                ChannelModel channel = await this.GetChannelBySlug(slug);
                if (channel != null)
                {
                    UserModel user = await this.GetUserByID(channel.BroadcasterUserID.ToString());
                    if (user != null)
                    {
                        user.ChannelSlug = channel.Slug;
                        return user;
                    }
                }
                return null;
            });
        }

        public async Task<Result> UpdateChannel(string title = null, long? categoryID = null, IEnumerable<string> customTags = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(title)) { jobj["stream_title"] = title; }
                if (categoryID.HasValue) { jobj["category_id"] = categoryID.Value; }
                if (customTags != null) { jobj["custom_tags"] = JArray.FromObject(customTags.Take(10).ToArray()); }
                if (jobj.Count > 0)
                {
                    HttpResponseMessage response = await this.HttpClient.PatchAsync("channels", AdvancedHttpClient.CreateContentFromObject(jobj));
                    if (!response.IsSuccessStatusCode)
                    {
                        return new Result(await response.Content.ReadAsStringAsync());
                    }
                }

                return new Result();
            });
        }

        public async Task<ChatMessageResultModel> SendChatMessage(string content, bool isBot, long broadcasterUserID, string replyMessageID = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["content"] = content;
                jobj["type"] = isBot ? "bot" : "user";

                if (!isBot)
                {
                    jobj["broadcaster_user_id"] = broadcasterUserID;
                }

                if (!string.IsNullOrEmpty(replyMessageID))
                {
                    jobj["reply_to_message_id"] = replyMessageID;
                }

                ResponseModel<ChatMessageResultModel> result = await this.HttpClient.PostAsync<ResponseModel<ChatMessageResultModel>>("chat", AdvancedHttpClient.CreateContentFromObject(jobj));
                return result?.Data;
            });
        }

        public async Task DeleteChatMessage(string messageID)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                await this.HttpClient.DeleteAsync("chat/" + messageID);
            });
        }

        public async Task TimeoutUser(long broadcasterUserID, long userID, int durationInMinutes, string reason = null)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["broadcaster_user_id"] = broadcasterUserID;
                jobj["user_id"] = userID;
                jobj["duration"] = durationInMinutes;
                if (!string.IsNullOrEmpty(reason)) { jobj["reason"] = reason; }

                await this.HttpClient.PostAsync("moderation/bans", AdvancedHttpClient.CreateContentFromObject(jobj));
            });
        }

        public async Task BanUser(long broadcasterUserID, long userID, string reason = null)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["broadcaster_user_id"] = broadcasterUserID;
                jobj["user_id"] = userID;
                if (!string.IsNullOrEmpty(reason)) { jobj["reason"] = reason; }

                await this.HttpClient.PostAsync("moderation/bans", AdvancedHttpClient.CreateContentFromObject(jobj));
            });
        }

        public async Task UnbanUser(long broadcasterUserID, long userID)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["broadcaster_user_id"] = broadcasterUserID;
                jobj["user_id"] = userID;

                await this.HttpClient.DeleteAsync("moderation/bans", AdvancedHttpClient.CreateContentFromObject(jobj));
            });
        }

        public async Task<IEnumerable<ChannelRewardModel>> GetChannelRewards()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.GetDataResultAsync<ChannelRewardModel>("channels/rewards");
            });
        }

        public async Task<ChannelRewardModel> CreateChannelReward(string title, int cost, string description = null, string backgroundColor = null, bool isEnabled = true, bool isUserInputRequired = false, bool shouldRedemptionsSkipRequestQueue = false)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["title"] = title;
                jobj["cost"] = cost;
                if (!string.IsNullOrEmpty(description)) { jobj["description"] = description; }
                if (!string.IsNullOrEmpty(backgroundColor)) { jobj["background_color"] = backgroundColor; }
                jobj["is_enabled"] = isEnabled;
                jobj["is_user_input_required"] = isUserInputRequired;
                jobj["should_redemptions_skip_request_queue"] = shouldRedemptionsSkipRequestQueue;

                ResponseModel<ChannelRewardModel> result = await this.HttpClient.PostAsync<ResponseModel<ChannelRewardModel>>("channels/rewards", AdvancedHttpClient.CreateContentFromObject(jobj));
                return result?.Data;
            });
        }

        public async Task<ChannelRewardModel> UpdateChannelReward(string rewardID, string title = null, int? cost = null, string description = null, string backgroundColor = null, bool? isEnabled = null, bool? isPaused = null, bool? isUserInputRequired = null, bool? shouldRedemptionsSkipRequestQueue = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(title)) { jobj["title"] = title; }
                if (cost.HasValue) { jobj["cost"] = cost.Value; }
                if (!string.IsNullOrEmpty(description)) { jobj["description"] = description; }
                if (!string.IsNullOrEmpty(backgroundColor)) { jobj["background_color"] = backgroundColor; }
                if (isEnabled.HasValue) { jobj["is_enabled"] = isEnabled.Value; }
                if (isPaused.HasValue) { jobj["is_paused"] = isPaused.Value; }
                if (isUserInputRequired.HasValue) { jobj["is_user_input_required"] = isUserInputRequired.Value; }
                if (shouldRedemptionsSkipRequestQueue.HasValue) { jobj["should_redemptions_skip_request_queue"] = shouldRedemptionsSkipRequestQueue.Value; }

                ResponseModel<ChannelRewardModel> result = await this.HttpClient.PatchAsync<ResponseModel<ChannelRewardModel>>("channels/rewards/" + rewardID, AdvancedHttpClient.CreateContentFromObject(jobj));
                return result?.Data;
            });
        }

        public async Task DeleteChannelReward(string rewardID)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                await this.HttpClient.DeleteAsync("channels/rewards/" + rewardID);
            });
        }

        public async Task<PaginatedResponseModel<ChannelRewardRedemptionsByRewardModel>> GetChannelRewardRedemptions(string rewardID = null, string status = null, IEnumerable<string> redemptionIDs = null, string cursor = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                List<string> parameters = new List<string>();
                if (redemptionIDs != null && redemptionIDs.Count() > 0)
                {
                    parameters.AddRange(redemptionIDs.Select(id => "id=" + id));
                }
                else
                {
                    if (!string.IsNullOrEmpty(rewardID)) { parameters.Add("reward_id=" + rewardID); }
                    if (!string.IsNullOrEmpty(status)) { parameters.Add("status=" + status); }
                    if (!string.IsNullOrEmpty(cursor)) { parameters.Add("cursor=" + AdvancedHttpClient.URLEncodeString(cursor)); }
                }

                return await this.HttpClient.GetAsync<PaginatedResponseModel<ChannelRewardRedemptionsByRewardModel>>("channels/rewards/redemptions" + ((parameters.Count > 0) ? "?" + string.Join("&", parameters) : string.Empty));
            });
        }

        public async Task<IEnumerable<FailedRedemptionModel>> AcceptChannelRewardRedemptions(IEnumerable<string> redemptionIDs)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["ids"] = JArray.FromObject(redemptionIDs.Take(25).ToArray());

                ResponseModel<List<FailedRedemptionModel>> result = await this.HttpClient.PostAsync<ResponseModel<List<FailedRedemptionModel>>>("channels/rewards/redemptions/accept", AdvancedHttpClient.CreateContentFromObject(jobj));
                return result?.Data ?? new List<FailedRedemptionModel>();
            });
        }

        public async Task<IEnumerable<FailedRedemptionModel>> RejectChannelRewardRedemptions(IEnumerable<string> redemptionIDs)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["ids"] = JArray.FromObject(redemptionIDs.Take(25).ToArray());

                ResponseModel<List<FailedRedemptionModel>> result = await this.HttpClient.PostAsync<ResponseModel<List<FailedRedemptionModel>>>("channels/rewards/redemptions/reject", AdvancedHttpClient.CreateContentFromObject(jobj));
                return result?.Data ?? new List<FailedRedemptionModel>();
            });
        }

        public async Task<IEnumerable<LivestreamModel>> GetLivestreams(IEnumerable<string> broadcasterUserIDs = null, string categoryID = null, string language = null, int limit = 25, string sort = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                List<string> parameters = new List<string>();
                if (broadcasterUserIDs != null && broadcasterUserIDs.Count() > 0) { parameters.AddRange(broadcasterUserIDs.Take(50).Select(id => "broadcaster_user_id=" + id)); }
                if (!string.IsNullOrEmpty(categoryID)) { parameters.Add("category_id=" + categoryID); }
                if (!string.IsNullOrEmpty(language)) { parameters.Add("language=" + AdvancedHttpClient.URLEncodeString(language)); }
                if (limit > 0) { parameters.Add("limit=" + Math.Min(limit, 100)); }
                if (!string.IsNullOrEmpty(sort)) { parameters.Add("sort=" + sort); }

                return await this.GetDataResultAsync<LivestreamModel>("livestreams" + ((parameters.Count > 0) ? "?" + string.Join("&", parameters) : string.Empty));
            });
        }

        public async Task<LivestreamStatsModel> GetLivestreamsStats()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                ResponseModel<LivestreamStatsModel> result = await this.HttpClient.GetAsync<ResponseModel<LivestreamStatsModel>>("livestreams/stats");
                return result?.Data;
            });
        }

        public async Task<PaginatedResponseModel<CategoryWithTagsModel>> GetCategories(string cursor = null, int limit = 25, IEnumerable<string> names = null, IEnumerable<string> tags = null, IEnumerable<string> ids = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                List<string> parameters = new List<string>();
                if (!string.IsNullOrEmpty(cursor)) { parameters.Add("cursor=" + AdvancedHttpClient.URLEncodeString(cursor)); }
                if (limit > 0) { parameters.Add("limit=" + limit); }
                if (names != null && names.Count() > 0) { parameters.Add("name=" + string.Join(",", names.Select(n => AdvancedHttpClient.URLEncodeString(n)))); }
                if (tags != null && tags.Count() > 0) { parameters.Add("tag=" + string.Join(",", tags.Select(t => AdvancedHttpClient.URLEncodeString(t)))); }
                if (ids != null && ids.Count() > 0) { parameters.Add("id=" + string.Join(",", ids)); }

                return await this.HttpClient.GetAsync<PaginatedResponseModel<CategoryWithTagsModel>>("https://api.kick.com/public/v2/categories" + ((parameters.Count > 0) ? "?" + string.Join("&", parameters) : string.Empty));
            });
        }

        public async Task<LeaderboardModel> GetKicksLeaderboard(int top = 10)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                ResponseModel<LeaderboardModel> result = await this.HttpClient.GetAsync<ResponseModel<LeaderboardModel>>("kicks/leaderboard?top=" + Math.Min(top, 100));
                return result?.Data;
            });
        }

        public async Task<IEnumerable<T>> GetDataResultAsync<T>(string requestUri)
        {
            ResponseModel<List<T>> result = await this.HttpClient.GetAsync<ResponseModel<List<T>>>(requestUri);
            if (result != null && result.Data != null && result.Data.Count > 0)
            {
                return result.Data;
            }
            return new List<T>();
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

                await this.ValidateTokenScopes(token, scopes);
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

        private async Task ValidateTokenScopes(OAuthTokenModel token, IEnumerable<string> scopes)
        {
            TokenIntrospectionModel introspection = null;
            using (AdvancedHttpClient client = new AdvancedHttpClient())
            {
                client.SetBearerAuthorization(token);
                ResponseModel<TokenIntrospectionModel> response = await client.PostAsync<ResponseModel<TokenIntrospectionModel>>("https://id.kick.com/oauth/token/introspect");
                introspection = response?.Data;
            }

            HashSet<string> expectedScopes = new HashSet<string>(scopes ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> grantedScopes = new HashSet<string>((introspection.Scope ?? string.Empty)
                .Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> missingScopes = expectedScopes.Except(grantedScopes, StringComparer.OrdinalIgnoreCase).OrderBy(s => s);

            if (missingScopes.Any())
            {
                throw new InvalidOperationException(
                    "Missing Kick scopes: " + string.Join(", ", missingScopes) + Environment.NewLine +
                    "Please log in again and enable all requested scopes.");
            }
        }
    }
}


