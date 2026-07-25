using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.User;
using ModelContextProtocol;
using ModelContextProtocol.Server;
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
        [McpServerTool(Name = "get_user", ReadOnly = true, OpenWorld = true)]
        [Description("Look up a viewer by their username or platform ID on a specific platform. Returns their stored watch time, currency balances, and per-platform identities.")]
        public static Task<UserResult> GetUser(
            [Description("The platform to search on, for example Twitch, YouTube, Trovo, or Kick.")] string platform,
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

                return ToResult(user);
            });
        }

        [McpServerTool(Name = "list_active_users", ReadOnly = true)]
        [Description("List the viewers Mix It Up currently considers active in chat. Useful for picking a real user to test a command against.")]
        public static Task<ListActiveUsersResult> ListActiveUsers(
            [Description("Maximum number of users to return. Defaults to 25, maximum 200.")] int pageSize = 25)
        {
            return ToolHelpers.RunWithTimeout("list_active_users", () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                if (pageSize < 1 || pageSize > 200)
                {
                    throw new McpException("pageSize must be between 1 and 200.");
                }

                List<UserV2ViewModel> active = ServiceManager.Get<UserService>().GetActiveUsers().ToList();

                return Task.FromResult(new ListActiveUsersResult()
                {
                    TotalCount = active.Count,
                    Users = active.Take(pageSize).Select(u => new ActiveUserResult()
                    {
                        ID = u.ID.ToString(),
                        Username = u.Username,
                        DisplayName = u.DisplayName,
                        Platform = u.Platform.ToString(),
                    }).ToList(),
                });
            });
        }

        private static UserResult ToResult(UserV2Model user)
        {
            UserResult result = new UserResult()
            {
                ID = user.ID.ToString(),
                OnlineViewingMinutes = user.OnlineViewingMinutes,
                LastActivity = user.LastActivity,
                CustomTitle = user.CustomTitle,
                Notes = user.Notes,
            };

            foreach (KeyValuePair<StreamingPlatformTypeEnum, UserPlatformV2ModelBase> kvp in user.PlatformData)
            {
                result.Platforms.Add(new UserPlatformResult()
                {
                    Platform = kvp.Key.ToString(),
                    ID = kvp.Value.ID,
                    Username = kvp.Value.Username,
                    DisplayName = kvp.Value.DisplayName,
                });
            }

            foreach (KeyValuePair<System.Guid, int> kvp in user.CurrencyAmounts)
            {
                // A user can hold an amount for a currency that has since been deleted, so the name
                // is reported separately from the ID rather than being used as the key. Keying on a
                // name that falls back to a raw GUID makes orphans indistinguishable from real ones.
                bool known = ChannelSession.Settings.Currency.TryGetValue(kvp.Key, out var currency);
                result.CurrencyAmounts.Add(new UserCurrencyResult()
                {
                    ID = kvp.Key.ToString(),
                    Name = known ? currency.Name : null,
                    Amount = kvp.Value,
                    IsOrphaned = !known,
                });
            }

            return result;
        }

        public class UserResult
        {
            [Description("The internal Mix It Up GUID for this user.")]
            public string ID { get; set; }

            [Description("Total minutes this user has been seen online.")]
            public int OnlineViewingMinutes { get; set; }

            [Description("When this user was last active.")]
            public System.DateTimeOffset LastActivity { get; set; }

            [Description("The custom title assigned to this user, if any.")]
            public string CustomTitle { get; set; }

            [Description("Streamer notes stored against this user, if any.")]
            public string Notes { get; set; }

            [Description("The identities this user has on each connected platform.")]
            public List<UserPlatformResult> Platforms { get; set; } = new List<UserPlatformResult>();

            [Description("Currency balances held by this user.")]
            public List<UserCurrencyResult> CurrencyAmounts { get; set; } = new List<UserCurrencyResult>();
        }

        public class UserCurrencyResult
        {
            [Description("The GUID of the currency.")]
            public string ID { get; set; }

            [Description("The display name of the currency, or null if the currency no longer exists.")]
            public string Name { get; set; }

            [Description("The amount this user holds.")]
            public int Amount { get; set; }

            [Description("True when the user holds an amount for a currency that has been deleted from settings.")]
            public bool IsOrphaned { get; set; }
        }

        public class UserPlatformResult
        {
            public string Platform { get; set; }

            public string ID { get; set; }

            public string Username { get; set; }

            public string DisplayName { get; set; }
        }

        public class ListActiveUsersResult
        {
            [Description("Total number of active users.")]
            public int TotalCount { get; set; }

            [Description("The page of active users requested.")]
            public List<ActiveUserResult> Users { get; set; } = new List<ActiveUserResult>();
        }

        public class ActiveUserResult
        {
            public string ID { get; set; }

            public string Username { get; set; }

            public string DisplayName { get; set; }

            public string Platform { get; set; }
        }
    }
}
