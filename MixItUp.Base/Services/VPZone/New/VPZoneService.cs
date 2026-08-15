using MixItUp.Base.Model;
using MixItUp.Base.Model.VPZone.ChannelPoints;
using MixItUp.Base.Model.VPZone.Chat;
using MixItUp.Base.Model.VPZone.Streams;
using MixItUp.Base.Model.VPZone.Subscriptions;
using MixItUp.Base.Model.VPZone.Users;
using MixItUp.Base.Model.VPZone.Webhooks;
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

namespace MixItUp.Base.Services.VPZone.New
{
    /// <summary>
    /// Result of a chat moderation call, carrying the HTTP status and VPZone's machine-readable error
    /// code so callers can tell a rejected request apart from a transient failure.
    /// </summary>
    public class VPZoneModerationResult : Result
    {
        public VPZoneModerationResult() : base() { }

        public VPZoneModerationResult(string message) : base(message) { }

        public int? StatusCode { get; set; }

        public string ErrorCode { get; set; }

        public bool IsRequestRejected { get { return this.StatusCode.HasValue && this.StatusCode.Value >= 400 && this.StatusCode.Value < 500; } }
    }

    public class VPZoneService : StreamingPlatformServiceBaseNew
    {
        private const string OAuthBaseAddress = "https://vpzone.tv/oauth/authorize";
        private const string OAuthTokenAddress = "https://vpzone.tv/api/oauth/token";
        private const string OAuthRevokeAddress = "https://vpzone.tv/api/oauth/revoke";

        // VPZone accepts http://localhost:8919/ as a registered redirect URI, so the desktop client
        // catches the OAuth redirect on its own local listener (LocalOAuthKestrelServer) exactly like
        // the other streaming platforms, with no Desktop API relay in the path.
        public const string OAuthRedirectAddress = "http://localhost:8919/";

        // Also registered, as the fallback for a machine where the local listener cannot bind: the
        // Mix It Up Desktop API catches the redirect and relays it down to the local OAuth server.
        public const string OAuthRedirectFallbackAddress = "https://desktop.api.mixitup.bot/api/v2/user/vpzone/callback";

        private const string BaseAddressFormat = "https://vpzone.tv/api/v1/";

        // The chat service hangs a couple of read-only capabilities off its own path rather than the
        // v1 API. Its moderation routes live here too, but they authenticate off the website's own
        // session cookie and ignore the Authorization header, so only the read-only ones are used
        // below. Everything moderation-shaped goes through the v1 equivalents.
        private const string ChatServiceBaseAddress = "https://vpzone.tv/api/chat/";

        public const string ChannelLinkFormat = "https://vpzone.tv/{0}";

        public override string Name { get { return "VPZone"; } }

        public override string ClientID { get { return "a89fb6bd-dde5-4b57-ba24-93b30a0f390c"; } }
        public override string ClientSecret { get { return ServiceManager.Get<SecretsService>().GetSecret("VPZoneSecret"); } }

        public override StreamingPlatformTypeEnum Platform { get { return StreamingPlatformTypeEnum.VPZone; } }

        public override bool IsConnected { get; protected set; }

        private readonly Dictionary<string, string> stateToCodeVerifier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public VPZoneService(IEnumerable<string> scopes, bool isBotService = false)
            : base(BaseAddressFormat, scopes, isBotService) { }

        // ===== Identity =====

