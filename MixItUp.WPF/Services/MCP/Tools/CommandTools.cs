using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.Tools
{
    [McpServerToolType]
    public class CommandTools
    {
        [McpServerTool(Name = "list_commands", ReadOnly = true, UseStructuredContent = true)]
        [Description("List the commands configured in Mix It Up, newest page first. Returns command IDs needed by get_command, run_command, enable_command, disable_command and toggle_command. Use nameFilter to narrow the list instead of paging through everything.")]
        public static Task<ListCommandsResult> ListCommands(
            [Description("Case-insensitive substring to match against the command name. Omit to list all commands.")] string nameFilter = null,
            [Description("Number of commands to skip, for paging. Defaults to 0.")] int skip = 0,
            [Description("Maximum number of commands to return. Defaults to 25, maximum 200.")] int pageSize = 25)
        {
            return ToolHelpers.RunWithTimeout("list_commands", () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                ToolHelpers.RequireSkip(skip);
                ToolHelpers.RequirePageSize(pageSize);

                IEnumerable<CommandModelBase> commands = GetUserFacingCommands();

                if (!string.IsNullOrEmpty(nameFilter))
                {
                    commands = commands.Where(c => c.Name != null && c.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                List<CommandModelBase> matched = commands.ToList();

                return Task.FromResult(new ListCommandsResult()
                {
                    TotalCount = matched.Count,
                    Commands = matched.OrderBy(c => c.Name).Skip(skip).Take(pageSize).Select(ToResult).ToList(),
                });
            });
        }

        [McpServerTool(Name = "get_command", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get a single command by its ID. Use list_commands to find the ID.")]
        public static Task<CommandResult> GetCommand(
            [Description("The GUID of the command.")] string commandId)
        {
            return ToolHelpers.RunWithTimeout("get_command", () =>
            {
                ToolHelpers.RequireSettingsLoaded();
                return Task.FromResult(ToResult(GetCommandOrThrow(commandId)));
            });
        }

        [McpServerTool(Name = "run_command", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true)]
        [Description("Queue a command to run, exactly as if it had been triggered in chat. WARNING: this produces the command's real side effects, which for most commands means publicly visible chat messages, overlay changes, or currency awards on a live channel. The command is queued onto a background runner, so this returns as soon as it is accepted rather than waiting for it to finish.")]
        public static Task<ActionResult> RunCommand(
            [Description("The GUID of the command to run.")] string commandId,
            [Description("Arguments to pass to the command, as they would be typed after the trigger in chat.")] string arguments = null,
            [Description("Platform to run the command against, for example Twitch. Defaults to all connected platforms.")] string platform = null,
            [Description("Skip the command's requirements such as cooldowns, role restrictions, and currency costs. Defaults to false.")] bool ignoreRequirements = false)
        {
            return ToolHelpers.RunWithTimeout("run_command", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                CommandModelBase command = GetCommandOrThrow(commandId);
                StreamingPlatformTypeEnum platformType = ToolHelpers.ParsePlatform(platform);

                CommandParametersModel parameters = new CommandParametersModel(
                    platform: platformType,
                    arguments: CommandParametersModel.GenerateArguments(arguments))
                {
                    IgnoreRequirements = ignoreRequirements,
                };

                await ServiceManager.Get<CommandService>().Queue(command.ID, parameters);

                ToolHelpers.LogToolAction("run_command", $"queued '{command.Name}' ({command.ID}) on {platformType}, ignoreRequirements={ignoreRequirements}");

                return new ActionResult($"Queued command '{command.Name}'.");
            });
        }

        // Enable, disable and toggle are three tools rather than one tool with a state argument.
        // An MCP client grants permission per tool, so a single combined tool would make "let the
        // agent turn commands off but not on" impossible to express.
        [McpServerTool(Name = "enable_command", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Enable a command so it can be triggered. If the command is already enabled this changes nothing. A re-enabled chat command becomes usable by viewers again immediately.")]
        public static Task<CommandResult> EnableCommand(
            [Description("The GUID of the command. Call list_commands to find it.")] string commandId)
        {
            return SetCommandEnabled("enable_command", commandId, command => true);
        }

        [McpServerTool(Name = "disable_command", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Disable a command so it can no longer be triggered. If the command is already disabled this changes nothing. WARNING: a disabled chat command stops working for viewers on the live channel until it is enabled again.")]
        public static Task<CommandResult> DisableCommand(
            [Description("The GUID of the command. Call list_commands to find it.")] string commandId)
        {
            return SetCommandEnabled("disable_command", commandId, command => false);
        }

        [McpServerTool(Name = "toggle_command", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true)]
        [Description("Flip a command between enabled and disabled. WARNING: this changes whether viewers on the live channel can use the command. Call get_command first if you need to know which way it will go.")]
        public static Task<CommandResult> ToggleCommand(
            [Description("The GUID of the command. Call list_commands to find it.")] string commandId)
        {
            return SetCommandEnabled("toggle_command", commandId, command => !command.IsEnabled);
        }

        private static Task<CommandResult> SetCommandEnabled(string toolName, string commandId, Func<CommandModelBase, bool> newState)
        {
            return ToolHelpers.RunWithTimeout(toolName, async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                CommandModelBase command = GetCommandOrThrow(commandId);

                command.IsEnabled = newState(command);

                // Trigger tables and timer groups are built from the enabled set, so they have to be
                // rebuilt for the change to actually take effect.
                if (command is ChatCommandModel)
                {
                    ServiceManager.Get<ChatService>().RebuildCommandTriggers();
                }
                else if (command is TimerCommandModel)
                {
                    await ServiceManager.Get<TimerService>().RebuildTimerGroups();
                }

                ToolHelpers.LogToolAction(toolName, $"'{command.Name}' ({command.ID}) isEnabled={command.IsEnabled}");

                return ToResult(command);
            });
        }

        private static CommandModelBase GetCommandOrThrow(string commandId)
        {
            Guid id = ToolHelpers.ParseID(commandId, nameof(commandId));
            if (!ChannelSession.Settings.Commands.TryGetValue(id, out CommandModelBase command) || command == null)
            {
                throw new McpException($"No command found with ID '{commandId}'. Call list_commands to see valid IDs.");
            }
            return command;
        }

        private static IEnumerable<CommandModelBase> GetUserFacingCommands()
        {
            return new List<CommandModelBase>(ServiceManager.Get<CommandService>().AllCommands)
                .Where(c => !c.IsEmbedded && c.Type != CommandTypeEnum.Custom);
        }

        private static CommandResult ToResult(CommandModelBase command)
        {
            return new CommandResult()
            {
                ID = command.ID.ToString(),
                Name = command.Name,
                Type = command.Type.ToString(),
                IsEnabled = command.IsEnabled,
                Unlocked = command.Unlocked,
                GroupName = command.GroupName,
            };
        }

        public class ListCommandsResult
        {
            [Description("Total number of commands matching the filter, before paging.")]
            public int TotalCount { get; set; }

            [Description("The page of commands requested.")]
            public List<CommandResult> Commands { get; set; } = new List<CommandResult>();
        }

        public class CommandResult
        {
            [Description("The GUID of the command, used by the other command tools.")]
            public string ID { get; set; }

            [Description("The display name of the command.")]
            public string Name { get; set; }

            [Description("The command type, for example Chat, Event, or Timer.")]
            public string Type { get; set; }

            [Description("Whether the command is currently enabled.")]
            public bool IsEnabled { get; set; }

            [Description("Whether the command bypasses requirement checks.")]
            public bool Unlocked { get; set; }

            [Description("The group the command belongs to, or null if ungrouped.")]
            public string GroupName { get; set; }
        }
    }
}
