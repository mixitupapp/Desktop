using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Currency;
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
    /// <summary>
    /// Currency balances are real viewer data. Every mutating tool here changes what a viewer sees
    /// when they check their balance in chat, so each one says so in its description rather than
    /// relying on the caller having read the documentation.
    /// </summary>
    [McpServerToolType]
    public class CurrencyTools
    {
        [McpServerTool(Name = "list_currencies", ReadOnly = true, UseStructuredContent = true)]
        [Description("List every currency and rank system configured in Mix It Up. Returns the currency IDs that get_user_currency, set_user_currency, adjust_user_currency, give_currency_to_users and get_currency_leaderboard all require.")]
        public static Task<ListCurrenciesResult> ListCurrencies()
        {
            return ToolHelpers.RunWithTimeout("list_currencies", () =>
            {
                Dictionary<Guid, CurrencyModel> currencies = ToolHelpers.SnapshotCurrencies();

                return Task.FromResult(new ListCurrenciesResult()
                {
                    TotalCount = currencies.Count,
                    Currencies = currencies.Values.OrderBy(c => c.Name).Select(ToResult).ToList(),
                });
            });
        }

        [McpServerTool(Name = "get_user_currency", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get how much of one currency a single viewer holds. Identify the viewer by either userId or username. Call list_currencies for currency IDs.")]
        public static Task<UserCurrencyResult> GetUserCurrency(
            [Description("The GUID of the currency. Call list_currencies to find it.")] string currencyId,
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("get_user_currency", async () =>
            {
                CurrencyModel currency = ToolHelpers.GetCurrencyOrThrow(currencyId);
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                return ToUserResult(currency, user);
            });
        }

        [McpServerTool(Name = "adjust_user_currency", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true)]
        [Description("Add to or subtract from a viewer's currency balance, relative to what they already hold. WARNING: this changes a real viewer's balance on a live channel and cannot be undone. Pass a negative amount to subtract. To replace the balance outright, use set_user_currency instead.")]
        public static Task<UserCurrencyResult> AdjustUserCurrency(
            [Description("The GUID of the currency. Call list_currencies to find it.")] string currencyId,
            [Description("How much to add. Negative values subtract.")] int amount,
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("adjust_user_currency", async () =>
            {
                CurrencyModel currency = ToolHelpers.GetCurrencyOrThrow(currencyId);
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                if (amount == 0)
                {
                    throw new McpException("amount cannot be zero. Use a positive value to add or a negative value to subtract.");
                }

                int previous = currency.GetAmount(user);

                if (amount > 0)
                {
                    currency.AddAmount(user, amount);
                }
                else
                {
                    // The v1 Developer API refuses a subtraction the viewer cannot cover rather than
                    // silently flooring at zero, which is the safer behaviour to keep: an agent that
                    // asked to take 500 from someone holding 20 has made a mistake worth reporting.
                    int quantityToRemove = -amount;
                    if (!currency.HasAmount(new UserV2ViewModel(user), quantityToRemove))
                    {
                        throw new McpException($"'{ToolHelpers.GetDisplayUsername(user)}' holds {previous} {currency.Name} and cannot give up {quantityToRemove}. Use set_user_currency if you intend to overwrite the balance.");
                    }
                    currency.SubtractAmount(user, quantityToRemove);
                }

                ToolHelpers.LogToolAction("adjust_user_currency", $"'{currency.Name}' for user {user.ID} changed by {amount}, from {previous} to {currency.GetAmount(user)}");

                return ToUserResult(currency, user);
            });
        }

        [McpServerTool(Name = "set_user_currency", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Set a viewer's currency balance to an exact amount, discarding what they held. WARNING: this overwrites a real viewer's balance on a live channel and cannot be undone. To change the balance relative to its current value, use adjust_user_currency instead.")]
        public static Task<UserCurrencyResult> SetUserCurrency(
            [Description("The GUID of the currency. Call list_currencies to find it.")] string currencyId,
            [Description("The exact balance to set. Clamped to the currency's configured maximum.")] int amount,
            [Description("The internal Mix It Up GUID of the viewer. Provide this or username, not both.")] string userId = null,
            [Description("The viewer's platform username. Provide this or userId, not both.")] string username = null,
            [Description("Platform to resolve username on, for example Twitch. Ignored when userId is given. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("set_user_currency", async () =>
            {
                CurrencyModel currency = ToolHelpers.GetCurrencyOrThrow(currencyId);
                UserV2Model user = await ToolHelpers.ResolveUser(userId, platform, username);

                if (amount < 0)
                {
                    throw new McpException("amount cannot be negative. Use 0 to clear the balance.");
                }

                int previous = currency.GetAmount(user);
                currency.SetAmount(user, amount);

                ToolHelpers.LogToolAction("set_user_currency", $"'{currency.Name}' for user {user.ID} set from {previous} to {currency.GetAmount(user)}");

                return ToUserResult(currency, user);
            });
        }

        [McpServerTool(Name = "give_currency_to_users", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true)]
        [Description("Give the same amount of one currency to several viewers at once. WARNING: this changes real viewer balances on a live channel and cannot be undone. Usernames that cannot be resolved are skipped and reported back rather than failing the whole call. To give different amounts, call this once per amount, or use adjust_user_currency per viewer.")]
        public static Task<GiveCurrencyResult> GiveCurrencyToUsers(
            [Description("The GUID of the currency. Call list_currencies to find it.")] string currencyId,
            [Description("The platform usernames of the viewers to give to.")] string[] usernames,
            [Description("How much to give each viewer. Must be greater than zero.")] int amount,
            [Description("Platform to resolve the usernames on, for example Twitch. Defaults to the profile's default platform.")] string platform = null)
        {
            return ToolHelpers.RunWithTimeout("give_currency_to_users", async () =>
            {
                CurrencyModel currency = ToolHelpers.GetCurrencyOrThrow(currencyId);

                if (usernames == null || usernames.Length == 0)
                {
                    throw new McpException("usernames cannot be empty.");
                }
                ToolHelpers.RequirePositive(amount, nameof(amount));

                await ServiceManager.Get<UserService>().LoadAllUserData();

                StreamingPlatformTypeEnum platformType = string.IsNullOrWhiteSpace(platform)
                    ? ChannelSession.Settings.DefaultStreamingPlatform
                    : ToolHelpers.ParsePlatform(platform);

                GiveCurrencyResult result = new GiveCurrencyResult()
                {
                    CurrencyID = currency.ID.ToString(),
                    CurrencyName = currency.Name,
                    AmountGivenEach = amount,
                };

                foreach (string name in usernames)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    UserV2ViewModel found = await ServiceManager.Get<UserService>().GetUserByPlatform(
                        platformType, platformID: name, platformUsername: name, performPlatformSearch: true);

                    if (found == null || !ChannelSession.Settings.Users.TryGetValue(found.ID, out UserV2Model user) || user == null)
                    {
                        result.NotFound.Add(name);
                        continue;
                    }

                    currency.AddAmount(user, amount);
                    result.Given.Add(ToUserResult(currency, user));
                }

                ToolHelpers.LogToolAction("give_currency_to_users", $"gave {amount} '{currency.Name}' to {result.Given.Count} of {usernames.Length} requested users on {platformType}");

                return result;
            });
        }

        private static UserCurrencyResult ToUserResult(CurrencyModel currency, UserV2Model user)
        {
            return new UserCurrencyResult()
            {
                CurrencyID = currency.ID.ToString(),
                CurrencyName = currency.Name,
                UserID = user.ID.ToString(),
                Username = ToolHelpers.GetDisplayUsername(user),
                Amount = currency.GetAmount(user),
            };
        }

        private static CurrencyResult ToResult(CurrencyModel currency)
        {
            return new CurrencyResult()
            {
                ID = currency.ID.ToString(),
                Name = currency.Name,
                IsPrimary = currency.IsPrimary,
                IsRank = currency.IsRank,
                SpecialIdentifier = currency.SpecialIdentifier,
                MaxAmount = currency.MaxAmount,
                AcquireAmount = currency.AcquireAmount,
                AcquireInterval = currency.AcquireInterval,
                RankNames = currency.Ranks.OrderBy(r => r.Amount).Select(r => r.Name).ToList(),
            };
        }

        public class ListCurrenciesResult
        {
            [Description("Total number of currencies configured.")]
            public int TotalCount { get; set; }

            [Description("Every currency, ordered by name.")]
            public List<CurrencyResult> Currencies { get; set; } = new List<CurrencyResult>();
        }

        public class CurrencyResult
        {
            [Description("The GUID of the currency, required by the other currency tools.")]
            public string ID { get; set; }

            [Description("The display name of the currency.")]
            public string Name { get; set; }

            [Description("Whether this is the profile's primary currency.")]
            public bool IsPrimary { get; set; }

            [Description("Whether this currency is configured as a rank system rather than a spendable balance.")]
            public bool IsRank { get; set; }

            [Description("The special identifier used to reference this currency in command text.")]
            public string SpecialIdentifier { get; set; }

            [Description("The maximum balance a viewer may hold.")]
            public int MaxAmount { get; set; }

            [Description("How much is granted each interval while a viewer is watching.")]
            public int AcquireAmount { get; set; }

            [Description("How many minutes pass between grants.")]
            public int AcquireInterval { get; set; }

            [Description("The rank names configured for this currency, lowest first. Empty when this is not a rank system.")]
            public List<string> RankNames { get; set; } = new List<string>();
        }

        public class UserCurrencyResult
        {
            [Description("The GUID of the currency.")]
            public string CurrencyID { get; set; }

            [Description("The display name of the currency.")]
            public string CurrencyName { get; set; }

            [Description("The internal Mix It Up GUID of the viewer.")]
            public string UserID { get; set; }

            [Description("The viewer's username.")]
            public string Username { get; set; }

            [Description("The viewer's balance after the call.")]
            public int Amount { get; set; }
        }

        public class GiveCurrencyResult
        {
            [Description("The GUID of the currency given.")]
            public string CurrencyID { get; set; }

            [Description("The display name of the currency given.")]
            public string CurrencyName { get; set; }

            [Description("How much each resolved viewer received.")]
            public int AmountGivenEach { get; set; }

            [Description("The viewers who received the currency, with their resulting balances.")]
            public List<UserCurrencyResult> Given { get; set; } = new List<UserCurrencyResult>();

            [Description("Usernames that could not be resolved to a viewer and were skipped.")]
            public List<string> NotFound { get; set; } = new List<string>();
        }
    }
}
