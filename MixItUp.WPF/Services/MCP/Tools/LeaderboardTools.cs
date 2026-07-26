using MixItUp.Base;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace MixItUp.WPF.Services.MCP.Tools
{
    /// <summary>
    /// Leaderboards read the full user table, so each of these forces a complete user data load
    /// before ranking. All three are read-only.
    /// </summary>
    /// <remarks>
    /// Viewers flagged as excluded from specialty tracking are left out of every leaderboard here.
    /// The v1 Developer API filters them and v2 does not; a viewer the streamer has deliberately
    /// excluded from currency and rank tracking appearing at the top of a leaderboard is the
    /// behaviour worth keeping out of, so these follow v1.
    /// </remarks>
    [McpServerToolType]
    public class LeaderboardTools
    {
        [McpServerTool(Name = "get_watchtime_leaderboard", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get the viewers with the most watch time, highest first. Viewers excluded from specialty tracking are omitted.")]
        public static Task<WatchtimeLeaderboardResult> GetWatchtimeLeaderboard(
            [Description("How many viewers to return. Defaults to 10, maximum 200.")] int count = 10)
        {
            return ToolHelpers.RunWithTimeout("get_watchtime_leaderboard", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();
                ToolHelpers.RequirePageSize(count);

                await ServiceManager.Get<UserService>().LoadAllUserData();

                List<WatchtimeEntryResult> entries = ChannelSession.Settings.Users.Values
                    .Where(u => !u.IsSpecialtyExcluded && u.OnlineViewingMinutes > 0)
                    .OrderByDescending(u => u.OnlineViewingMinutes)
                    .Take(count)
                    .Select(u => new WatchtimeEntryResult()
                    {
                        UserID = u.ID.ToString(),
                        Username = ToolHelpers.GetDisplayUsername(u),
                        OnlineViewingMinutes = u.OnlineViewingMinutes,
                    })
                    .ToList();

                return new WatchtimeLeaderboardResult() { Users = entries };
            });
        }

        [McpServerTool(Name = "get_currency_leaderboard", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get the viewers holding the most of one currency, highest first. Call list_currencies to find the currency ID. Viewers excluded from specialty tracking are omitted.")]
        public static Task<CurrencyLeaderboardResult> GetCurrencyLeaderboard(
            [Description("The GUID of the currency. Call list_currencies to find it.")] string currencyId,
            [Description("How many viewers to return. Defaults to 10, maximum 200.")] int count = 10)
        {
            return ToolHelpers.RunWithTimeout("get_currency_leaderboard", async () =>
            {
                CurrencyModel currency = ToolHelpers.GetCurrencyOrThrow(currencyId);
                ToolHelpers.RequirePageSize(count);

                await ServiceManager.Get<UserService>().LoadAllUserData();

                List<CurrencyEntryResult> entries = ChannelSession.Settings.Users.Values
                    .Where(u => !u.IsSpecialtyExcluded && u.CurrencyAmounts.ContainsKey(currency.ID) && u.CurrencyAmounts[currency.ID] > 0)
                    .OrderByDescending(u => u.CurrencyAmounts[currency.ID])
                    .Take(count)
                    .Select(u => new CurrencyEntryResult()
                    {
                        UserID = u.ID.ToString(),
                        Username = ToolHelpers.GetDisplayUsername(u),
                        Amount = u.CurrencyAmounts[currency.ID],
                    })
                    .ToList();

                return new CurrencyLeaderboardResult()
                {
                    CurrencyID = currency.ID.ToString(),
                    CurrencyName = currency.Name,
                    Users = entries,
                };
            });
        }

        [McpServerTool(Name = "get_inventory_item_leaderboard", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get the viewers holding the most of one inventory item, highest first. Call list_inventories to find the inventory and item IDs. Viewers excluded from specialty tracking are omitted.")]
        public static Task<InventoryItemLeaderboardResult> GetInventoryItemLeaderboard(
            [Description("The GUID of the inventory. Call list_inventories to find it.")] string inventoryId,
            [Description("The GUID of the item. Call list_inventories to find it.")] string itemId,
            [Description("How many viewers to return. Defaults to 10, maximum 200.")] int count = 10)
        {
            return ToolHelpers.RunWithTimeout("get_inventory_item_leaderboard", async () =>
            {
                InventoryModel inventory = ToolHelpers.GetInventoryOrThrow(inventoryId);
                InventoryItemModel item = ToolHelpers.GetInventoryItemOrThrow(inventory, itemId);
                ToolHelpers.RequirePageSize(count);

                await ServiceManager.Get<UserService>().LoadAllUserData();

                Guid inventoryID = inventory.ID;
                Guid itemID = item.ID;

                List<InventoryItemEntryResult> entries = ChannelSession.Settings.Users.Values
                    .Where(u => !u.IsSpecialtyExcluded
                        && u.InventoryAmounts.ContainsKey(inventoryID)
                        && u.InventoryAmounts[inventoryID].ContainsKey(itemID)
                        && u.InventoryAmounts[inventoryID][itemID] > 0)
                    .OrderByDescending(u => u.InventoryAmounts[inventoryID][itemID])
                    .Take(count)
                    .Select(u => new InventoryItemEntryResult()
                    {
                        UserID = u.ID.ToString(),
                        Username = ToolHelpers.GetDisplayUsername(u),
                        Amount = u.InventoryAmounts[inventoryID][itemID],
                    })
                    .ToList();

                return new InventoryItemLeaderboardResult()
                {
                    InventoryID = inventoryID.ToString(),
                    InventoryName = inventory.Name,
                    ItemID = itemID.ToString(),
                    ItemName = item.Name,
                    Users = entries,
                };
            });
        }

        public class WatchtimeLeaderboardResult
        {
            [Description("The top viewers by watch time, highest first.")]
            public List<WatchtimeEntryResult> Users { get; set; } = new List<WatchtimeEntryResult>();
        }

        public class WatchtimeEntryResult
        {
            [Description("The internal Mix It Up GUID of the viewer.")]
            public string UserID { get; set; }

            [Description("The viewer's username.")]
            public string Username { get; set; }

            [Description("Total minutes the viewer has been seen online.")]
            public int OnlineViewingMinutes { get; set; }
        }

        public class CurrencyLeaderboardResult
        {
            [Description("The GUID of the currency ranked on.")]
            public string CurrencyID { get; set; }

            [Description("The display name of the currency ranked on.")]
            public string CurrencyName { get; set; }

            [Description("The top viewers by balance, highest first.")]
            public List<CurrencyEntryResult> Users { get; set; } = new List<CurrencyEntryResult>();
        }

        public class CurrencyEntryResult
        {
            [Description("The internal Mix It Up GUID of the viewer.")]
            public string UserID { get; set; }

            [Description("The viewer's username.")]
            public string Username { get; set; }

            [Description("The viewer's balance.")]
            public int Amount { get; set; }
        }

        public class InventoryItemLeaderboardResult
        {
            [Description("The GUID of the inventory ranked on.")]
            public string InventoryID { get; set; }

            [Description("The display name of the inventory ranked on.")]
            public string InventoryName { get; set; }

            [Description("The GUID of the item ranked on.")]
            public string ItemID { get; set; }

            [Description("The display name of the item ranked on.")]
            public string ItemName { get; set; }

            [Description("The top viewers by quantity held, highest first.")]
            public List<InventoryItemEntryResult> Users { get; set; } = new List<InventoryItemEntryResult>();
        }

        public class InventoryItemEntryResult
        {
            [Description("The internal Mix It Up GUID of the viewer.")]
            public string UserID { get; set; }

            [Description("The viewer's username.")]
            public string Username { get; set; }

            [Description("How many of the item the viewer holds.")]
            public int Amount { get; set; }
        }
    }
}
