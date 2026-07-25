using MixItUp.Base.Model;
using MixItUp.Base.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.Tools
{
    [McpServerToolType]
    public class ChatTools
    {
        // Additive rather than destructive: this creates a new message, it does not modify or
        // remove existing state. OpenWorld because it leaves the app for the platform's chat.
        [McpServerTool(Name = "send_chat_message", Destructive = false, OpenWorld = true)]
        [Description("Send a message to the live chat of one or all connected platforms. This is publicly visible to everyone watching the stream and cannot be unsent.")]
        public static Task<string> SendChatMessage(
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

                return $"Sent message to {platformType}.";
            });
        }

        [McpServerTool(Name = "clear_chat", Destructive = true, Idempotent = true, OpenWorld = true)]
        [Description("Clear the chat history for one or all connected platforms. This clears chat for viewers, not just the local display, and is a real moderation action on the channel.")]
        public static Task<string> ClearChat(
            [Description("Platform to clear, for example Twitch. Defaults to all connected platforms.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("clear_chat", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                StreamingPlatformTypeEnum platformType = ToolHelpers.ParsePlatform(platform);

                await ServiceManager.Get<ChatService>().ClearMessages(platformType);

                ToolHelpers.LogToolAction("clear_chat", $"cleared chat on {platformType}");

                return $"Cleared chat for {platformType}.";
            });
        }
    }
}
