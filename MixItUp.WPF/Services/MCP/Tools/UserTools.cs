using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.User;
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
    public class UserTools
    {
        // OpenWorld because performPlatformSearch reaches the live platform API when the user is
        // not already known locally.
        [McpServerTool(Name = "get_user", ReadOnly = true, OpenWorld = true, UseStructuredContent = true)]
        [Description("Look up a viewer by their username or platform ID on a specific platform. Returns their stored watch time, currency balances, inventory holdings and per-platform identities. If the viewer is not already known locally, the platform is searched for them.")]
        public static Task<UserResult> GetUser(
            [Description("The platform to search on, for example Twitch, YouTube, Kick, or Velora.")] string platform,
            [Description("The username or platform ID of the viewer.")] string usernameOrId)
        {
            return ToolHelpers.RunWithTimeout("get_user", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                if (string.IsNullOrWhiteSpace(platform))
                {
                    throw new McpException("platform is required. Call get_status to see which platforms are connected.");
                }
                if (string.IsNullOrWhiteSpace(usernameOrId))
                {
                    throw new McpException("usernameOrId cannot be empty.");
                }

                StreamingPlatformTypeEnum platformType = ToolHelpers.ParsePlatform(platform);

                await ServiceManager.Get<UserService>().LoadAllUserData();

                UserV2ViewModel found = await ServiceManager.Get<UserService>().GetUserByPlatform(
                    platformType,
                    platformID: usernameOrId,
                    platformUsername: usernameOrId,
                    performPlatformSearch: true);

                if (found == null || !ChannelSession.Settings.Users.TryGetValue(found.ID, out UserV2Model user) || user == null)
                {
                    throw new McpException($"No user '{usernameOrId}' found on platform '{platformType}'.");
                }

                return UserResult.From(user);
            });
        }

        [McpServerTool(Name = "get_user_by_id", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get a viewer by their internal Mix It Up GUID. Unlike get_user this never contacts a streaming platform; it only reads the local user table. Use list_users, list_active_users or get_user to discover IDs.")]
        public static Task<UserResult> GetUserById(
            [Description("The internal Mix It Up GUID of the viewer.")] string userId)
        {
            return ToolHelpers.RunWithTimeout("get_user_by_id", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                await ServiceManager.Get<UserService>().LoadAllUserData();

                Guid id = ToolHelpers.ParseID(userId, nameof(userId));
                if (!ChannelSession.Settings.Users.TryGetValue(id, out UserV2Model user) || user == null)
                {
                    throw new McpException($"No user found with ID '{userId}'. Call list_users to see valid IDs.");
                }

                return UserResult.From(user);
            });
        }

        [McpServerTool(Name = "list_users", ReadOnly = true, UseStructuredContent = true)]
        [Description("Page through every viewer Mix It Up has ever recorded, including viewers who are not currently in chat. This table is large on an established channel, so page rather than trying to read it all at once. For only the viewers present right now, use list_active_users.")]
        public static Task<ListUsersResult> ListUsers(
            [Description("Number of viewers to skip, for paging. Defaults to 0.")] int skip = 0,
            [Description("Maximum number of viewers to return. Defaults to 25, maximum 200.")] int pageSize = 25)
        {
            return ToolHelpers.RunWithTimeout("list_users", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();
                ToolHelpers.RequireSkip(skip);
                ToolHelpers.RequirePageSize(pageSize);

                await ServiceManager.Get<UserService>().LoadAllUserData();

                List<UserV2Model> all = ChannelSession.Settings.Users.Values.ToList();

                return new ListUsersResult()
                {
                    TotalCount = all.Count,
                    Users = all.OrderBy(u => u.ID).Skip(skip).Take(pageSize).Select(UserSummaryResult.From).ToList(),
                };
            });
        }

        [McpServerTool(Name = "list_active_users", ReadOnly = true, UseStructuredContent = true)]
        [Description("List the viewers Mix It Up currently considers active in chat. A viewer becomes active when they join or speak and drops off when they leave, so this is legitimately empty when nobody has been seen since the app started. Useful for picking a real viewer to act on.")]
        public static Task<ListActiveUsersResult> ListActiveUsers(
            [Description("Platform to restrict the list to, for example Twitch. Defaults to all connected platforms.")] string platform = null,
            [Description("Number of viewers to skip, for paging. Defaults to 0.")] int skip = 0,
            [Description("Maximum number of viewers to return. Defaults to 25, maximum 200.")] int pageSize = 25)
        {
            return ToolHelpers.RunWithTimeout("list_active_users", () =>
            {
                ToolHelpers.RequireSettingsLoaded();
                ToolHelpers.RequireSkip(skip);
                ToolHelpers.RequirePageSize(pageSize);

                StreamingPlatformTypeEnum platformType = ToolHelpers.ParsePlatform(platform);

                List<UserV2ViewModel> active = ServiceManager.Get<UserService>().GetActiveUsers(platformType).ToList();

                return Task.FromResult(new ListActiveUsersResult()
                {
                    // Counted from the flattened set rather than UserService.GetActiveUserCount(),
                    // which returns the number of platforms holding active users rather than the
                    // number of users.
                    TotalCount = active.Count,
                    Users = active.OrderBy(u => u.ID).Skip(skip).Take(pageSize).Select(u => new ActiveUserResult()
                    {
                        ID = u.ID.ToString(),
                        Username = u.Username,
                        DisplayName = u.DisplayName,
                        Platform = u.Platform.ToString(),
                        OnlineViewingMinutes = u.OnlineViewingMinutes,
                    }).ToList(),
                });
            });
        }

        [McpServerTool(Name = "get_users_bulk", ReadOnly = true, OpenWorld = true, UseStructuredContent = true)]
        [Description("Look up several viewers in one call by username or platform ID. Names that cannot be resolved are reported in NotFound rather than failing the call. Use this instead of calling get_user in a loop.")]
        public static Task<GetUsersBulkResult> GetUsersBulk(
            [Description("The usernames or platform IDs to look up.")] string[] usernamesOrIds,
            [Description("Platform to resolve the names on, for example Twitch. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("get_users_bulk", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                if (usernamesOrIds == null || usernamesOrIds.Length == 0)
                {
                    throw new McpException("usernamesOrIds cannot be empty.");
                }

                await ServiceManager.Get<UserService>().LoadAllUserData();

                StreamingPlatformTypeEnum platformType = string.IsNullOrWhiteSpace(platform)
                    ? ChannelSession.Settings.DefaultStreamingPlatform
                    : ToolHelpers.ParsePlatform(platform);

                GetUsersBulkResult result = new GetUsersBulkResult();

                foreach (string usernameOrId in usernamesOrIds)
                {
                    if (string.IsNullOrWhiteSpace(usernameOrId))
                    {
                        continue;
                    }

                    // Matches the v1 Developer API, which accepts an internal GUID here as well as a
                    // platform name.
                    UserV2ViewModel found = Guid.TryParse(usernameOrId, out Guid parsedId)
                        ? await ServiceManager.Get<UserService>().GetUserByID(platformType, parsedId)
                        : await ServiceManager.Get<UserService>().GetUserByPlatform(platformType, platformID: usernameOrId, platformUsername: usernameOrId, performPlatformSearch: true);

                    if (found == null || !ChannelSession.Settings.Users.TryGetValue(found.ID, out UserV2Model user) || user == null)
                    {
                        result.NotFound.Add(usernameOrId);
                        continue;
                    }

                    result.Users.Add(UserResult.From(user));
                }

                return result;
            });
        }

        // Additive: this records a viewer Mix It Up did not have, it does not change an existing one.
        // OpenWorld because the platform is searched when the viewer is not already known.
        [McpServerTool(Name = "add_user", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true, UseStructuredContent = true)]
        [Description("Add a viewer to the local user table by looking them up on a streaming platform. Fails if the platform does not know that username. If the viewer already exists locally their existing record is returned unchanged.")]
        public static Task<UserResult> AddUser(
            [Description("The platform to look the viewer up on, for example Twitch.")] string platform,
            [Description("The viewer's username on that platform.")] string username)
        {
            return ToolHelpers.RunWithTimeout("add_user", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                if (string.IsNullOrWhiteSpace(platform))
                {
                    throw new McpException("platform is required. Call get_status to see which platforms are connected.");
                }
                if (string.IsNullOrWhiteSpace(username))
                {
                    throw new McpException("username cannot be empty.");
                }

                StreamingPlatformTypeEnum platformType = ToolHelpers.ParsePlatform(platform);

                UserV2ViewModel found = await ServiceManager.Get<UserService>().GetUserByPlatform(
                    platformType, platformUsername: username, performPlatformSearch: true);

                if (found == null || !ChannelSession.Settings.Users.TryGetValue(found.ID, out UserV2Model user) || user == null)
                {
                    throw new McpException($"No user '{username}' found on platform '{platformType}'. The platform has to know the account before it can be added.");
                }

                ToolHelpers.LogToolAction("add_user", $"added or resolved '{username}' on {platformType} as {user.ID}");

                return UserResult.From(user);
            });
        }

        [McpServerTool(Name = "set_user_watch_time", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Set a viewer's total watch time to an exact number of minutes, discarding the recorded value. WARNING: this overwrites real tracked history for a real viewer and cannot be undone. Watch time drives rank progression and the watch time leaderboard.")]
        public static Task<UserResult> SetUserWatchTime(
            [Description("The total watch time in minutes to record.")] int minutes,
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("set_user_watch_time", async () =>
            {
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                if (minutes < 0)
                {
                    throw new McpException("minutes cannot be negative.");
                }

                int previous = user.OnlineViewingMinutes;

                // Set through the view model so the record is marked dirty and actually persisted.
                new UserV2ViewModel(user).OnlineViewingMinutes = minutes;

                ToolHelpers.LogToolAction("set_user_watch_time", $"user {user.ID} watch time set from {previous} to {minutes} minutes");

                return UserResult.From(user);
            });
        }

        [McpServerTool(Name = "delete_user", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Permanently delete a viewer's stored record. WARNING: this destroys their watch time, every currency balance, every inventory item and every stream pass they hold, for a real viewer, and cannot be undone. Nothing on the streaming platform changes; only Mix It Up's record of them is erased.")]
        public static Task<ActionResult> DeleteUser(
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("delete_user", async () =>
            {
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                Guid id = user.ID;
                string name = ToolHelpers.GetDisplayUsername(user);

                ServiceManager.Get<UserService>().DeleteUserData(id);

                ToolHelpers.LogToolAction("delete_user", $"deleted user {id} ('{name}')");

                return new ActionResult($"Deleted user '{name}' ({id}).");
            });
        }

        public class ListUsersResult
        {
            [Description("Total number of viewers on record, before paging.")]
            public int TotalCount { get; set; }

            [Description("The page of viewers requested.")]
            public List<UserSummaryResult> Users { get; set; } = new List<UserSummaryResult>();
        }

        public class GetUsersBulkResult
        {
            [Description("The viewers that were resolved.")]
            public List<UserResult> Users { get; set; } = new List<UserResult>();

            [Description("Names that could not be resolved to a viewer.")]
            public List<string> NotFound { get; set; } = new List<string>();
        }

        public class ListActiveUsersResult
        {
            [Description("Total number of active viewers, before paging.")]
            public int TotalCount { get; set; }

            [Description("The page of active viewers requested.")]
            public List<ActiveUserResult> Users { get; set; } = new List<ActiveUserResult>();
        }

        public class ActiveUserResult
        {
            [Description("The internal Mix It Up GUID of the viewer.")]
            public string ID { get; set; }

            [Description("The viewer's username on the platform they are active on.")]
            public string Username { get; set; }

            [Description("The viewer's display name.")]
            public string DisplayName { get; set; }

            [Description("The platform the viewer is active on.")]
            public string Platform { get; set; }

            [Description("Total minutes the viewer has been seen online.")]
            public int OnlineViewingMinutes { get; set; }
        }
    }
}
