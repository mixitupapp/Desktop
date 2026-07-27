using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MixItUp.WPF.Services.MCP.Tools
{
    /// <summary>
    /// Result shapes shared across more than one tool class.
    /// </summary>
    /// <remarks>
    /// Every tool returns one of these rather than a bare string so the SDK can generate an output
    /// schema and populate structuredContent. A tool whose only product is a side effect still returns
    /// an object, because a client consuming structured output has nothing to read from a string.
    /// <para>
    /// Nesting appears in results only. Tool <em>inputs</em> are flat scalars throughout: an agent has
    /// to construct arguments, but it only has to read results.
    /// </para>
    /// </remarks>
    public class ActionResult
    {
        [Description("Human-readable confirmation of what was done.")]
        public string Message { get; set; }

        [Description("Whether the action was carried out.")]
        public bool Success { get; set; } = true;

        public ActionResult() { }

        public ActionResult(string message) { this.Message = message; }
    }

    public class UserPlatformResult
    {
        [Description("The streaming platform this identity belongs to.")]
        public string Platform { get; set; }

        [Description("The platform's own ID for this viewer.")]
        public string ID { get; set; }

        [Description("The viewer's username on this platform.")]
        public string Username { get; set; }

        [Description("The viewer's display name on this platform.")]
        public string DisplayName { get; set; }

        [Description("The roles the viewer holds on this platform, for example Moderator or Subscriber.")]
        public List<string> Roles { get; set; } = new List<string>();

        [Description("When the viewer followed the channel, or null if they have not.")]
        public DateTimeOffset? FollowDate { get; set; }

        [Description("When the viewer subscribed to the channel, or null if they have not.")]
        public DateTimeOffset? SubscribeDate { get; set; }

        [Description("The viewer's subscription tier, or 0 if not subscribed.")]
        public int SubscriberTier { get; set; }
    }

    public class UserCurrencyAmountResult
    {
        [Description("The GUID of the currency. Call list_currencies to resolve names to IDs.")]
        public string CurrencyID { get; set; }

        [Description("The display name of the currency, or null if the currency no longer exists.")]
        public string CurrencyName { get; set; }

        [Description("The amount the viewer holds.")]
        public int Amount { get; set; }

        [Description("True when the viewer holds an amount for a currency that has been deleted from settings.")]
        public bool IsOrphaned { get; set; }
    }

    public class UserInventoryAmountResult
    {
        [Description("The GUID of the inventory. Call list_inventories to resolve names to IDs.")]
        public string InventoryID { get; set; }

        [Description("The display name of the inventory, or null if the inventory no longer exists.")]
        public string InventoryName { get; set; }

        [Description("The GUID of the item.")]
        public string ItemID { get; set; }

        [Description("The display name of the item, or null if the item no longer exists.")]
        public string ItemName { get; set; }

        [Description("The quantity the viewer holds.")]
        public int Amount { get; set; }

        [Description("True when the viewer holds an amount for an inventory or item that has been deleted from settings.")]
        public bool IsOrphaned { get; set; }
    }

    /// <summary>
    /// The full stored record for a viewer.
    /// </summary>
    public class UserResult
    {
        [Description("The internal Mix It Up GUID for this viewer. This is what the userId argument on other tools expects.")]
        public string ID { get; set; }

        [Description("The viewer's username on the first platform they are known on. Viewers can span platforms, so treat Platforms as authoritative.")]
        public string Username { get; set; }

        [Description("Total minutes this viewer has been seen online.")]
        public int OnlineViewingMinutes { get; set; }

        [Description("When this viewer was last active.")]
        public DateTimeOffset LastActivity { get; set; }

        [Description("When this viewer's record was last written.")]
        public DateTimeOffset LastUpdated { get; set; }

        [Description("The custom title assigned to this viewer, if any.")]
        public string CustomTitle { get; set; }

        [Description("Streamer notes stored against this viewer, if any.")]
        public string Notes { get; set; }

        [Description("True when the viewer is excluded from currency, rank and inventory tracking.")]
        public bool IsSpecialtyExcluded { get; set; }

        [Description("The identities this viewer has on each platform.")]
        public List<UserPlatformResult> Platforms { get; set; } = new List<UserPlatformResult>();

        [Description("Currency balances held by this viewer.")]
        public List<UserCurrencyAmountResult> CurrencyAmounts { get; set; } = new List<UserCurrencyAmountResult>();

        [Description("Inventory items held by this viewer.")]
        public List<UserInventoryAmountResult> InventoryAmounts { get; set; } = new List<UserInventoryAmountResult>();

        public static UserResult From(UserV2Model user)
        {
            UserResult result = new UserResult()
            {
                ID = user.ID.ToString(),
                Username = ToolHelpers.GetDisplayUsername(user),
                OnlineViewingMinutes = user.OnlineViewingMinutes,
                LastActivity = user.LastActivity,
                LastUpdated = user.LastUpdated,
                CustomTitle = user.CustomTitle,
                Notes = user.Notes,
                IsSpecialtyExcluded = user.IsSpecialtyExcluded,
            };

            foreach (KeyValuePair<StreamingPlatformTypeEnum, UserPlatformV2ModelBase> kvp in user.PlatformData)
            {
                UserPlatformResult platform = new UserPlatformResult()
                {
                    Platform = kvp.Key.ToString(),
                    ID = kvp.Value.ID,
                    Username = kvp.Value.Username,
                    DisplayName = kvp.Value.DisplayName,
                    FollowDate = kvp.Value.FollowDate,
                    SubscribeDate = kvp.Value.SubscribeDate,
                    SubscriberTier = kvp.Value.SubscriberTier,
                };
                foreach (var role in kvp.Value.Roles)
                {
                    platform.Roles.Add(role.ToString());
                }
                result.Platforms.Add(platform);
            }

            // A viewer can hold an amount for a currency or item that has since been deleted, so the
            // name is reported separately from the ID rather than being used as the key. Keying on a
            // name that falls back to a raw GUID makes orphans indistinguishable from real entries.
            Dictionary<Guid, CurrencyModel> currencies = ToolHelpers.SnapshotCurrencies();
            foreach (KeyValuePair<Guid, int> kvp in user.CurrencyAmounts)
            {
                bool known = currencies.TryGetValue(kvp.Key, out CurrencyModel currency);
                result.CurrencyAmounts.Add(new UserCurrencyAmountResult()
                {
                    CurrencyID = kvp.Key.ToString(),
                    CurrencyName = known ? currency.Name : null,
                    Amount = kvp.Value,
                    IsOrphaned = !known,
                });
            }

            Dictionary<Guid, InventoryModel> inventories = ToolHelpers.SnapshotInventories();
            foreach (KeyValuePair<Guid, Dictionary<Guid, int>> inventoryAmounts in user.InventoryAmounts)
            {
                bool knownInventory = inventories.TryGetValue(inventoryAmounts.Key, out InventoryModel inventory);
                foreach (KeyValuePair<Guid, int> itemAmount in inventoryAmounts.Value)
                {
                    InventoryItemModel item = knownInventory ? inventory.GetItem(itemAmount.Key) : null;
                    result.InventoryAmounts.Add(new UserInventoryAmountResult()
                    {
                        InventoryID = inventoryAmounts.Key.ToString(),
                        InventoryName = knownInventory ? inventory.Name : null,
                        ItemID = itemAmount.Key.ToString(),
                        ItemName = item?.Name,
                        Amount = itemAmount.Value,
                        IsOrphaned = item == null,
                    });
                }
            }

            return result;
        }
    }

    /// <summary>
    /// A viewer reduced to what a caller needs to pick one out of a list.
    /// </summary>
    public class UserSummaryResult
    {
        [Description("The internal Mix It Up GUID for this viewer.")]
        public string ID { get; set; }

        [Description("The viewer's username on the first platform they are known on.")]
        public string Username { get; set; }

        [Description("Total minutes this viewer has been seen online.")]
        public int OnlineViewingMinutes { get; set; }

        [Description("When this viewer was last active.")]
        public DateTimeOffset LastActivity { get; set; }

        [Description("The platforms this viewer is known on.")]
        public List<string> Platforms { get; set; } = new List<string>();

        public static UserSummaryResult From(UserV2Model user)
        {
            UserSummaryResult result = new UserSummaryResult()
            {
                ID = user.ID.ToString(),
                Username = ToolHelpers.GetDisplayUsername(user),
                OnlineViewingMinutes = user.OnlineViewingMinutes,
                LastActivity = user.LastActivity,
            };

            foreach (KeyValuePair<StreamingPlatformTypeEnum, UserPlatformV2ModelBase> kvp in user.PlatformData)
            {
                result.Platforms.Add(kvp.Key.ToString());
            }

            return result;
        }
    }
}
