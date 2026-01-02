using MixItUp.API.V1.Models;
using MixItUp.Base;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSocketSharp;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V1
{
    [Route("api/quotes")]
    [ApiController]
    public class QuotesV1Controller : ControllerBase
    {
        [HttpGet]
        public IEnumerable<Quote> Get()
        {
            return ChannelSession.Settings.Quotes.Select(q => QuoteFromUserQuoteViewModel(q));
        }

        [Route("{quoteID:int}")]
        [HttpGet]
        public IActionResult Get(int quoteID)
        {
            var quote = ChannelSession.Settings.Quotes.FirstOrDefault(q => q.ID == quoteID);
            if (quote == null)
            {
                return NotFound(new Error { Message = $"Unable to find quote: {quoteID}." });
            }
            return Ok(QuoteFromUserQuoteViewModel(quote));
        }

        [HttpPut]
        public async Task<IActionResult> Add([FromBody] AddQuote quote)
        {
            if (quote == null || quote.QuoteText.IsNullOrEmpty())
            {
                return BadRequest(new Error { Message = $"Unable to create quote, no QuoteText was supplied." });
            }

            var quoteText = quote.QuoteText.Trim(new char[] { ' ', '\'', '\"' });
            UserQuoteModel newQuote = new UserQuoteModel(UserQuoteViewModel.GetNextQuoteNumber(), quoteText, DateTimeOffset.Now, await GamePreMadeChatCommandModel.GetCurrentGameName(ChannelSession.Settings.DefaultStreamingPlatform));
            ChannelSession.Settings.Quotes.Add(newQuote);
            await ChannelSession.SaveSettings();
            UserQuoteModel.QuoteAdded(newQuote);
            return Ok(QuoteFromUserQuoteViewModel(newQuote));
        }

        public static Quote QuoteFromUserQuoteViewModel(UserQuoteModel quoteData)
        {
            return new Quote
            {
                ID = quoteData.ID,
                DateTime = quoteData.DateTime,
                GameName = quoteData.GameName,
                QuoteText = quoteData.Quote,
            };
        }
    }
}