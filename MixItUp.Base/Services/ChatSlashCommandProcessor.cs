using MixItUp.Base.Model;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public enum SlashCommandResultEnum
    {
        /// <summary>The text is not a slash command Mix It Up knows about; send it as a normal chat message.</summary>
        NotRecognized,
        /// <summary>The command ran on at least one platform; never echo it as a chat message.</summary>
        Handled,
        /// <summary>The command was recognized but could not run; the user has been alerted. Do not echo it.</summary>
        Rejected,
    }

    /// <summary>
    /// Interprets text typed into the Mix It Up chat box as a slash command.
    ///
    /// Platforms disagree about which slash commands they implement, and some (Velora) execute them
    /// server-side when they arrive as an ordinary chat message. Broadcasting the raw text therefore both
    /// leaks the command as visible chat on the platforms that do not implement it and quietly relies on
    /// server-side parsing on the ones that do. Instead, every command Mix It Up knows about is executed
    /// here against the platform APIs and then swallowed, so it never reaches chat as text.
    ///
    /// A command runs on every targeted platform that implements it - "/title" sets the title everywhere,
    /// "/announce" announces on both Twitch and Velora - and simply does not run on the ones that do not.
    ///
    /// Text that is not a known command (including Twitch's "/me") is left alone and sent as a chat message.
    /// </summary>
    public static class ChatSlashCommandProcessor
    {
        private const string AnnouncePrefix = "announce";

        // The /announce accent colors, which differ per platform; the bare "/announce" carries no suffix and
        // uses each platform's default. "/announceblue" runs on both, "/announcepurple" is Twitch-only and
        // "/announcegold" is Velora-only, so the suffix decides which platforms a given announce targets.
        private static readonly HashSet<string> TwitchAnnounceColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            string.Empty, "blue", "green", "orange", "purple",
        };

        private static readonly HashSet<string> VeloraAnnounceColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            string.Empty, "blue", "gold", "green", "red", "orange", "coral",
        };

        private static readonly HashSet<string> ChannelPointDirections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "add", "grant", "remove", "deduct",
        };

        /// <summary>
        /// Runs <paramref name="text"/> as a slash command against <paramref name="selectedPlatform"/> (or every
        /// connected platform when it is <see cref="StreamingPlatformTypeEnum.All"/>).
        /// </summary>
        public static async Task<SlashCommandResultEnum> Process(string text, StreamingPlatformTypeEnum selectedPlatform, bool sendAsStreamer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return SlashCommandResultEnum.NotRecognized;
                }

                string trimmed = text.Trim();
                if (trimmed.Length < 2 || trimmed[0] != '/')
                {
                    return SlashCommandResultEnum.NotRecognized;
                }

                string[] parts = trimmed.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string name = parts[0].Substring(1).ToLowerInvariant();
                List<string> arguments = parts.Skip(1).ToList();

                if (!IsKnownCommand(name))
                {
                    return SlashCommandResultEnum.NotRecognized;
                }

                List<StreamingPlatformTypeEnum> targets = GetTargetPlatforms(selectedPlatform).Where(p => IsSupportedOn(name, p)).ToList();

                // "/clear" also clears the local Mix It Up window, so it stays available with nothing connected.
                if (targets.Count == 0 && name != "clear")
                {
                    await Alert(string.Format(MixItUp.Base.Resources.ChatCommandNotSupportedOnPlatform, name));
                    return SlashCommandResultEnum.Rejected;
                }

                return await Run(name, arguments, trimmed, targets, selectedPlatform, sendAsStreamer);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return SlashCommandResultEnum.Rejected;
            }
        }

        /// <summary>The single source of truth for which platform implements which command.</summary>
        private static bool IsSupportedOn(string name, StreamingPlatformTypeEnum platform)
        {
            switch (name)
            {
                // Implemented by every StreamingPlatformSessionBase.
                case "ban":
                case "unban":
                case "timeout":
                case "untimeout":
                case "purge":
                case "mod":
                case "unmod":
                case "clear":
                case "title":
                case "settitle":
                case "game":
                case "setgame":
                case "category":
                    return true;

                // ChatService.Whisper only has a Twitch implementation.
                case "w":
                case "whisper":
                    return platform == StreamingPlatformTypeEnum.Twitch;

                // Twitch and Velora both implement these; neither has a StreamingPlatformSessionBase hook, so
                // each is dispatched against that platform's own session.
                case "vip":
                case "unvip":
                case "raid":
                case "shoutout":
                case "so":
                    return platform == StreamingPlatformTypeEnum.Twitch || platform == StreamingPlatformTypeEnum.Velora;

                // Velora's channel-point slash command has no counterpart elsewhere in Mix It Up.
                case "cp":
                    return platform == StreamingPlatformTypeEnum.Velora;

                default:
                    return IsAnnounceColorSupportedOn(name, platform);
            }
        }

        private static bool IsKnownCommand(string name) { return StreamingPlatforms.SupportedPlatforms.Any(p => IsSupportedOn(name, p)); }

        private static bool IsAnnounceCommand(string name)
        {
            return StreamingPlatforms.SupportedPlatforms.Any(p => IsAnnounceColorSupportedOn(name, p));
        }

        private static bool IsAnnounceColorSupportedOn(string name, StreamingPlatformTypeEnum platform)
        {
            if (!name.StartsWith(AnnouncePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string color = name.Substring(AnnouncePrefix.Length);
            if (platform == StreamingPlatformTypeEnum.Twitch) { return TwitchAnnounceColors.Contains(color); }
            if (platform == StreamingPlatformTypeEnum.Velora) { return VeloraAnnounceColors.Contains(color); }
            return false;
        }

        private static IEnumerable<StreamingPlatformTypeEnum> GetTargetPlatforms(StreamingPlatformTypeEnum selectedPlatform)
        {
            if (selectedPlatform == StreamingPlatformTypeEnum.All || selectedPlatform == StreamingPlatformTypeEnum.None)
            {
                return StreamingPlatforms.GetConnectedPlatforms();
            }
            return StreamingPlatforms.IsPlatformConnected(selectedPlatform) ? new List<StreamingPlatformTypeEnum>() { selectedPlatform } : new List<StreamingPlatformTypeEnum>();
        }

        private static async Task<SlashCommandResultEnum> Run(string name, List<string> arguments, string trimmed, List<StreamingPlatformTypeEnum> targets, StreamingPlatformTypeEnum selectedPlatform, bool sendAsStreamer)
        {
            switch (name)
            {
                case "ban":
                    if (arguments.Count < 1) { return await Usage("/ban <username> [reason]"); }
                    return await ForEachUserTarget(targets, arguments[0], (user) => ServiceManager.Get<ChatService>().BanUser(user, TextAfter(trimmed, 2)));

                case "unban":
                case "untimeout":
                    if (arguments.Count < 1) { return await Usage($"/{name} <username>"); }
                    return await ForEachUserTarget(targets, arguments[0], (user) =>
                    {
                        // Velora separates the two; everywhere else lifting the ban also lifts a timeout.
                        if (name == "untimeout" && user.Platform == StreamingPlatformTypeEnum.Velora)
                        {
                            return ServiceManager.Get<VeloraSession>().UntimeoutUser(user);
                        }
                        return ServiceManager.Get<ChatService>().UnbanUser(user);
                    });

                case "timeout":
                    if (arguments.Count < 2) { return await Usage("/timeout <username> <seconds> [reason]"); }
                    if (!int.TryParse(arguments[1], out int duration) || duration <= 0)
                    {
                        return await Reject(MixItUp.Base.Resources.ChatTimeoutAmountMustBeGreaterThanZero);
                    }
                    return await ForEachUserTarget(targets, arguments[0], (user) => ServiceManager.Get<ChatService>().TimeoutUser(user, duration, TextAfter(trimmed, 3)));

                case "purge":
                    if (arguments.Count < 1) { return await Usage("/purge <username>"); }
                    return await ForEachUserTarget(targets, arguments[0], (user) => ServiceManager.Get<ChatService>().PurgeUser(user));

                case "mod":
                    if (arguments.Count < 1) { return await Usage("/mod <username>"); }
                    return await ForEachUserTarget(targets, arguments[0], (user) => ServiceManager.Get<ChatService>().ModUser(user));

                case "unmod":
                    if (arguments.Count < 1) { return await Usage("/unmod <username>"); }
                    return await ForEachUserTarget(targets, arguments[0], (user) => ServiceManager.Get<ChatService>().UnmodUser(user));

                case "vip":
                    if (arguments.Count < 1) { return await Usage("/vip <username>"); }
                    return await ForEachUserTarget(targets, arguments[0], (user) => IsVelora(user)
                        ? ServiceManager.Get<VeloraSession>().VIPUser(user)
                        : ServiceManager.Get<TwitchSession>().VIPUser(user));

                case "unvip":
                    if (arguments.Count < 1) { return await Usage("/unvip <username>"); }
                    return await ForEachUserTarget(targets, arguments[0], (user) => IsVelora(user)
                        ? ServiceManager.Get<VeloraSession>().UnVIPUser(user)
                        : ServiceManager.Get<TwitchSession>().UnVIPUser(user));

                case "shoutout":
                case "so":
                    if (arguments.Count < 1) { return await Usage($"/{name} <username>"); }
                    return await ForEachUserTarget(targets, arguments[0], (user) => IsVelora(user)
                        ? ServiceManager.Get<VeloraSession>().Shoutout(user)
                        : ServiceManager.Get<TwitchSession>().Shoutout(user));

                case "clear":
                    // ClearMessages already fans out across platforms and clears the local window exactly once.
                    await ServiceManager.Get<ChatService>().ClearMessages(selectedPlatform == StreamingPlatformTypeEnum.None ? StreamingPlatformTypeEnum.All : selectedPlatform);
                    return SlashCommandResultEnum.Handled;

                case "w":
                case "whisper":
                    if (arguments.Count < 2) { return await Usage("/w <username> <message>"); }
                    await ServiceManager.Get<ChatService>().Whisper(UserService.SanitizeUsername(arguments[0]), StreamingPlatformTypeEnum.Twitch, TextAfter(trimmed, 2), sendAsStreamer);
                    return SlashCommandResultEnum.Handled;

                case "title":
                case "settitle":
                    if (arguments.Count < 1) { return await Usage("/title <stream title>"); }
                    return await ForEachPlatformTarget(targets, (p) => StreamingPlatforms.GetPlatformSession(p).SetStreamTitle(TextAfter(trimmed, 1)), MixItUp.Base.Resources.FailedToUpdateChannelInformation);

                case "game":
                case "setgame":
                case "category":
                    if (arguments.Count < 1) { return await Usage($"/{name} <category>"); }
                    return await ForEachPlatformTarget(targets, (p) => StreamingPlatforms.GetPlatformSession(p).SetStreamCategory(TextAfter(trimmed, 1)), MixItUp.Base.Resources.ErrorFailedToUpdateCategory);

                case "raid":
                    if (arguments.Count < 1) { return await Usage("/raid <channel>"); }
                    string raidTarget = UserService.SanitizeUsername(arguments[0]);
                    return await ForEachPlatformAction(targets, (p) => p == StreamingPlatformTypeEnum.Velora
                        ? ServiceManager.Get<VeloraSession>().Raid(raidTarget)
                        : ServiceManager.Get<TwitchSession>().Raid(raidTarget));

                case "cp":
                    if (arguments.Count < 3 || !ChannelPointDirections.Contains(arguments[0]))
                    {
                        return await Usage("/cp <add|remove> <amount> <target>");
                    }
                    await ServiceManager.Get<VeloraSession>().AdjustChannelPoints(arguments[0], arguments[1], arguments[2]);
                    return SlashCommandResultEnum.Handled;

                default:
                    if (IsAnnounceCommand(name))
                    {
                        if (arguments.Count < 1) { return await Usage("/announce <message>"); }

                        string colorSuffix = name.Substring(AnnouncePrefix.Length);
                        string color = string.IsNullOrEmpty(colorSuffix) ? null : colorSuffix;
                        string announcement = TextAfter(trimmed, 1);

                        // Both platforms honour the chat box's "send as" selection; Velora falls back to the
                        // streamer socket when no bot is connected.
                        return await ForEachPlatformAction(targets, (p) => p == StreamingPlatformTypeEnum.Velora
                            ? ServiceManager.Get<VeloraSession>().SendAnnouncement(announcement, color, sendAsStreamer)
                            : ServiceManager.Get<TwitchSession>().SendAnnouncement(announcement, color, sendAsStreamer));
                    }
                    return SlashCommandResultEnum.NotRecognized;
            }
        }

        private static bool IsVelora(UserV2ViewModel user) { return user.Platform == StreamingPlatformTypeEnum.Velora; }

        private static async Task<SlashCommandResultEnum> ForEachPlatformAction(List<StreamingPlatformTypeEnum> targets, Func<StreamingPlatformTypeEnum, Task> action)
        {
            foreach (StreamingPlatformTypeEnum platform in targets)
            {
                await action(platform);
            }
            return SlashCommandResultEnum.Handled;
        }

        /// <summary>
        /// Resolves <paramref name="username"/> on the target platforms and runs <paramref name="action"/> on each
        /// platform it resolved to.
        ///
        /// Usernames are not unique across platforms, so this never guesses: a user seen chatting on a platform is
        /// an unambiguous match and wins outright. Only when nobody is chatting under that name does it fall back
        /// to a platform account lookup, and if that turns up accounts on more than one platform - which may well
        /// be different people - it refuses to act and asks for an explicit platform instead of moderating both.
        /// </summary>
        private static async Task<SlashCommandResultEnum> ForEachUserTarget(List<StreamingPlatformTypeEnum> targets, string username, Func<UserV2ViewModel, Task> action)
        {
            string sanitized = UserService.SanitizeUsername(username);
            if (string.IsNullOrEmpty(sanitized))
            {
                return await Reject(MixItUp.Base.Resources.UserNotFound);
            }

            List<UserV2ViewModel> matches = targets
                .Select(p => ServiceManager.Get<UserService>().GetActiveUserByPlatform(p, platformUsername: sanitized))
                .Where(u => u != null)
                .ToList();

            if (matches.Count == 0)
            {
                foreach (StreamingPlatformTypeEnum platform in targets)
                {
                    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(platform, platformUsername: sanitized, performPlatformSearch: true);
                    if (user != null)
                    {
                        matches.Add(user);
                    }
                }

                if (matches.Count > 1)
                {
                    return await Reject(string.Format(MixItUp.Base.Resources.ChatCommandUserOnMultiplePlatforms, sanitized));
                }
            }

            if (matches.Count == 0)
            {
                return await Reject(MixItUp.Base.Resources.UserNotFound);
            }

            foreach (UserV2ViewModel user in matches)
            {
                await action(user);
            }
            return SlashCommandResultEnum.Handled;
        }

        private static async Task<SlashCommandResultEnum> ForEachPlatformTarget(List<StreamingPlatformTypeEnum> targets, Func<StreamingPlatformTypeEnum, Task<Result>> action, string failureMessage)
        {
            bool succeededAnywhere = false;
            foreach (StreamingPlatformTypeEnum platform in targets)
            {
                Result result = await action(platform);
                if (result != null && result.Success)
                {
                    succeededAnywhere = true;
                }
                else
                {
                    Logger.Log(LogLevel.Error, $"Slash command failed on {platform}: {result?.Message}");
                    await Alert(failureMessage);
                }
            }
            return succeededAnywhere ? SlashCommandResultEnum.Handled : SlashCommandResultEnum.Rejected;
        }

        /// <summary>
        /// Returns everything after the first <paramref name="tokenCount"/> whitespace-separated tokens of the
        /// original text, or null when there is nothing left. Unlike re-joining the split arguments this keeps
        /// the free-text payload (a stream title, an announcement, a whisper) exactly as the user typed it.
        /// </summary>
        private static string TextAfter(string trimmed, int tokenCount)
        {
            int index = 0;
            for (int token = 0; token < tokenCount; token++)
            {
                while (index < trimmed.Length && trimmed[index] == ' ') { index++; }
                while (index < trimmed.Length && trimmed[index] != ' ') { index++; }
            }

            string remainder = index < trimmed.Length ? trimmed.Substring(index).Trim() : string.Empty;
            return remainder.Length > 0 ? remainder : null;
        }

        private static async Task<SlashCommandResultEnum> Usage(string usage)
        {
            return await Reject(string.Format(MixItUp.Base.Resources.ChatCommandInvalidUsage, usage));
        }

        private static async Task<SlashCommandResultEnum> Reject(string message)
        {
            await Alert(message);
            return SlashCommandResultEnum.Rejected;
        }

        private static async Task Alert(string message)
        {
            await ServiceManager.Get<ChatService>().AddMessage(new AlertChatMessageViewModel(message));
        }
    }
}
