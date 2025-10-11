using MixItUp.API.V2.Models;
using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V2
{
    [Route("api/v2/counters")]
    [ApiController]
    public class CountersV2Controller : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAllCounters()
        {
            var result = new GetListOfCountersResponse();
            result.TotalCount = ChannelSession.Settings.Counters.Count;

            foreach (var kvp in ChannelSession.Settings.Counters.OrderBy(c => c.Key))
            {
                result.Counters.Add(CounterMapper.ToCounter(kvp.Value));
            }

            return Ok(result);
        }

        [Route("{counterName}")]
        [HttpGet]
        public IActionResult GetCounterByName(string counterName)
        {
            string name = counterName.ToLower();

            if (!ChannelSession.Settings.Counters.TryGetValue(name, out var counter) || counter == null)
            {
                return NotFound();
            }

            return Ok(new GetSingleCounterResponse { Counter = CounterMapper.ToCounter(counter) });
        }

        [HttpPost]
        public IActionResult CreateCounter([FromBody] CreateCounterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Counter name is required");
            }

            string counterName = request.Name.ToLower();

            if (ChannelSession.Settings.Counters.ContainsKey(counterName))
            {
                return BadRequest($"Counter '{request.Name}' already exists");
            }

            CounterModel.CreateCounter(request.Name, false, false);

            var counter = ChannelSession.Settings.Counters[counterName];

            return Ok(new GetSingleCounterResponse { Counter = CounterMapper.ToCounter(counter) });
        }

        [Route("{counterName}/set")]
        [HttpPost]
        public async Task<IActionResult> SetCounter(string counterName, [FromBody] SetCounterRequest request)
        {
            string name = counterName.ToLower();

            if (!ChannelSession.Settings.Counters.TryGetValue(name, out var counter) || counter == null)
            {
                return NotFound();
            }

            if (!request.Amount.HasValue)
            {
                return BadRequest("Amount is required");
            }

            await counter.SetAmount(request.Amount.Value);

            return Ok(new GetSingleCounterResponse { Counter = CounterMapper.ToCounter(counter) });
        }

        [Route("{counterName}/update")]
        [HttpPost]
        public async Task<IActionResult> UpdateCounter(string counterName, [FromBody] UpdateCounterRequest request)
        {
            string name = counterName.ToLower();

            if (!ChannelSession.Settings.Counters.TryGetValue(name, out var counter) || counter == null)
            {
                return NotFound();
            }

            double amount = request.Amount ?? 1;
            await counter.UpdateAmount(amount);

            return Ok(new GetSingleCounterResponse { Counter = CounterMapper.ToCounter(counter) });
        }

        [Route("{counterName}/reset")]
        [HttpPost]
        public async Task<IActionResult> ResetCounter(string counterName)
        {
            string name = counterName.ToLower();

            if (!ChannelSession.Settings.Counters.TryGetValue(name, out var counter) || counter == null)
            {
                return NotFound();
            }

            await counter.ResetAmount();

            return Ok(new GetSingleCounterResponse { Counter = CounterMapper.ToCounter(counter) });
        }

        [Route("{counterName}")]
        [HttpDelete]
        public IActionResult DeleteCounter(string counterName)
        {
            string name = counterName.ToLower();

            if (!ChannelSession.Settings.Counters.TryGetValue(name, out var counter) || counter == null)
            {
                return NotFound();
            }

            ChannelSession.Settings.Counters.Remove(name);

            return Ok();
        }
    }

    public static class CounterMapper
    {
        public static Counter ToCounter(CounterModel model)
        {
            return new Counter
            {
                Name = model.Name,
                Amount = model.Amount
            };
        }
    }
}