using MixItUp.API.V2.Models;
using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V2
{
    [Route("api/v2/users")]
    [ApiController]
    public class UsersV2Controller : ControllerBase
    {
        [Route("{userId:guid}")]
        [HttpGet]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
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

            return Ok(new GetSingleUserResponse { User = UserMapper.ToUser(user) });
        }

        [Route("{platform}/{usernameOrID}")]
        [HttpGet]
        public async Task<IActionResult> GetUserByPlatformUsername(string platform, string usernameOrID)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (!Enum.TryParse<StreamingPlatformTypeEnum>(platform, ignoreCase: true, out var platformEnum))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Bad Request",
                    Detail = $"Unknown platform: {platform}"
                });
            }

            var usermodel = await ServiceManager.Get<UserService>().GetUserByPlatform(platformEnum, platformID: usernameOrID, platformUsername: usernameOrID, performPlatformSearch: true);
            if (usermodel == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"User '{usernameOrID}' not found on platform '{platform}'"
                });
            }

            if (!ChannelSession.Settings.Users.TryGetValue(usermodel.ID, out var user) || user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"User '{usernameOrID}' not found on platform '{platform}'"
                });
            }

            return Ok(new GetSingleUserResponse { User = UserMapper.ToUser(user) });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers(int skip = 0, int pageSize = 25)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            var users = ChannelSession.Settings.Users.Values
                .OrderBy(u => u.ID)
                .Skip(skip)
                .Take(pageSize);

            var result = new GetListOfUsersResponse();
            result.TotalCount = ChannelSession.Settings.Users.Count;
            foreach (var user in users)
            {
                result.Users.Add(UserMapper.ToUser(user));
            }

            return Ok(result);
        }

        [Route("active")]
        [HttpGet]
        public async Task<IActionResult> GetAllActiveUsers(int skip = 0, int pageSize = 25)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            var users = ServiceManager.Get<UserService>().GetActiveUsers()
                .OrderBy(u => u.ID)
                .Skip(skip)
                .Take(pageSize);

            var result = new GetListOfUsersResponse();
            result.TotalCount = ServiceManager.Get<UserService>().GetActiveUserCount();
            foreach (var user in users)
            {
                result.Users.Add(UserMapper.ToUser(user.Model));
            }

            return Ok(result);
        }

        [Route("add")]
        [HttpPost]
        public async Task<IActionResult> AddUser(NewUser newUser)
        {
            if (!Enum.TryParse<StreamingPlatformTypeEnum>(newUser.Platform, ignoreCase: true, out var platformEnum))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Bad Request",
                    Detail = $"Unknown platform: {newUser.Platform}"
                });
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(platformEnum, platformUsername: newUser.Username, performPlatformSearch: true);
            if (user == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"User '{newUser.Username}' not found on platform '{newUser.Platform}'"
                });
            }

            return Ok(new GetSingleUserResponse { User = UserMapper.ToUser(user.Model) });
        }

        [Route("{userId:guid}")]
        [HttpDelete]
        public async Task<IActionResult> DeleteUserById(Guid userId)
        {
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

            ServiceManager.Get<UserService>().DeleteUserData(user.ID);

            return Ok();
        }
    }

    public static class UserMapper
    {
        public static User ToUser(UserV2Model user)
        {
            return new User
            {
                ID = user.ID,
                LastActivity = user.LastActivity,
                LastUpdated = user.LastUpdated,
                OnlineViewingMinutes = user.OnlineViewingMinutes,
                CurrencyAmounts = new Dictionary<Guid, int>(user.CurrencyAmounts),
                InventoryAmounts = new Dictionary<Guid, Dictionary<Guid, int>>(user.InventoryAmounts),
                StreamPassAmounts = new Dictionary<Guid, int>(user.StreamPassAmounts),
                CustomTitle = user.CustomTitle,
                IsSpecialtyExcluded = user.IsSpecialtyExcluded,
                Notes = user.Notes,
                PlatformData = ToPlatformData(user.PlatformData),
            };
        }

        private static Dictionary<string, UserPlatformData> ToPlatformData(Dictionary<Base.Model.StreamingPlatformTypeEnum, UserPlatformV2ModelBase> platformData)
        {
            var results = new Dictionary<string, UserPlatformData>();

            foreach (var kvp in platformData)
            {
                results[kvp.Key.ToString()] = ToUserPlatformData(kvp.Value);
            }

            return results;

        }

        private static UserPlatformData ToUserPlatformData(UserPlatformV2ModelBase value)
        {
            return new UserPlatformData
            {
                Platform = value.Platform.ToString(),
                ID = value.ID,
                Username = value.Username,
                DisplayName = value.DisplayName,
                AvatarLink = value.AvatarLink,
                SubscriberBadgeLink = value.SubscriberBadgeLink,
                RoleBadgeLink = value.RoleBadgeLink,
                SpecialtyBadgeLink = value.SpecialtyBadgeLink,
                Roles = new HashSet<string>(value.Roles.Select(r => r.ToString())),
                AccountDate = value.AccountDate,
                FollowDate = value.FollowDate,
                SubscribeDate = value.SubscribeDate,
                SubscriberTier = value.SubscriberTier,
            };
        }
    }
}