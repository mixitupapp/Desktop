using MixItUp.API.V2.Models;
using MixItUp.Base;
using MixItUp.Base.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V2
{
    [Route("api/v2/leaderboard")]
    [ApiController]
    public class LeaderboardV2Controller : ControllerBase
    {
        [Route("watchtime")]
        [HttpGet]
        public async Task<IActionResult> GetTopWatchtimeUsers([FromQuery] int count = 10)
        {
            if (count <= 0) { count = 10; }

            await ServiceManager.Get<UserService>().LoadAllUserData();

            var topUsers = ChannelSession.Settings.Users.Values
                .Where(u => u.OnlineViewingMinutes > 0)
                .OrderByDescending(u => u.OnlineViewingMinutes)
                .Take(count)
                .Select(u => new WatchtimeUserAmount
                {
                    UserID = u.ID,
                    Username = u.GetAllPlatformUsernames().FirstOrDefault() ?? string.Empty,
                    OnlineViewingMinutes = u.OnlineViewingMinutes
                })
                .ToList();

            return Ok(new GetTopWatchtimeUsersResponse { Users = topUsers });
        }

        [Route("currency/{currencyId:guid}")]
        [HttpGet]
        public async Task<IActionResult> GetTopCurrencyUsers(Guid currencyId, [FromQuery] int count = 10)
        {
            if (!ChannelSession.Settings.Currency.TryGetValue(currencyId, out var currency) || currency == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"Currency with ID '{currencyId}' not found"
                });
            }

            if (count <= 0) { count = 10; }

            await ServiceManager.Get<UserService>().LoadAllUserData();

            var topUsers = ChannelSession.Settings.Users.Values
                .Where(u => u.CurrencyAmounts.ContainsKey(currencyId) && u.CurrencyAmounts[currencyId] > 0)
                .OrderByDescending(u => u.CurrencyAmounts[currencyId])
                .Take(count)
                .Select(u => new CurrencyUserAmount
                {
                    UserID = u.ID,
                    Username = u.GetAllPlatformUsernames().FirstOrDefault() ?? string.Empty,
                    Amount = u.CurrencyAmounts[currencyId]
                })
                .ToList();

            return Ok(new GetTopCurrencyUsersResponse
            {
                CurrencyID = currencyId,
                CurrencyName = currency.Name,
                Users = topUsers
            });
        }

        [Route("inventory/{inventoryId:guid}/{itemId:guid}")]
        [HttpGet]
        public async Task<IActionResult> GetTopInventoryItemUsers(Guid inventoryId, Guid itemId, [FromQuery] int count = 10)
        {
            if (!ChannelSession.Settings.Inventory.TryGetValue(inventoryId, out var inventory) || inventory == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"Inventory with ID '{inventoryId}' not found"
                });
            }

            var item = inventory.GetItem(itemId);
            if (item == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"Item with ID '{itemId}' not found in inventory"
                });
            }

            if (count <= 0) { count = 10; }

            await ServiceManager.Get<UserService>().LoadAllUserData();

            var topUsers = ChannelSession.Settings.Users.Values
                .Where(u => u.InventoryAmounts.ContainsKey(inventoryId) && u.InventoryAmounts[inventoryId].ContainsKey(itemId) && u.InventoryAmounts[inventoryId][itemId] > 0)
                .OrderByDescending(u => u.InventoryAmounts[inventoryId][itemId])
                .Take(count)
                .Select(u => new InventoryItemUserAmount
                {
                    UserID = u.ID,
                    Username = u.GetAllPlatformUsernames().FirstOrDefault() ?? string.Empty,
                    Amount = u.InventoryAmounts[inventoryId][itemId]
                })
                .ToList();

            return Ok(new GetTopInventoryItemUsersResponse
            {
                InventoryID = inventoryId,
                InventoryName = inventory.Name,
                ItemID = itemId,
                ItemName = item.Name,
                Users = topUsers
            });
        }
    }
}
