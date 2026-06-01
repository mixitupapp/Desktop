using MixItUp.API.V2.Models;
using MixItUp.Base;
using MixItUp.Base.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V2
{
    [Route("api/v2/inventory")]
    [ApiController]
    public class InventoryV2Controller : ControllerBase
    {
        [HttpGet]
        public IActionResult GetInventories()
        {
            var inventories = new List<GetInventoryResponse>();

            foreach (var inventory in ChannelSession.Settings.Inventory)
            {
                var items = new List<GetInventoryItemResponse>();
                foreach (var item in inventory.Value.Items)
                {
                    items.Add(new GetInventoryItemResponse
                    {
                        ID = item.Value.ID,
                        Name = item.Value.Name
                    });
                }

                inventories.Add(new GetInventoryResponse
                {
                    ID = inventory.Value.ID,
                    Name = inventory.Value.Name,
                    Items = items
                });
            }

            return Ok(inventories);
        }

        [Route("{inventoryId:guid}/{itemId:guid}/top")]
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

        [Route("{inventoryId:guid}/{userId:guid}")]
        [HttpGet]
        public async Task<IActionResult> GetInventoryItemAmountsForUser(Guid inventoryId, Guid userId)
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

            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (!ChannelSession.Settings.Users.TryGetValue(userId, out var user) || user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"User with ID '{userId}' not found"
                });
            }

            var itemAmounts = new List<GetInventoryItemAmountResponse>();
            foreach (var item in inventory.Items)
            {
                int amount = inventory.GetAmount(user, item.Value);
                if (amount > 0)
                {
                    itemAmounts.Add(new GetInventoryItemAmountResponse()
                    {
                        ID = item.Value.ID,
                        Name = item.Value.Name,
                        Amount = amount
                    });
                }
            }

            return Ok(itemAmounts);
        }

        [Route("{inventoryId:guid}/{itemId:guid}/{userId:guid}")]
        [HttpGet]
        public async Task<IActionResult> GetInventoryItemAmountForUser(Guid inventoryId, Guid itemId, Guid userId)
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

            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (!ChannelSession.Settings.Users.TryGetValue(userId, out var user) || user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"User with ID '{userId}' not found"
                });
            }

            return Ok(new GetInventoryItemAmountResponse
            {
                ID = itemId,
                Name = item.Name,
                Amount = inventory.GetAmount(user, item)
            });
        }

        [Route("{inventoryId:guid}/{itemId:guid}/{userId:guid}")]
        [HttpPatch]
        public async Task<IActionResult> UpdateInventoryItemAmountForUser(Guid inventoryId, Guid itemId, Guid userId, [FromBody] UpdateInventoryAmount updateAmount)
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

            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (!ChannelSession.Settings.Users.TryGetValue(userId, out var user) || user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"User with ID '{userId}' not found"
                });
            }

            if (updateAmount.Amount > 0)
            {
                inventory.AddAmount(user, item, updateAmount.Amount);
            }
            else if (updateAmount.Amount < 0)
            {
                inventory.SubtractAmount(user, item, -1 * updateAmount.Amount);
            }

            return Ok(new GetInventoryItemAmountResponse
            {
                ID = itemId,
                Name = item.Name,
                Amount = inventory.GetAmount(user, item)
            });
        }

        [Route("{inventoryId:guid}/{itemId:guid}/{userId:guid}")]
        [HttpPut]
        public async Task<IActionResult> SetInventoryItemAmountForUser(Guid inventoryId, Guid itemId, Guid userId, [FromBody] UpdateInventoryAmount updateAmount)
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

            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (!ChannelSession.Settings.Users.TryGetValue(userId, out var user) || user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"User with ID '{userId}' not found"
                });
            }

            inventory.SetAmount(user, item, updateAmount.Amount);

            return Ok(new GetInventoryItemAmountResponse
            {
                ID = itemId,
                Name = item.Name,
                Amount = inventory.GetAmount(user, item)
            });
        }
    }
}