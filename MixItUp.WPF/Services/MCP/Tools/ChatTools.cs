using MixItUp.Base.Model;
using MixItUp.Base.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.Tools
{
    [McpServerToolType]
    public class ChatTools
    {
        // Additive rather than destructive: this creates a new message, it does not modify or
        // remove existing state. OpenWorld because it leaves the app for the platform's chat.
        [McpServerTool(Name = "send_chat_message", ReadOnly = false, Destructive = false, OpenWorld = true, UseStructuredContent = true)]
        [Description("Send a message to the live chat of one or all connected platforms. WARNING: this is publicly visible to everyone watching the stream and cannot be unsent.")]
        public static Task<ActionResult> SendChatMessage(
            [Description("The message text to send.")] string message,
            [Description("Platform to send to, for example Twitch. Defaults to all connected platforms.")] string platform = null,
            [Description("Send as the streamer account rather than the bot account. Defaults to false.")] bool sendAsStreamer = false)
        {
            return ToolHelpers.RunWithTimeout("send_chat_message", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                if (string.IsNullOrWhiteSpace(message))
                {
                    throw new McpException("message cannot be empty.");
                }

                StreamingPlatformTypeEnum platformType = ToolHelpers.ParsePlatform(platform);
                string account = sendAsStreamer ? "streamer" : "bot";

                await ServiceManager.Get<ChatService>().SendMessage(message, platformType, sendAsStreamer);

                ToolHelpers.LogToolAction("send_chat_message", $"sent to {platformType} as {account}: \"{message}\"");

                return new ActionResult($"Sent message to {platformType}.");
            });
        }

        // Additive for the same reason as send_chat_message: a whisper is a new message.
        [McpServerTool(Name = "send_whisper", ReadOnly = false, Destructive = false, OpenWorld = true, UseStructuredContent = true)]
        [Description("Send a private whisper to one viewer. WARNING: this reaches a real viewer on a live channel and cannot be unsent. Only Twitch delivers whispers: on every other platform this call succeeds but nothing is sent, because those platforms have no whisper mechanism.")]
        public static Task<ActionResult> SendWhisper(
            [Description("The username of the viewer to whisper.")] string username,
            [Description("The message text to send.")] string message,
            [Description("Platform to resolve the username on, for example Twitch. Defaults to all connected platforms.")] string platform = null,
            [Description("Send as the streamer account rather than the bot account. Defaults to false.")] bool sendAsStreamer = false)
        {
            return ToolHelpers.RunWithTimeout("send_whisper", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                if (string.IsNullOrWhiteSpace(username))
                {
                    throw new McpException("username cannot be empty.");
                }
                if (string.IsNullOrWhiteSpace(message))
                {
                    throw new McpException("message cannot be empty.");
                }

                StreamingPlatformTypeEnum platformType = ToolHelpers.ParsePlatform(platform);
                string account = sendAsStreamer ? "streamer" : "bot";

                await ServiceManager.Get<ChatService>().Whisper(username, platformType, message, sendAsStreamer);

                ToolHelpers.LogToolAction("send_whisper", $"whispered {username} on {platformType} as {account}: \"{message}\"");

                return new ActionResult($"Sent whisper to '{username}' on {platformType}.");
            });
        }

        [McpServerTool(Name = "clear_chat", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true, UseStructuredContent = true)]
        [Description("Clear the chat history for one or all connected platforms. WARNING: on Twitch, Velora and VPZone this is a real moderation action that clears chat for every viewer and cannot be undone. On YouTube and Kick only the local Mix It Up chat display is cleared, because those platforms provide no clear-chat API, and viewers there keep seeing the full history.")]
        public static Task<ActionResult> ClearChat(
            [Description("Platform to clear, for example Twitch. Defaults to all connected platforms.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("clear_chat", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                StreamingPlatformTypeEnum platformType = ToolHelpers.ParsePlatform(platform);

                // ChatService.ClearMessages reaches a platform API only for Twitch, Velora and
                // VPZone, and only while that session is connected; every other case clears the local
                // display alone. Work out which of those actually applied before clearing, so the
                // result never tells a caller that viewers saw a clear which never left the app.
                List<string> clearedForViewers = new List<string>();
                foreach (StreamingPlatformTypeEnum viewerFacing in new[] { StreamingPlatformTypeEnum.Twitch, StreamingPlatformTypeEnum.Velora, StreamingPlatformTypeEnum.VPZone })
                {
                    if ((platformType == StreamingPlatformTypeEnum.All || platformType == viewerFacing)
                        && StreamingPlatforms.IsPlatformConnected(viewerFacing))
                    {
                        clearedForViewers.Add(viewerFacing.ToString());
                    }
                }

                await ServiceManager.Get<ChatService>().ClearMessages(platformType);

                ToolHelpers.LogToolAction("clear_chat", $"cleared chat on {platformType}");

                string outcome = clearedForViewers.Count > 0
                    ? $"Cleared for every viewer on {string.Join(" and ", clearedForViewers)}, and cleared in the Mix It Up display."
                    : "Cleared in the Mix It Up display only; no platform that supports clearing chat for viewers was connected.";

                return new ActionResult($"Cleared chat for {platformType}. {outcome}");
            });
        }
    }
}
