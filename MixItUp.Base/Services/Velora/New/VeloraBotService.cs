using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.Velora.Bots;
using MixItUp.Base.Model.Velora.Users;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Velora.New
{
    /// <summary>
    /// The session's <see cref="StreamingPlatformSessionBase.BotOAuthService"/> for Velora. Unlike the
    /// other platforms, a Velora bot is not an independent account with its own OAuth login: it is a bot
    /// entity owned by the streamer's account (Velora Bot Studio, or created here via
    /// POST /integrations/oauth/bot/create) that Velora connects to this OAuth app server-side. Chat is
    /// then sent as the bot with sendAsBot on the STREAMER's token, so this service never authenticates
    /// anything itself.
    ///
    /// "Connecting" the bot therefore means create-or-select over the streamer's service, and the
    /// persisted BotOAuthToken is a synthetic connection marker - it only records that the user enabled
    /// the bot (IsBotEnabled / startup auto-connect), never authorizes a request. The connection itself
    /// lives on Velora's side and GET /integrations/oauth/bot/current is its source of truth.
    /// </summary>
    public class VeloraBotService : VeloraService
    {
        private const string BotConnectionMarkerTokenValue = "velora-bot-connection";
        private const string BotConnectionMarkerScope = "velora:bot-connection";

        // Matches Velora's live validation ("Bot name can only contain letters, numbers, and underscores",
        // min 3) and the 20-char cap UpdateBotDto documents.
        private static readonly Regex BotNameRegex = new Regex(@"^[A-Za-z0-9_]{3,20}$", RegexOptions.Compiled);

        private const int MaximumBotNamePromptAttempts = 5;

        private readonly VeloraService streamerService;

        public override string Name { get { return "Velora Bot"; } }

        /// <summary>The bot Velora reports as connected to this app; set by both connect paths.</summary>
        public VeloraBotModel ConnectedBot { get; private set; }

        public VeloraBotService(VeloraService streamerService)
            : base(new List<string>(), isBotService: true)
        {
            this.streamerService = streamerService;
        }

        public override async Task<Result> AutomaticConnect()
        {
            StreamingPlatformAuthenticationSettingsModel authenticationSettings = this.GetAuthenticationSettings();
            if (!(authenticationSettings?.IsBotEnabled ?? false))
            {
                return new Result(success: false);
            }

            // Replace whatever was stored (a prior marker, or a real bot OAuth token from the old
            // independent-account model) with a fresh marker; it is what settings saves persist.
            this.OAuthToken = CreateBotConnectionMarkerToken(this.ClientID);

            // The bot rides the streamer's token, and at startup the streamer connects concurrently -
            // wait for it before asking Velora about the bot connection.
            for (int i = 0; i < 60 && !this.streamerService.IsConnected; i++)
            {
                await Task.Delay(500);
            }
            if (!this.streamerService.IsConnected)
            {
                return new Result(Resources.VeloraBotStreamerAccountRequired);
            }

            VeloraBotCurrentResponseModel current = await this.streamerService.GetCurrentBot();
            if (current == null || !current.HasConnectedBot)
            {
                // The user revoked the connection from Velora's Bot Studio (or it never completed);
                // failing here routes them to the standard "reconnect the bot account" startup prompt.
                return new Result(Resources.VeloraBotNotConnected);
            }

            this.ConnectedBot = current.Bot;
            this.IsConnected = true;
            return new Result();
        }

        public override async Task<Result> ManualConnect(CancellationToken cancellationToken)
        {
            if (this.streamerService == null || !this.streamerService.IsConnected)
            {
                return new Result(Resources.VeloraBotStreamerAccountRequired);
            }

            // Offer every bot the user owns; fold in an already-connected bot the available list omits.
            List<VeloraBotModel> bots = (await this.streamerService.GetAvailableBots())?.ToList() ?? new List<VeloraBotModel>();
            VeloraBotCurrentResponseModel current = await this.streamerService.GetCurrentBot();
            if (current != null && current.HasConnectedBot && !bots.Any(b => string.Equals(b.BestID, current.Bot.BestID, StringComparison.OrdinalIgnoreCase)))
            {
                bots.Insert(0, current.Bot);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new Result(Resources.VeloraBotSetupCanceled);
            }

            bool createNewBot = bots.Count == 0;
            if (bots.Count > 0)
            {
                // Bot usernames are lowercased platform-side; the display name keeps the typed casing.
                List<string> options = bots.Select(b => b.BestDisplayName).ToList();
                options.Add(Resources.VeloraBotCreateNewBotOption);

                string selection = null;
                await DispatcherHelper.Dispatcher.InvokeAsync(async () =>
                {
                    selection = await DialogHelper.ShowDropDown(options, Resources.VeloraBotSelectBotPrompt);
                });
                if (string.IsNullOrEmpty(selection) || cancellationToken.IsCancellationRequested)
                {
                    return new Result(Resources.VeloraBotSetupCanceled);
                }

                if (string.Equals(selection, Resources.VeloraBotCreateNewBotOption, StringComparison.Ordinal))
                {
                    createNewBot = true;
                }
                else
                {
                    VeloraBotModel selectedBot = bots.FirstOrDefault(b => string.Equals(b.BestDisplayName, selection, StringComparison.Ordinal));
                    if (selectedBot == null)
                    {
                        return new Result(Resources.VeloraBotSetupCanceled);
                    }

                    Result selectResult = await this.streamerService.SelectBot(selectedBot.BestID);
                    if (!selectResult.Success)
                    {
                        return new Result(string.Format(Resources.VeloraBotConnectFailed, ExtractErrorMessage(selectResult.Message)));
                    }
                }
            }

            if (createNewBot)
            {
                Result createResult = await this.PromptForNameAndCreateBot(cancellationToken);
                if (!createResult.Success)
                {
                    return createResult;
                }
            }

            // Velora's stored connection is the source of truth for what actually got connected.
            current = await this.streamerService.GetCurrentBot();
            if (current == null || !current.HasConnectedBot)
            {
                return new Result(Resources.VeloraBotFailedToConnect);
            }

            this.ConnectedBot = current.Bot;
            this.OAuthToken = CreateBotConnectionMarkerToken(this.ClientID);
            this.IsConnected = true;

            // A brand-new bot has no profile image, so offer one now as an optional (skippable) step.
            // Best-effort: an upload problem must not fail the connect - Edit Bot can retry later.
            if (createNewBot && !cancellationToken.IsCancellationRequested)
            {
                Result avatarResult = await this.PromptForAvatar(offerSkip: true, cancellationToken);
                if (!avatarResult.Success && !string.IsNullOrEmpty(avatarResult.Message))
                {
                    await ShowMessageOnDispatcher(avatarResult.Message);
                }
            }

            return new Result();
        }

        /// <summary>
        /// Shows the image browser (with a Skip button during setup, Cancel in the edit flow), uploads
        /// the chosen file as the connected bot's avatar, and re-adopts Velora's view of the bot so the
        /// new avatarUrl flows into ConnectedBot. Dismissing the dialog is a successful no-op.
        /// </summary>
        public async Task<Result> PromptForAvatar(bool offerSkip, CancellationToken cancellationToken)
        {
            if (this.ConnectedBot == null || string.IsNullOrWhiteSpace(this.ConnectedBot.BestID))
            {
                return new Result(Resources.VeloraBotNotConnected);
            }

            string filePath = null;
            await DispatcherHelper.Dispatcher.InvokeAsync(async () =>
            {
                filePath = await DialogHelper.ShowImageFileBrowser(Resources.VeloraBotAvatarPrompt,
                    VeloraService.BotAvatarValidExtensions, VeloraService.BotAvatarMaxFileSizeBytes,
                    cancelText: offerSkip ? Resources.Skip : Resources.Cancel);
            });
            if (string.IsNullOrEmpty(filePath) || cancellationToken.IsCancellationRequested)
            {
                return new Result();
            }

            Result result = await this.streamerService.UploadBotAvatar(this.ConnectedBot.BestID, filePath);
            if (!result.Success)
            {
                return new Result(string.Format(Resources.VeloraBotAvatarUploadFailed, ExtractErrorMessage(result.Message)));
            }

            await this.RefreshConnectedBot();
            return new Result();
        }

        /// <summary>
        /// Renames the connected bot in place via PATCH /api/bots/{id} (a rename, never a re-create; the
        /// bot's ID and app connection survive). Velora enforces one rename per 90 days, so the cooldown
        /// endpoint is consulted first; its response shape is unpublished and is parsed permissively -
        /// when in doubt the rename is attempted and the server's own rejection is surfaced.
        /// </summary>
        public async Task<Result> PromptRenameBot(CancellationToken cancellationToken)
        {
            if (this.ConnectedBot == null || string.IsNullOrWhiteSpace(this.ConnectedBot.BestID))
            {
                return new Result(Resources.VeloraBotNotConnected);
            }

            JToken cooldown = await this.streamerService.GetBotUsernameCooldown(this.ConnectedBot.BestID);
            if (cooldown is JObject cooldownObj)
            {
                foreach (string key in new string[] { "canChange", "canRename", "allowed", "available" })
                {
                    if (cooldownObj[key]?.Type == JTokenType.Boolean && !cooldownObj.Value<bool>(key))
                    {
                        string nextAllowed = UserModel.FirstNonEmpty(cooldownObj.Value<string>("nextChangeAt"), cooldownObj.Value<string>("availableAt"), cooldownObj.Value<string>("cooldownEndsAt"));
                        string message = Resources.VeloraBotRenameOnCooldown;
                        if (!string.IsNullOrWhiteSpace(nextAllowed))
                        {
                            message += $" ({nextAllowed})";
                        }
                        return new Result(message);
                    }
                }
            }

            string lastEntry = this.ConnectedBot.BestDisplayName;
            for (int attempt = 0; attempt < MaximumBotNamePromptAttempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new Result();
                }

                string name = null;
                await DispatcherHelper.Dispatcher.InvokeAsync(async () =>
                {
                    name = await DialogHelper.ShowTextEntry(Resources.VeloraBotEnterBotName, lastEntry, Resources.VeloraBotNameRequirements);
                });
                if (string.IsNullOrWhiteSpace(name))
                {
                    return new Result();
                }

                name = name.Trim();
                lastEntry = name;

                if (!BotNameRegex.IsMatch(name))
                {
                    await ShowMessageOnDispatcher(Resources.VeloraBotNameRequirements);
                    continue;
                }

                // Usernames are lowercased platform-side; the same name in different casing only changes
                // the display name, so skip the availability check the user's own name would fail.
                bool sameUsername = string.Equals(name, this.ConnectedBot.BestUsername, StringComparison.OrdinalIgnoreCase);
                if (!sameUsername && !await this.streamerService.CheckBotNameAvailability(name))
                {
                    await ShowMessageOnDispatcher(string.Format(Resources.VeloraBotNameUnavailable, name));
                    continue;
                }

                Result updateResult = await this.streamerService.UpdateBot(this.ConnectedBot.BestID,
                    username: sameUsername ? null : name, displayName: name);
                if (updateResult.Success)
                {
                    await this.RefreshConnectedBot();
                    return new Result();
                }

                string error = ExtractErrorMessage(updateResult.Message);
                if (error.IndexOf("taken", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    await ShowMessageOnDispatcher(string.Format(Resources.VeloraBotNameUnavailable, name));
                    continue;
                }
                return new Result(string.Format(Resources.VeloraBotRenameFailed, error));
            }
            return new Result();
        }

        private async Task RefreshConnectedBot()
        {
            VeloraBotCurrentResponseModel current = await this.streamerService.GetCurrentBot();
            if (current != null && current.HasConnectedBot)
            {
                this.ConnectedBot = current.Bot;
            }
        }

        public override async Task Disable()
        {
            // Logging the bot out of Mix It Up means severing the app connection on Velora's side too
            // (the bot entity itself survives; it belongs to the user). Best-effort: also cleans up a
            // half-completed setup, and disconnecting when nothing is connected is a server-side no-op.
            try
            {
                if (this.streamerService != null && this.streamerService.IsConnected)
                {
                    Result result = await this.streamerService.DisconnectBot();
                    if (result != null && !result.Success)
                    {
                        Logger.Log(LogLevel.Error, "Failed to disconnect the Velora bot from the app: " + result.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            this.ConnectedBot = null;

            await base.Disable();
        }

        /// <summary>The marker token is not an OAuth credential, so there is nothing to refresh. Its
        /// far-future expiry keeps the shared refresh plumbing from ever landing here, but guard anyway.</summary>
        protected override Task RefreshOAuthToken()
        {
            return Task.CompletedTask;
        }

        private async Task<Result> PromptForNameAndCreateBot(CancellationToken cancellationToken)
        {
            string lastEntry = null;
            for (int attempt = 0; attempt < MaximumBotNamePromptAttempts; attempt++)
            {
                string name = null;
                await DispatcherHelper.Dispatcher.InvokeAsync(async () =>
                {
                    name = await DialogHelper.ShowTextEntry(Resources.VeloraBotEnterBotName, lastEntry, Resources.VeloraBotNameRequirements);
                });
                if (string.IsNullOrWhiteSpace(name) || cancellationToken.IsCancellationRequested)
                {
                    return new Result(Resources.VeloraBotSetupCanceled);
                }

                name = name.Trim();
                lastEntry = name;

                if (!BotNameRegex.IsMatch(name))
                {
                    await ShowMessageOnDispatcher(Resources.VeloraBotNameRequirements);
                    continue;
                }

                if (!await this.streamerService.CheckBotNameAvailability(name))
                {
                    await ShowMessageOnDispatcher(string.Format(Resources.VeloraBotNameUnavailable, name));
                    continue;
                }

                Result createResult = await this.streamerService.CreateBot(name);
                if (createResult.Success)
                {
                    return new Result();
                }

                string error = ExtractErrorMessage(createResult.Message);

                // A name race (409) is re-promptable; anything else (403 not an affiliate / bot limit
                // reached, transport failure) will not get better by retyping the name.
                if (error.IndexOf("taken", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    await ShowMessageOnDispatcher(string.Format(Resources.VeloraBotNameUnavailable, name));
                    continue;
                }
                return new Result(string.Format(Resources.VeloraBotCreateFailed, error));
            }
            return new Result(Resources.VeloraBotSetupCanceled);
        }

        private static OAuthTokenModel CreateBotConnectionMarkerToken(string clientID)
        {
            return new OAuthTokenModel()
            {
                clientID = clientID,
                accessToken = BotConnectionMarkerTokenValue,
                refreshToken = BotConnectionMarkerTokenValue,
                // Far enough out that RefreshOAuthTokenIfCloseToExpiring never considers it expiring.
                expiresIn = (long)TimeSpan.FromDays(3650).TotalSeconds,
                ScopeList = BotConnectionMarkerScope,
            };
        }

        private static async Task ShowMessageOnDispatcher(string message)
        {
            await DispatcherHelper.Dispatcher.InvokeAsync(async () => await DialogHelper.ShowMessage(message));
        }

        /// <summary>Velora error bodies are {"message": string | string[], "error": ..., "statusCode": ...};
        /// pull the human part out for dialogs, falling back to the raw body.</summary>
        private static string ExtractErrorMessage(string responseBody)
        {
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    JObject jobj = JObject.Parse(responseBody);
                    JToken message = jobj["message"];
                    if (message is JArray array)
                    {
                        return string.Join(" ", array.Select(t => t.ToString()));
                    }
                    if (message != null && message.Type == JTokenType.String)
                    {
                        return message.ToString();
                    }
                }
                catch { }
            }
            return responseBody ?? string.Empty;
        }
    }
}
