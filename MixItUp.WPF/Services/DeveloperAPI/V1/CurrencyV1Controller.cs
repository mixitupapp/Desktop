using MixItUp.API.V1.Models;
using MixItUp.Base;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V1
{
    [Route("api/currency")]
    [ApiController]
    public class CurrencyV1Controller : ControllerBase
    {
        [HttpGet]
        public IEnumerable<Currency> Get()
        {
            List<Currency> list = new List<Currency>();
            foreach (var currency in ChannelSession.Settings.Currency.Values)
            {
                list.Add(CurrencyFromUserCurrencyViewModel(currency));
            }

            return list;
        }

        [Route("{currencyID:guid}")]
        [HttpGet]
        public IActionResult Get(Guid currencyID)
        {
            if (!ChannelSession.Settings.Currency.ContainsKey(currencyID))
            {
                return NotFound(new Error { Message = $"Unable to find currency: {currencyID.ToString()}." });
            }

            return Ok(CurrencyFromUserCurrencyViewModel(ChannelSession.Settings.Currency[currencyID]));
        }

        [Route("{currencyID:guid}/top")]
        [HttpGet]
        public async Task<IActionResult> Get(Guid currencyID, int count = 10)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (!ChannelSession.Settings.Currency.ContainsKey(currencyID))
            {
                return NotFound(new Error { Message = $"Unable to find currency: {currencyID.ToString()}." });
            }

            if (count < 1)
            {
                return BadRequest(new Error { Message = $"Count must be greater than 0." });
            }

            CurrencyModel currency = ChannelSession.Settings.Currency[currencyID];

            Dictionary<Guid, UserV2Model> allUsersDictionary = ChannelSession.Settings.Users.ToDictionary();

            IEnumerable<UserV2Model> allUsers = allUsersDictionary.Select(kvp => kvp.Value);
            allUsers = allUsers.Where(u => !u.IsSpecialtyExcluded);

            List<User> currencyUserList = new List<User>();
            foreach (UserV2Model currencyUser in allUsers.OrderByDescending(u => currency.GetAmount(u)).Take(count))
            {
                currencyUserList.Add(UserV1Controller.UserFromUserDataViewModel(new UserV2ViewModel(currencyUser)));
            }
            return Ok(currencyUserList);
        }

        [Route("{currencyID:guid}/give")]
        [HttpPost]
        public async Task<IActionResult> BulkGive(Guid currencyID, [FromBody] IEnumerable<GiveUserCurrency> giveDatas)
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            if (!ChannelSession.Settings.Currency.ContainsKey(currencyID))
            {
                return NotFound(new Error { Message = $"Unable to find currency: {currencyID.ToString()}." });
            }

            if (giveDatas == null)
            {
                return BadRequest(new Error { Message = $"Unable to parse array of give data from POST body." });
            }

            CurrencyModel currency = ChannelSession.Settings.Currency[currencyID];

            List<User> users = new List<User>();
            foreach (var giveData in giveDatas)
            {
                UserV2ViewModel user = await UserV1Controller.GetUserData(ChannelSession.Settings.DefaultStreamingPlatform, giveData.UsernameOrID);
                if (user != null && giveData.Amount > 0)
                {
                    currency.AddAmount(user, giveData.Amount);
                    users.Add(UserV1Controller.UserFromUserDataViewModel(user));
                }
            }

            return Ok(users);
        }

        public static CurrencyAmount CurrencyAmountFromUserCurrencyViewModel(CurrencyModel currency, int amount)
        {
            return new CurrencyAmount
            {
                ID = currency.ID,
                Name = currency.Name,
                Amount = amount
            };
        }

        public static Currency CurrencyFromUserCurrencyViewModel(CurrencyModel currency)
        {
            return new Currency
            {
                ID = currency.ID,
                Name = currency.Name
            };
        }
    }
}