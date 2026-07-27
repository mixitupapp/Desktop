using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.Tools
{
    /// <summary>
    /// Inventories hold real viewer property. As with currency, every mutating tool here states in
    /// its own description that it changes live viewer data.
    /// </summary>
    [McpServerToolType]
    public class InventoryTools
    {
        [McpServerTool(Name = "list_inventories", ReadOnly = true, UseStructuredContent = true)]
        [Description("List every inventory configured in Mix It Up along with the items each one holds. Returns the inventory and item IDs that the other inventory tools and get_inventory_item_leaderboard require.")]
        public static Task<ListInventoriesResult> ListInventories()
        {
            return ToolHelpers.RunWithTimeout("list_inventories", () =>
            {
                Dictionary<Guid, InventoryModel> inventories = ToolHelpers.SnapshotInventories();

                return Task.FromResult(new ListInventoriesResult()
                {
                    TotalCount = inventories.Count,
                    Inventories = inventories.Values.OrderBy(i => i.Name).Select(ToSummaryResult).ToList(),
                });
            });
        }

        [McpServerTool(Name = "get_inventory", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get one inventory in full, including its shop and trade configuration and each item's buy price, sell price and maximum. Use list_inventories to find the inventory ID.")]
        public static Task<InventoryResult> GetInventory(
            [Description("The GUID of the inventory.")] string inventoryId)
        {
            return ToolHelpers.RunWithTimeout("get_inventory", () =>
            {
                return Task.FromResult(ToResult(ToolHelpers.GetInventoryOrThrow(inventoryId)));
            });
        }

        [McpServerTool(Name = "list_user_inventory_items", ReadOnly = true, UseStructuredContent = true)]
        [Description("List every item a single viewer holds in one inventory, skipping items they hold none of. Identify the viewer by either userId or username.")]
        public static Task<ListUserInventoryItemsResult> ListUserInventoryItems(
            [Description("The GUID of the inventory. Call list_inventories to find it.")] string inventoryId,
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("list_user_inventory_items", async () =>
            {
                InventoryModel inventory = ToolHelpers.GetInventoryOrThrow(inventoryId);
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                ListUserInventoryItemsResult result = new ListUserInventoryItemsResult()
                {
                    InventoryID = inventory.ID.ToString(),
                    InventoryName = inventory.Name,
                    UserID = user.ID.ToString(),
                    Username = ToolHelpers.GetDisplayUsername(user),
                };

                foreach (InventoryItemModel item in inventory.Items.Values.OrderBy(i => i.Name))
                {
                    int amount = inventory.GetAmount(user, item);
                    if (amount > 0)
                    {
                        result.Items.Add(new UserInventoryItemResult()
                        {
                            ItemID = item.ID.ToString(),
                            ItemName = item.Name,
                            Amount = amount,
                        });
                    }
                }

                return result;
            });
        }

        [McpServerTool(Name = "get_user_inventory_item", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get how many of one inventory item a single viewer holds. Identify the item by either itemId or itemName, and the viewer by either userId or username.")]
        public static Task<UserInventoryItemAmountResult> GetUserInventoryItem(
            [Description("The GUID of the inventory. Call list_inventories to find it.")] string inventoryId,
            [Description("The GUID of the item. Provide this or itemName, not both.")] string itemId = null,
            [Description("The name of the item. Provide this or itemId, not both.")] string itemName = null,
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("get_user_inventory_item", async () =>
            {
                InventoryModel inventory = ToolHelpers.GetInventoryOrThrow(inventoryId);
                InventoryItemModel item = ResolveItem(inventory, itemId, itemName);
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                return ToAmountResult(inventory, item, user);
            });
        }

        [McpServerTool(Name = "adjust_user_inventory_item", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true)]
        [Description("Add to or subtract from how many of an item a viewer holds, relative to what they already have. WARNING: this changes a real viewer's inventory on a live channel and cannot be undone. Pass a negative amount to take items away. To replace the quantity outright, use set_user_inventory_item instead.")]
        public static Task<UserInventoryItemAmountResult> AdjustUserInventoryItem(
            [Description("The GUID of the inventory. Call list_inventories to find it.")] string inventoryId,
            [Description("How many to add. Negative values subtract.")] int amount,
            [Description("The GUID of the item. Provide this or itemName, not both.")] string itemId = null,
            [Description("The name of the item. Provide this or itemId, not both.")] string itemName = null,
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("adjust_user_inventory_item", async () =>
            {
                InventoryModel inventory = ToolHelpers.GetInventoryOrThrow(inventoryId);
                InventoryItemModel item = ResolveItem(inventory, itemId, itemName);
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                if (amount == 0)
                {
                    throw new McpException("amount cannot be zero. Use a positive value to add or a negative value to subtract.");
                }

                int previous = inventory.GetAmount(user, item);

                if (amount > 0)
                {
                    inventory.AddAmount(user, item, amount);
                }
                else
                {
                    // Matches the v1 Developer API, which refuses a removal the viewer cannot cover
                    // rather than silently flooring at zero.
                    int quantityToRemove = -amount;
                    if (previous < quantityToRemove && !user.IsSpecialtyExcluded)
                    {
                        throw new McpException($"'{ToolHelpers.GetDisplayUsername(user)}' holds {previous} of '{item.Name}' and cannot give up {quantityToRemove}. Use set_user_inventory_item if you intend to overwrite the quantity.");
                    }
                    inventory.SubtractAmount(user, item, quantityToRemove);
                }

                ToolHelpers.LogToolAction("adjust_user_inventory_item", $"'{inventory.Name}'/'{item.Name}' for user {user.ID} changed by {amount}, from {previous} to {inventory.GetAmount(user, item)}");

                return ToAmountResult(inventory, item, user);
            });
        }

        [McpServerTool(Name = "set_user_inventory_item", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Set how many of an item a viewer holds to an exact quantity, discarding what they had. WARNING: this overwrites a real viewer's inventory on a live channel and cannot be undone. To change the quantity relative to its current value, use adjust_user_inventory_item instead.")]
        public static Task<UserInventoryItemAmountResult> SetUserInventoryItem(
            [Description("The GUID of the inventory. Call list_inventories to find it.")] string inventoryId,
            [Description("The exact quantity to set. Clamped to the item's configured maximum.")] int amount,
            [Description("The GUID of the item. Provide this or itemName, not both.")] string itemId = null,
            [Description("The name of the item. Provide this or itemId, not both.")] string itemName = null,
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("set_user_inventory_item", async () =>
            {
                InventoryModel inventory = ToolHelpers.GetInventoryOrThrow(inventoryId);
                InventoryItemModel item = ResolveItem(inventory, itemId, itemName);
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                if (amount < 0)
                {
                    throw new McpException("amount cannot be negative. Use 0 to clear the item.");
                }

                int previous = inventory.GetAmount(user, item);
                inventory.SetAmount(user, item, amount);

                ToolHelpers.LogToolAction("set_user_inventory_item", $"'{inventory.Name}'/'{item.Name}' for user {user.ID} set from {previous} to {inventory.GetAmount(user, item)}");

                return ToAmountResult(inventory, item, user);
            });
        }

        /// <summary>
        /// Items are addressed by GUID in v2 and by name in v1. Both are accepted for the same reason
        /// users accept a GUID or a username: it is one target reached two ways, not two operations.
        /// </summary>
        private static InventoryItemModel ResolveItem(InventoryModel inventory, string itemId, string itemName)
        {
            bool hasItemId = !string.IsNullOrWhiteSpace(itemId);
            bool hasItemName = !string.IsNullOrWhiteSpace(itemName);

            if (hasItemId == hasItemName)
            {
                throw new McpException(hasItemId
                    ? "Specify either itemId or itemName, not both."
                    : $"Either itemId or itemName is required. Inventory '{inventory.Name}' holds: {string.Join(", ", inventory.Items.Values.Select(i => i.Name))}");
            }

            if (hasItemId)
            {
                return ToolHelpers.GetInventoryItemOrThrow(inventory, itemId);
            }

            InventoryItemModel item = inventory.GetItem(itemName);
            if (item == null)
            {
                throw new McpException($"Inventory '{inventory.Name}' has no item named '{itemName}'. It holds: {string.Join(", ", inventory.Items.Values.Select(i => i.Name))}");
            }
            return item;
        }

        private static UserInventoryItemAmountResult ToAmountResult(InventoryModel inventory, InventoryItemModel item, UserV2Model user)
        {
            return new UserInventoryItemAmountResult()
            {
                InventoryID = inventory.ID.ToString(),
                InventoryName = inventory.Name,
                ItemID = item.ID.ToString(),
                ItemName = item.Name,
                UserID = user.ID.ToString(),
                Username = ToolHelpers.GetDisplayUsername(user),
                Amount = inventory.GetAmount(user, item),
            };
        }

        private static InventorySummaryResult ToSummaryResult(InventoryModel inventory)
        {
            return new InventorySummaryResult()
            {
                ID = inventory.ID.ToString(),
                Name = inventory.Name,
                Items = inventory.Items.Values.OrderBy(i => i.Name).Select(i => new InventoryItemSummaryResult()
                {
                    ID = i.ID.ToString(),
                    Name = i.Name,
                }).ToList(),
            };
        }

        private static InventoryResult ToResult(InventoryModel inventory)
        {
            return new InventoryResult()
            {
                ID = inventory.ID.ToString(),
                Name = inventory.Name,
                SpecialIdentifier = inventory.SpecialIdentifier,
                DefaultMaxAmount = inventory.DefaultMaxAmount,
                ShopEnabled = inventory.ShopEnabled,
                ShopCommand = inventory.ShopCommand,
                ShopCurrencyID = inventory.ShopCurrencyID != Guid.Empty ? inventory.ShopCurrencyID.ToString() : null,
                TradeEnabled = inventory.TradeEnabled,
                TradeCommand = inventory.TradeCommand,
                Items = inventory.Items.Values.OrderBy(i => i.Name).Select(i => new InventoryItemResult()
                {
                    ID = i.ID.ToString(),
                    Name = i.Name,
                    MaxAmount = i.MaxAmount,
                    BuyAmount = i.BuyAmount,
                    SellAmount = i.SellAmount,
                }).ToList(),
            };
        }

        public class ListInventoriesResult
        {
            [Description("Total number of inventories configured.")]
            public int TotalCount { get; set; }

            [Description("Every inventory, ordered by name.")]
            public List<InventorySummaryResult> Inventories { get; set; } = new List<InventorySummaryResult>();
        }

        public class InventorySummaryResult
        {
            [Description("The GUID of the inventory, required by the other inventory tools.")]
            public string ID { get; set; }

            [Description("The display name of the inventory.")]
            public string Name { get; set; }

            [Description("The items this inventory holds.")]
            public List<InventoryItemSummaryResult> Items { get; set; } = new List<InventoryItemSummaryResult>();
        }

        public class InventoryItemSummaryResult
        {
            [Description("The GUID of the item.")]
            public string ID { get; set; }

            [Description("The display name of the item.")]
            public string Name { get; set; }
        }

        public class InventoryResult
        {
            [Description("The GUID of the inventory.")]
            public string ID { get; set; }

            [Description("The display name of the inventory.")]
            public string Name { get; set; }

            [Description("The special identifier used to reference this inventory in command text.")]
            public string SpecialIdentifier { get; set; }

            [Description("The default maximum quantity of any one item a viewer may hold.")]
            public int DefaultMaxAmount { get; set; }

            [Description("Whether viewers can buy and sell items from this inventory in chat.")]
            public bool ShopEnabled { get; set; }

            [Description("The chat command that opens the shop.")]
            public string ShopCommand { get; set; }

            [Description("The GUID of the currency the shop trades in, or null if no shop currency is set. Call list_currencies to resolve it.")]
            public string ShopCurrencyID { get; set; }

            [Description("Whether viewers can trade items from this inventory in chat.")]
            public bool TradeEnabled { get; set; }

            [Description("The chat command that starts a trade.")]
            public string TradeCommand { get; set; }

            [Description("Every item in this inventory with its shop pricing and limits.")]
            public List<InventoryItemResult> Items { get; set; } = new List<InventoryItemResult>();
        }

        public class InventoryItemResult
        {
            [Description("The GUID of the item.")]
            public string ID { get; set; }

            [Description("The display name of the item.")]
            public string Name { get; set; }

            [Description("The maximum quantity a viewer may hold, or -1 when the inventory default applies.")]
            public int MaxAmount { get; set; }

            [Description("What the shop charges for one of these, or -1 when it cannot be bought.")]
            public int BuyAmount { get; set; }

            [Description("What the shop pays for one of these, or -1 when it cannot be sold.")]
            public int SellAmount { get; set; }
        }

        public class ListUserInventoryItemsResult
        {
            [Description("The GUID of the inventory.")]
            public string InventoryID { get; set; }

            [Description("The display name of the inventory.")]
            public string InventoryName { get; set; }

            [Description("The internal Mix It Up GUID of the viewer.")]
            public string UserID { get; set; }

            [Description("The viewer's username.")]
            public string Username { get; set; }

            [Description("The items the viewer holds at least one of.")]
            public List<UserInventoryItemResult> Items { get; set; } = new List<UserInventoryItemResult>();
        }

        public class UserInventoryItemResult
        {
            [Description("The GUID of the item.")]
            public string ItemID { get; set; }

            [Description("The display name of the item.")]
            public string ItemName { get; set; }

            [Description("How many the viewer holds.")]
            public int Amount { get; set; }
        }

        public class UserInventoryItemAmountResult
        {
            [Description("The GUID of the inventory.")]
            public string InventoryID { get; set; }

            [Description("The display name of the inventory.")]
            public string InventoryName { get; set; }

            [Description("The GUID of the item.")]
            public string ItemID { get; set; }

            [Description("The display name of the item.")]
            public string ItemName { get; set; }

            [Description("The internal Mix It Up GUID of the viewer.")]
            public string UserID { get; set; }

            [Description("The viewer's username.")]
            public string Username { get; set; }

            [Description("How many the viewer holds after the call.")]
            public int Amount { get; set; }
        }
    }
}