        /// <summary>GET /me - the token's own metadata plus the authenticated member's profile.</summary>
        public async Task<VPZoneTokenInfoModel> GetTokenInfo()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("me");
                return VPZoneResponseEnvelope.Unwrap<VPZoneTokenInfoModel>(response);
            });
        }

        public async Task<VPZoneUserModel> GetCurrentUser()
        {
            VPZoneTokenInfoModel tokenInfo = await this.GetTokenInfo();
            return tokenInfo?.Profile;
        }

        /// <summary>
        /// GET /users/{username} - a member profile extended with their VPZ+ standing
        /// (vpz_plus_active / vpz_plus_since) and their own channel summary.
        /// </summary>
        public async Task<VPZoneUserModel> GetUserByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("users/" + AdvancedHttpClient.URLEncodeString(username));
                return VPZoneResponseEnvelope.Unwrap<VPZoneUserModel>(response);
            });
        }

        /// <summary>
        /// GET /me/vpz-plus - the authenticated member's VPZ+ membership. "since" is the first-ever
        /// activation and never resets on renewal, so it is the membership anniversary.
        /// </summary>
        public async Task<VPZPlusMembershipModel> GetVPZPlusMembership()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("me/vpz-plus");
                return VPZoneResponseEnvelope.Unwrap<VPZPlusMembershipModel>(response);
            });
        }

        // ===== Channel =====

        public async Task<VPZoneChannelModel> GetChannel(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("channels/" + AdvancedHttpClient.URLEncodeString(slug));
                return VPZoneResponseEnvelope.Unwrap<VPZoneChannelModel>(response);
            });
        }

        /// <summary>
        /// PATCH /channels/{slug} - title (up to 140), category (up to 80) and tags (up to 10 of 30
        /// characters each). Setting a title while live also updates the running stream session, and a
        /// 422 means VPZone's content check refused the value.
        /// </summary>
        public async Task<Result> UpdateChannel(string slug, string title = null, string category = null, IEnumerable<string> tags = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(title)) { jobj["title"] = title; }
                if (!string.IsNullOrEmpty(category)) { jobj["category"] = category; }
                if (tags != null) { jobj["tags"] = JArray.FromObject(tags.Take(10).ToArray()); }
                if (jobj.Count == 0)
                {
                    return new Result();
                }

                HttpResponseMessage response = await this.HttpClient.PatchAsync($"channels/{AdvancedHttpClient.URLEncodeString(slug)}", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(VPZoneResponseEnvelope.ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
                }
                return new Result();
            });
        }

        /// <summary>GET /channels/{slug}/dashboard - follower and subscriber counts plus stream history.</summary>
        public async Task<VPZoneChannelDashboardModel> GetDashboard(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/dashboard");
                return VPZoneResponseEnvelope.Unwrap<VPZoneChannelDashboardModel>(response);
            });
        }

        /// <summary>GET /categories - ordered by live stream count descending, capped at 100 per call.</summary>
        public async Task<IEnumerable<VPZoneCategoryModel>> GetCategories(int limit = 100)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"categories?limit={Math.Min(Math.Max(limit, 1), 100)}");
                return VPZoneCategoryModel.ParseList(response);
            });
        }

        // ===== Chat =====

        /// <summary>
        /// POST /channels/{slug}/chat - sends as the authenticated member. The chat gateway is the
        /// primary send path; this is the fallback used when the socket is down, and the id it returns
        /// matches the id on the resulting broadcast frame.
        /// </summary>
        public async Task<VPZoneChatMessageModel> SendChatMessage(string slug, string message, string replyToMessageID = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["message"] = message;
                if (!string.IsNullOrEmpty(replyToMessageID)) { jobj["reply_to_message_id"] = replyToMessageID; }

                JToken response = await this.HttpClient.PostAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat", AdvancedHttpClient.CreateContentFromObject(jobj));
                return VPZoneResponseEnvelope.Unwrap<VPZoneChatMessageModel>(response);
            });
        }

        /// <summary>
        /// GET /channels/{slug}/chat - recent history, used to reconcile after downtime. The socket's
        /// own replay covers ordinary reconnects, so this is only for a cold start.
        /// </summary>
        public async Task<VPZoneChatHistoryModel> GetChatHistory(string slug, int limit = 50)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat?limit={Math.Min(Math.Max(limit, 1), 100)}");
                return VPZoneResponseEnvelope.Unwrap<VPZoneChatHistoryModel>(response);
            });
        }

        /// <summary>POST /channels/{slug}/chat/announcements - the highlighted announcement banner.</summary>
        public async Task<Result> SendAnnouncement(string slug, string message)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["message"] = message;

                HttpResponseMessage response = await this.HttpClient.PostAsync($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat/announcements", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    // The status is carried through because the two failure modes look nothing alike in
                    // support: a 4xx is something the streamer's own setup can explain, while a 5xx is
                    // VPZone rejecting a request that matches its own published contract.
                    return new Result($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {VPZoneResponseEnvelope.ExtractErrorMessage(await response.Content.ReadAsStringAsync())}");
                }
                return new Result();
            });
        }

        // ===== Moderation =====

        /// <summary>
        /// GET /api/chat/{slug}/viewers - the usernames currently connected to the channel's chat.
        /// The gateway's presence frame only carries a count, so this is the only way to know who is
        /// actually watching rather than only who has spoken.
        /// </summary>
        public async Task<IEnumerable<string>> GetChatViewers(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JObject response = await this.HttpClient.GetAsync<JObject>($"{ChatServiceBaseAddress}{AdvancedHttpClient.URLEncodeString(slug)}/viewers");
                JToken usernames = response?["usernames"];
                if (usernames == null || usernames.Type != JTokenType.Array)
                {
                    return null;
                }

                return usernames.Select(u => u?.ToString()).Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
            });
        }

        /// <summary>
        /// POST /channels/{slug}/chat/moderation/clear - wipes the room for everyone connected and
        /// drops the server's replay buffer, so someone joining a moment later does not see what was
        /// just cleared. Broadcasts a clear_chat frame. Stored history is left alone, which matches
        /// what a clear means on the other platforms: it governs what viewers see, not the record.
        /// </summary>
        public async Task<VPZoneModerationResult> ClearChat(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return new VPZoneModerationResult("a channel is required to clear chat");
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                HttpResponseMessage response = await this.HttpClient.PostAsync($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat/moderation/clear", content: null);
                return await BuildModerationResult(response, "clear");
            });
        }

        /// <summary>
        /// POST /channels/{slug}/chat/moderation/pin - pins an existing message as the channel's
        /// banner. Unlike Twitch the pin is applied to a message that has already been sent rather
        /// than set as it goes out, so the caller needs the message id first. Note the camel-cased
        /// messageId: this route takes it that way while the rest of v1 is snake_case.
        /// </summary>
        public async Task<VPZoneModerationResult> PinChatMessage(string slug, string messageID)
        {
            if (string.IsNullOrWhiteSpace(messageID))
            {
                return new VPZoneModerationResult("a message id is required to pin a message");
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["messageId"] = messageID;

                HttpResponseMessage response = await this.HttpClient.PostAsync($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat/moderation/pin", AdvancedHttpClient.CreateContentFromObject(jobj));
                return await BuildModerationResult(response, "pin");
            });
        }

        /// <summary>
        /// DELETE /channels/{slug}/chat/moderation/pin - clears whatever is currently pinned, whoever
        /// pinned it. Broadcasts pin_update with a null payload, which is how clients drop the banner.
        /// </summary>
        public async Task<VPZoneModerationResult> UnpinChatMessage(string slug)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                HttpResponseMessage response = await this.HttpClient.DeleteAsyncWithResponse($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat/moderation/pin");
                return await BuildModerationResult(response, "unpin");
            });
        }

        /// <summary>DELETE /channels/{slug}/chat/moderation?messageId= - removes one message.</summary>
        public async Task<VPZoneModerationResult> DeleteChatMessage(string slug, string messageID)
        {
            if (string.IsNullOrWhiteSpace(messageID))
            {
                return new VPZoneModerationResult("a message id is required to delete a message");
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                HttpResponseMessage response = await this.HttpClient.DeleteAsyncWithResponse($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat/moderation?messageId={AdvancedHttpClient.URLEncodeString(messageID)}");
                return await BuildModerationResult(response, "delete");
            });
        }

        /// <summary>
        /// POST /channels/{slug}/chat/moderation/bans. Omitting duration_seconds makes the ban
        /// permanent, which is how a timeout and a ban share one endpoint.
        /// </summary>
        public async Task<VPZoneModerationResult> BanUser(string slug, string targetUsername, string reason = null, int? durationSeconds = null)
        {
            if (string.IsNullOrWhiteSpace(targetUsername))
            {
                return new VPZoneModerationResult("a target username is required to ban a user");
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["target_username"] = targetUsername;
                if (!string.IsNullOrEmpty(reason)) { jobj["reason"] = reason; }
                if (durationSeconds.HasValue) { jobj["duration_seconds"] = Math.Max(durationSeconds.Value, 1); }

                HttpResponseMessage response = await this.HttpClient.PostAsync($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat/moderation/bans", AdvancedHttpClient.CreateContentFromObject(jobj));
                return await BuildModerationResult(response, durationSeconds.HasValue ? "timeout" : "ban");
            });
        }

        /// <summary>DELETE /channels/{slug}/chat/moderation/bans?username= - lifts a ban or timeout.</summary>
        public async Task<VPZoneModerationResult> UnbanUser(string slug, string targetUsername)
        {
            if (string.IsNullOrWhiteSpace(targetUsername))
            {
                return new VPZoneModerationResult("a target username is required to unban a user");
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                HttpResponseMessage response = await this.HttpClient.DeleteAsyncWithResponse($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/chat/moderation/bans?username={AdvancedHttpClient.URLEncodeString(targetUsername)}");
                return await BuildModerationResult(response, "unban");
            });
        }

        private static async Task<VPZoneModerationResult> BuildModerationResult(HttpResponseMessage response, string action)
        {
            if (response == null)
            {
                return new VPZoneModerationResult($"chat moderation '{action}' could not be sent");
            }

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                return new VPZoneModerationResult($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} on chat moderation '{action}': {VPZoneResponseEnvelope.ExtractErrorMessage(body)}")
                {
                    StatusCode = (int)response.StatusCode,
                    ErrorCode = VPZoneResponseEnvelope.ExtractErrorCode(body),
                };
            }
            return new VPZoneModerationResult();
        }

        // ===== Channel points =====

        public async Task<IEnumerable<VPZoneChannelPointRewardModel>> GetChannelPointRewards(string slug)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                // Disabled rewards are included so the channel points command editor can still map to a
                // reward the streamer has temporarily turned off.
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/points/rewards");
                return VPZoneChannelPointRewardModel.ParseList(response);
            });
        }

        /// <summary>POST /channels/{slug}/points/rewards - VPZone caps a channel at 20 custom rewards.</summary>
        public async Task<VPZoneChannelPointRewardModel> CreateChannelPointReward(string slug, string name, int cost, bool requirePrompt = false, string promptLabel = null, int cooldownSeconds = 0)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["name"] = name;
                jobj["cost"] = Math.Min(Math.Max(cost, 1), 1000000);
                jobj["require_prompt"] = requirePrompt;
                if (!string.IsNullOrEmpty(promptLabel)) { jobj["prompt_label"] = promptLabel; }
                jobj["cooldown_sec"] = Math.Min(Math.Max(cooldownSeconds, 0), 86400);

                JToken response = await this.HttpClient.PostAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/points/rewards", AdvancedHttpClient.CreateContentFromObject(jobj));
                return VPZoneResponseEnvelope.Unwrap<VPZoneChannelPointRewardModel>(response);
            });
        }

        public async Task<Result> UpdateChannelPointReward(string slug, string rewardID, JObject changes)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                HttpResponseMessage response = await this.HttpClient.PatchAsync($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/points/rewards/{AdvancedHttpClient.URLEncodeString(rewardID)}", AdvancedHttpClient.CreateContentFromObject(changes ?? new JObject()));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(VPZoneResponseEnvelope.ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
                }
                return new Result();
            });
        }

        public async Task<Result> DeleteChannelPointReward(string slug, string rewardID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                bool success = await this.HttpClient.DeleteAsync($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/points/rewards/{AdvancedHttpClient.URLEncodeString(rewardID)}");
                return success ? new Result() : new Result(Resources.VPZoneChannelPointRewardDeleteFailed);
            });
        }

        /// <summary>GET /channels/{slug}/points/redemptions - newest first, keyset-paginated on created_at.</summary>
        public async Task<VPZonePagedListModel<VPZoneChannelPointRedemptionModel>> GetChannelPointRedemptions(string slug, string status = null, int limit = 50, string before = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                List<string> query = new List<string>() { "limit=" + Math.Min(Math.Max(limit, 1), 100) };
                if (!string.IsNullOrEmpty(status)) { query.Add("status=" + AdvancedHttpClient.URLEncodeString(status)); }
                if (!string.IsNullOrEmpty(before)) { query.Add("before=" + AdvancedHttpClient.URLEncodeString(before)); }

                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/points/redemptions?{string.Join("&", query)}");
                return VPZonePagedListModel<VPZoneChannelPointRedemptionModel>.Parse(response);
            });
        }

        public async Task<VPZoneChannelPointBalanceModel> GetChannelPointBalance(string slug, string userID)
        {
            if (string.IsNullOrWhiteSpace(userID))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/points/balance?user_id={AdvancedHttpClient.URLEncodeString(userID)}");
                return VPZoneResponseEnvelope.Unwrap<VPZoneChannelPointBalanceModel>(response);
            });
        }

        /// <summary>
        /// POST /channels/{slug}/points/grant. VPZone accepts an API key here and rejects OAuth tokens,
        /// and the key is bound to a single channel at creation, so this only works once the streamer
        /// has supplied a channel-bound grant key. The idempotency key makes a retry safe.
        /// </summary>
        public async Task<VPZoneChannelPointGrantResultModel> GrantChannelPoints(string slug, string userID, int amount, string reason = null, string idempotencyKey = null)
        {
            if (string.IsNullOrWhiteSpace(userID) || amount <= 0)
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["user_id"] = userID;
                jobj["amount"] = Math.Min(amount, 1000000);
                if (!string.IsNullOrEmpty(reason)) { jobj["reason"] = reason; }
                jobj["idempotency_key"] = string.IsNullOrEmpty(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey;

                JToken response = await this.HttpClient.PostAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/points/grant", AdvancedHttpClient.CreateContentFromObject(jobj));
                return VPZoneResponseEnvelope.Unwrap<VPZoneChannelPointGrantResultModel>(response);
            });
        }

        // ===== Activity feeds (reconciliation after downtime) =====

        public async Task<VPZoneRecentActivityModel> GetRecentActivity(string slug)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/activity/recent");
                return VPZoneResponseEnvelope.Unwrap<VPZoneRecentActivityModel>(response);
            });
        }

        public async Task<VPZonePagedListModel<VPZoneSubscriberModel>> GetRecentSubscribers(string slug, int limit = 50, string before = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/subscriptions/recent{BuildPagingQuery(limit, before)}");
                return VPZonePagedListModel<VPZoneSubscriberModel>.Parse(response);
            });
        }

        public async Task<VPZonePagedListModel<VPZoneMemberEventModel>> GetRecentFollowers(string slug, int limit = 50, string before = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/follows/recent{BuildPagingQuery(limit, before)}");
                return VPZonePagedListModel<VPZoneMemberEventModel>.Parse(response);
            });
        }

        public async Task<VPZonePagedListModel<VPZonePixelCheerModel>> GetRecentPixelCheers(string slug, int limit = 50, string before = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/pixels/recent{BuildPagingQuery(limit, before)}");
                return VPZonePagedListModel<VPZonePixelCheerModel>.Parse(response);
            });
        }

        public async Task<VPZonePagedListModel<VPZoneRaidModel>> GetRecentRaids(string slug, int limit = 50, string before = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/raids/recent{BuildPagingQuery(limit, before)}");
                return VPZonePagedListModel<VPZoneRaidModel>.Parse(response);
            });
        }

        public async Task<VPZonePagedListModel<VPZoneClipModel>> GetRecentClips(string slug, int limit = 50, string before = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"channels/{AdvancedHttpClient.URLEncodeString(slug)}/clips/recent{BuildPagingQuery(limit, before)}");
                return VPZonePagedListModel<VPZoneClipModel>.Parse(response);
            });
        }

        private static string BuildPagingQuery(int limit, string before)
        {
            string query = "?limit=" + Math.Min(Math.Max(limit, 1), 100);
            if (!string.IsNullOrEmpty(before))
            {
                query += "&before=" + AdvancedHttpClient.URLEncodeString(before);
            }
            return query;
        }

        // ===== Webhooks =====

        public async Task<IEnumerable<VPZoneWebhookModel>> GetWebhooks()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("webhooks");
                JArray array = VPZoneResponseEnvelope.Unwrap(response) as JArray;
                List<VPZoneWebhookModel> webhooks = new List<VPZoneWebhookModel>();
                if (array != null)
                {
                    foreach (JToken item in array)
                    {
                        try
                        {
                            VPZoneWebhookModel webhook = item?.ToObject<VPZoneWebhookModel>();
                            if (webhook != null) { webhooks.Add(webhook); }
                        }
                        catch (Newtonsoft.Json.JsonException) { }
                    }
                }
                return (IEnumerable<VPZoneWebhookModel>)webhooks;
            });
        }

        /// <summary>POST /webhooks - the secret is returned only here, so it has to be captured now.</summary>
        public async Task<VPZoneWebhookModel> CreateWebhook(string url, IEnumerable<string> events)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["url"] = url;
                jobj["events"] = JArray.FromObject((events ?? VPZoneWebhookEventTypes.All).ToArray());

                JToken response = await this.HttpClient.PostAsync<JToken>("webhooks", AdvancedHttpClient.CreateContentFromObject(jobj));
                return VPZoneResponseEnvelope.Unwrap<VPZoneWebhookModel>(response);
            });
        }

        public async Task<Result> DeleteWebhook(string webhookID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                bool success = await this.HttpClient.DeleteAsync("webhooks/" + AdvancedHttpClient.URLEncodeString(webhookID));
                return success ? new Result() : new Result(Resources.VPZoneWebhookDeleteFailed);
            });
        }

        /// <summary>
        /// Whether the current OAuth token was authorized with the given scope. Lets callers skip an
        /// endpoint a pre-upgrade token cannot reach instead of collecting 403s.
        /// </summary>
        public bool HasScope(string scope)
        {
            string scopeList = this.GetOAuthTokenCopy()?.ScopeList;
            return !string.IsNullOrEmpty(scopeList) && scopeList.Split(',').Contains(scope, StringComparer.OrdinalIgnoreCase);
        }

        // ===== OAuth (authorization code with PKCE) =====

        protected override async Task<string> GetAuthorizationCodeURL(IEnumerable<string> scopes, string state, bool forceApprovalPrompt = false)
        {
            string codeVerifier = CreateCodeVerifier();
            string codeChallenge = CreateCodeChallenge(codeVerifier);
            this.stateToCodeVerifier[state] = codeVerifier;

            // VPZone requires PKCE with S256 and rejects an authorize request that omits state or the
            // challenge, so every parameter below is mandatory rather than optional hardening.
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
                Logger.Log(LogLevel.Error, "VPZone OAuth PKCE verifier missing for token exchange");
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

        public override async Task Disable()
        {
            // Revoking on the way out leaves no live grant behind when the streamer logs the account
            // out of Mix It Up. Best effort: a failure here must not block the disconnect.
            try
            {
                string accessToken = this.GetOAuthTokenCopy()?.accessToken;
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    JObject body = new JObject();
                    body["client_id"] = this.ClientID;
                    body["client_secret"] = this.ClientSecret;
                    body["token"] = accessToken;

                    using (AdvancedHttpClient client = new AdvancedHttpClient())
                    {
                        await client.PostAsync(OAuthRevokeAddress, AdvancedHttpClient.CreateContentFromObject(body));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            await base.Disable();
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
