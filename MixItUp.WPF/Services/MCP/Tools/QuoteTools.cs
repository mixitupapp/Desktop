using MixItUp.Base;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.User;
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
    /// Quotes exist only in the v1 Developer API; there is no v2 quotes controller.
    /// </summary>
    [McpServerToolType]
    public class QuoteTools
    {
        [McpServerTool(Name = "list_quotes", ReadOnly = true, UseStructuredContent = true)]
        [Description("List the quotes saved in Mix It Up, newest first. Returns the quote numbers that get_quote expects.")]
        public static Task<ListQuotesResult> ListQuotes(
            [Description("Case-insensitive substring to match against the quote text. Omit to list all quotes.")] string textFilter = null,
            [Description("Number of quotes to skip, for paging. Defaults to 0.")] int skip = 0,
            [Description("Maximum number of quotes to return. Defaults to 25, maximum 200.")] int pageSize = 25)
        {
            return ToolHelpers.RunWithTimeout("list_quotes", () =>
            {
                ToolHelpers.RequireSettingsLoaded();
                ToolHelpers.RequireSkip(skip);
                ToolHelpers.RequirePageSize(pageSize);

                IEnumerable<UserQuoteModel> quotes = ChannelSession.Settings.Quotes.ToList();

                if (!string.IsNullOrEmpty(textFilter))
                {
                    quotes = quotes.Where(q => q.Quote != null && q.Quote.IndexOf(textFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                List<UserQuoteModel> matched = quotes.ToList();

                return Task.FromResult(new ListQuotesResult()
                {
                    TotalCount = matched.Count,
                    Quotes = matched.OrderByDescending(q => q.ID).Skip(skip).Take(pageSize).Select(ToResult).ToList(),
                });
            });
        }

        [McpServerTool(Name = "get_quote", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get a single quote by its number. Use list_quotes to find valid numbers.")]
        public static Task<QuoteResult> GetQuote(
            [Description("The quote number, as shown by list_quotes.")] int quoteId)
        {
            return ToolHelpers.RunWithTimeout("get_quote", () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                UserQuoteModel quote = ChannelSession.Settings.Quotes.FirstOrDefault(q => q.ID == quoteId);
                if (quote == null)
                {
                    throw new McpException($"No quote found with number {quoteId}. Call list_quotes to see valid numbers.");
                }

                return Task.FromResult(ToResult(quote));
            });
        }

        // OpenWorld because the game name is read from the live platform when one is connected.
        // Additive rather than destructive: it appends a quote and changes nothing existing.
        [McpServerTool(Name = "add_quote", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true)]
        [Description("Save a new quote. The quote is stamped with the current time and the game currently set on the default platform, and settings are written to disk immediately. This adds to the local quote list; it does not announce anything in chat.")]
        public static Task<QuoteResult> AddQuote(
            [Description("The text of the quote. Surrounding quotation marks and whitespace are trimmed.")] string quoteText)
        {
            return ToolHelpers.RunWithTimeout("add_quote", async () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                if (string.IsNullOrWhiteSpace(quoteText))
                {
                    throw new McpException("quoteText cannot be empty.");
                }

                string text = quoteText.Trim(new char[] { ' ', '\'', '\"' });

                UserQuoteModel quote = new UserQuoteModel(
                    UserQuoteViewModel.GetNextQuoteNumber(),
                    text,
                    DateTimeOffset.Now,
                    await GamePreMadeChatCommandModel.GetCurrentGameName(ChannelSession.Settings.DefaultStreamingPlatform));

                ChannelSession.Settings.Quotes.Add(quote);
                await ChannelSession.SaveSettings();
                UserQuoteModel.QuoteAdded(quote);

                ToolHelpers.LogToolAction("add_quote", $"added quote #{quote.ID}: \"{quote.Quote}\"");

                return ToResult(quote);
            });
        }

        private static QuoteResult ToResult(UserQuoteModel quote)
        {
            return new QuoteResult()
            {
                ID = quote.ID,
                QuoteText = quote.Quote,
                DateTime = quote.DateTime,
                GameName = quote.GameName,
            };
        }

        public class ListQuotesResult
        {
            [Description("Total number of quotes matching the filter, before paging.")]
            public int TotalCount { get; set; }

            [Description("The page of quotes requested, newest first.")]
            public List<QuoteResult> Quotes { get; set; } = new List<QuoteResult>();
        }

        public class QuoteResult
        {
            [Description("The quote number, used to address it in get_quote.")]
            public int ID { get; set; }

            [Description("The text of the quote.")]
            public string QuoteText { get; set; }

            [Description("When the quote was saved.")]
            public DateTimeOffset DateTime { get; set; }

            [Description("The game that was being played when the quote was saved, if any.")]
            public string GameName { get; set; }
        }
    }
}
