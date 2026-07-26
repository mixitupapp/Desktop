using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using ModelContextProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.Tools
{
    /// <summary>
    /// Shared argument validation and audit logging for the MCP tools. Every validation failure here
    /// is something the calling agent can correct on its own, so the messages say what to do rather
    /// than just what broke.
    /// </summary>
    /// <remarks>
    /// Consent for the state-changing tools is deliberately left to the client, which is where the
    /// MCP specification puts it. An earlier revision enforced it server-side with elicitation, which
    /// works against the Claude Code CLI but is auto-declined by clients that advertise form-mode
    /// support without implementing a UI for it, leaving those tools permanently unusable. The
    /// annotations plus this audit trail carry that responsibility instead.
    /// </remarks>
    internal static class ToolHelpers
    {
        public static void RequireSettingsLoaded()
        {
            if (ChannelSession.Settings == null)
            {
                throw new McpException("No settings profile is loaded yet. Call get_status and wait until isSettingsLoaded is true.");
            }
        }

        public static Guid ParseID(string id, string parameterName)
        {
            if (!Guid.TryParse(id, out Guid result))
            {
                throw new McpException($"The {parameterName} value '{id}' is not a valid GUID.");
            }
            return result;
        }

        public static StreamingPlatformTypeEnum ParsePlatform(string platform)
        {
            if (string.IsNullOrEmpty(platform))
            {
                return StreamingPlatformTypeEnum.All;
            }

            // Enum.TryParse alone accepts every member, including the obsolete Mixer / Trovo /
            // Glimesh / Facebook entries plus None and Mock. Those parse cleanly and then fail
            // confusingly further down, so gate on the supported set instead.
            if (!Enum.TryParse<StreamingPlatformTypeEnum>(platform, ignoreCase: true, out StreamingPlatformTypeEnum result)
                || (result != StreamingPlatformTypeEnum.All && !StreamingPlatforms.IsValidPlatform(result)))
            {
                throw new McpException($"Unknown or unsupported platform '{platform}'. Valid values are: {string.Join(", ", StreamingPlatforms.SupportedPlatforms)}, or All.");
            }

            return result;
        }

        public static void RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new McpException($"{parameterName} must be greater than zero.");
            }
        }

        public static void RequirePageSize(int pageSize)
        {
            if (pageSize < 1 || pageSize > 200)
            {
                throw new McpException("pageSize must be between 1 and 200.");
            }
        }

        public static void RequireSkip(int skip)
        {
            if (skip < 0)
            {
                throw new McpException("skip must be zero or greater.");
            }
        }

        #region Settings Collection Snapshots

        // Settings.Currency, Settings.Inventory and Settings.Counters are plain Dictionary instances,
        // unlike Settings.Commands / Settings.Users / Settings.Quotes which are Database* types built on
        // LockedDictionary. The application mutates them from its own threads while these tools read them
        // from Kestrel request threads, so enumerating one directly can throw "Collection was modified".
        //
        // These helpers take a defensive copy and retry once, which is the strongest guarantee available
        // without changing the shipping collection types. A torn read is still possible in principle, but
        // it degrades to a stale snapshot rather than an exception, and every tool that resolves an entry
        // by ID re-reads the live dictionary through TryGetValue rather than trusting the copy.

        private static Dictionary<TKey, TValue> Snapshot<TKey, TValue>(Func<Dictionary<TKey, TValue>> source, string collectionName)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return new Dictionary<TKey, TValue>(source());
                }
                catch (InvalidOperationException) when (attempt < 2)
                {
                    // The application changed the collection mid-copy. Yield and take it again.
                    Thread.Sleep(25);
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Log(ex);
                    throw new McpException($"The {collectionName} collection was being modified by Mix It Up and could not be read. Retry the call.");
                }
            }
        }

        public static Dictionary<Guid, CurrencyModel> SnapshotCurrencies()
        {
            RequireSettingsLoaded();
            return Snapshot(() => ChannelSession.Settings.Currency, "currency");
        }

        public static Dictionary<Guid, InventoryModel> SnapshotInventories()
        {
            RequireSettingsLoaded();
            return Snapshot(() => ChannelSession.Settings.Inventory, "inventory");
        }

        public static Dictionary<string, CounterModel> SnapshotCounters()
        {
            RequireSettingsLoaded();
            return Snapshot(() => ChannelSession.Settings.Counters, "counters");
        }

        #endregion Settings Collection Snapshots

        #region Entity Resolution

        public static CurrencyModel GetCurrencyOrThrow(string currencyId)
        {
            RequireSettingsLoaded();
            Guid id = ParseID(currencyId, nameof(currencyId));
            if (!ChannelSession.Settings.Currency.TryGetValue(id, out CurrencyModel currency) || currency == null)
            {
                throw new McpException($"No currency found with ID '{currencyId}'. Call list_currencies to see valid IDs.");
            }
            return currency;
        }

        public static InventoryModel GetInventoryOrThrow(string inventoryId)
        {
            RequireSettingsLoaded();
            Guid id = ParseID(inventoryId, nameof(inventoryId));
            if (!ChannelSession.Settings.Inventory.TryGetValue(id, out InventoryModel inventory) || inventory == null)
            {
                throw new McpException($"No inventory found with ID '{inventoryId}'. Call list_inventories to see valid IDs.");
            }
            return inventory;
        }

        public static InventoryItemModel GetInventoryItemOrThrow(InventoryModel inventory, string itemId)
        {
            Guid id = ParseID(itemId, nameof(itemId));
            InventoryItemModel item = inventory.GetItem(id);
            if (item == null)
            {
                throw new McpException($"No item found with ID '{itemId}' in inventory '{inventory.Name}'. Call list_inventories to see the items each inventory holds.");
            }
            return item;
        }

        public static CounterModel GetCounterOrThrow(string counterName)
        {
            RequireSettingsLoaded();
            if (string.IsNullOrWhiteSpace(counterName))
            {
                throw new McpException("counterName cannot be empty.");
            }
            if (!ChannelSession.Settings.Counters.TryGetValue(counterName.ToLower(), out CounterModel counter) || counter == null)
            {
                throw new McpException($"No counter named '{counterName}' exists. Call list_counters to see valid names.");
            }
            return counter;
        }

        /// <summary>
        /// Resolves the single viewer a tool is to act on, accepting either the internal GUID that the
        /// v2 Developer API uses or the platform username that v1 uses.
        /// </summary>
        /// <remarks>
        /// This is the one place a tool takes alternative inputs for the same argument. It is alternative
        /// addressing of one target rather than an operation selector: dropping username addressing would
        /// lose a capability v1 has, and requiring the caller to pre-resolve a GUID would make every
        /// username-based workflow a two-call sequence.
        /// </remarks>
        public static async Task<UserV2Model> ResolveUser(string userId, string platform, string username)
        {
            RequireSettingsLoaded();

            bool hasUserId = !string.IsNullOrWhiteSpace(userId);
            bool hasUsername = !string.IsNullOrWhiteSpace(username);

            if (hasUserId == hasUsername)
            {
                throw new McpException(hasUserId
                    ? "Specify either userId or username, not both."
                    : "Either userId or username is required. Use list_active_users or get_user to find a viewer.");
            }

            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (hasUserId)
            {
                Guid id = ParseID(userId, nameof(userId));
                if (!ChannelSession.Settings.Users.TryGetValue(id, out UserV2Model user) || user == null)
                {
                    throw new McpException($"No user found with ID '{userId}'. Call list_users or get_user to find a valid ID.");
                }
                return user;
            }

            StreamingPlatformTypeEnum platformType = string.IsNullOrWhiteSpace(platform)
                ? ChannelSession.Settings.DefaultStreamingPlatform
                : ParsePlatform(platform);

            UserV2ViewModel found = await ServiceManager.Get<UserService>().GetUserByPlatform(
                platformType,
                platformID: username,
                platformUsername: username,
                performPlatformSearch: true);

            if (found == null || !ChannelSession.Settings.Users.TryGetValue(found.ID, out UserV2Model resolved) || resolved == null)
            {
                throw new McpException($"No user '{username}' found on platform '{platformType}'.");
            }

            return resolved;
        }

        /// <summary>
        /// The name to show for a stored user record in tool output. Users can exist on several
        /// platforms, so this is the first known username rather than an authoritative identity.
        /// </summary>
        public static string GetDisplayUsername(UserV2Model user)
        {
            return user.GetAllPlatformUsernames().FirstOrDefault() ?? string.Empty;
        }

        #endregion Entity Resolution

        /// <summary>
        /// Records every state-changing tool call. ForceLog rather than Log because the default log
        /// level drops Information, and an audit trail that only exists on verbose builds is not one.
        /// </summary>
        public static void LogToolAction(string toolName, string detail)
        {
            Logger.ForceLog(LogLevel.Information, $"MCP tool '{toolName}' invoked: {detail}");
        }

        /// <summary>
        /// The ceiling on how long a single tool call may wait before it gives up and reports back.
        /// Generous enough for the slowest legitimate work, which is get_user performing a live
        /// platform lookup on top of loading user data.
        /// </summary>
        public static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Bounds how long a tool call can wait for the application to answer.
        /// </summary>
        /// <remarks>
        /// This is not here to bound command execution, which is already asynchronous: CommandService
        /// enqueues onto a background runner and returns as soon as the work is accepted, so a command
        /// that legitimately runs for half a minute never holds a request thread.
        /// <para>
        /// It is here because several of these paths end up inside ThreadSafeObservableCollection,
        /// which marshals through DispatcherHelper.Dispatcher.Invoke with no timeout of its own. If the
        /// UI thread is wedged, behind a modal dialog for instance, those calls block indefinitely, and
        /// a hung tool call is indistinguishable to the caller from a hung application.
        /// </para>
        /// <para>
        /// The abandoned work is deliberately not cancelled. The blocking call is a synchronous
        /// dispatcher Invoke inside shipping code with no cancellation token to pass it, so the thread
        /// stays parked until the UI frees up. What this buys is a caller that gets a diagnosable
        /// answer rather than waiting forever.
        /// </para>
        /// </remarks>
        public static async Task<T> RunWithTimeout<T>(string toolName, Func<Task<T>> work)
        {
            Task<T> task = Task.Run(work);

            using (CancellationTokenSource timeoutCancellation = new CancellationTokenSource())
            {
                Task timeout = Task.Delay(ToolTimeout, timeoutCancellation.Token);
                if (await Task.WhenAny(task, timeout) != task)
                {
                    Logger.ForceLog(LogLevel.Warning, $"MCP tool '{toolName}' exceeded {ToolTimeout.TotalSeconds:0}s and was abandoned. The UI thread may be blocked.");
                    throw new McpException($"The '{toolName}' tool did not respond within {ToolTimeout.TotalSeconds:0} seconds and was abandoned. This usually means the Mix It Up window is blocked, for example by an open dialog. The operation may still complete on its own, so re-read state before retrying.");
                }

                timeoutCancellation.Cancel();
            }

            // Awaiting the completed task rather than returning its Result so that any exception it
            // raised, including the McpException validation failures, surfaces unwrapped.
            return await task;
        }
    }
}
