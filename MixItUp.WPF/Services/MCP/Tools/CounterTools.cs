using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.Tools
{
    /// <summary>
    /// Counters are addressed by name rather than GUID, and names are matched case-insensitively
    /// because Mix It Up stores them lowercased.
    /// </summary>
    [McpServerToolType]
    public class CounterTools
    {
        [McpServerTool(Name = "list_counters", ReadOnly = true, UseStructuredContent = true)]
        [Description("List every counter configured in Mix It Up with its current amount. Counters are addressed by name, so this is how you discover the names the other counter tools expect.")]
        public static Task<ListCountersResult> ListCounters()
        {
            return ToolHelpers.RunWithTimeout("list_counters", () =>
            {
                Dictionary<string, CounterModel> counters = ToolHelpers.SnapshotCounters();

                return Task.FromResult(new ListCountersResult()
                {
                    TotalCount = counters.Count,
                    Counters = counters.OrderBy(c => c.Key).Select(c => ToResult(c.Value)).ToList(),
                });
            });
        }

        [McpServerTool(Name = "get_counter", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get a single counter by name. Use list_counters to discover valid names.")]
        public static Task<CounterResult> GetCounter(
            [Description("The name of the counter.")] string counterName)
        {
            return ToolHelpers.RunWithTimeout("get_counter", () =>
            {
                return Task.FromResult(ToResult(ToolHelpers.GetCounterOrThrow(counterName)));
            });
        }

        [McpServerTool(Name = "create_counter", ReadOnly = false, Destructive = false, Idempotent = false, UseStructuredContent = true)]
        [Description("Create a new counter starting at zero. Fails if a counter with that name already exists. This changes the local settings profile only and has no effect on any streaming platform.")]
        public static Task<CounterResult> CreateCounter(
            [Description("The name for the new counter. Counter names are stored lowercased.")] string counterName)
        {
            return ToolHelpers.RunWithTimeout("create_counter", () =>
            {
                ToolHelpers.RequireSettingsLoaded();

                if (string.IsNullOrWhiteSpace(counterName))
                {
                    throw new McpException("counterName cannot be empty.");
                }

                string name = counterName.ToLower();
                if (ChannelSession.Settings.Counters.ContainsKey(name))
                {
                    throw new McpException($"A counter named '{counterName}' already exists. Call get_counter to read it, or set_counter to change its amount.");
                }

                // saveToFile and resetOnLoad match the Developer API's create: a counter created
                // through an integration is an in-memory value unless the streamer opts into the
                // file mirror through the UI.
                CounterModel.CreateCounter(counterName, false, false);

                CounterModel counter = ToolHelpers.GetCounterOrThrow(name);

                ToolHelpers.LogToolAction("create_counter", $"created counter '{counter.Name}'");

                return Task.FromResult(ToResult(counter));
            });
        }

        [McpServerTool(Name = "set_counter", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Set a counter to an exact amount, discarding its current value. Counters can be shown on overlays and read by commands, so a change here can be visible on stream. To add to the current amount instead, use update_counter.")]
        public static Task<CounterResult> SetCounter(
            [Description("The name of the counter.")] string counterName,
            [Description("The exact amount to set the counter to.")] double amount)
        {
            return ToolHelpers.RunWithTimeout("set_counter", async () =>
            {
                CounterModel counter = ToolHelpers.GetCounterOrThrow(counterName);

                double previous = counter.Amount;
                await counter.SetAmount(amount);

                ToolHelpers.LogToolAction("set_counter", $"'{counter.Name}' set from {previous} to {counter.Amount}");

                return ToResult(counter);
            });
        }

        [McpServerTool(Name = "update_counter", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true)]
        [Description("Add an amount to a counter's current value. Pass a negative amount to subtract. Counters can be shown on overlays and read by commands, so a change here can be visible on stream. To replace the value outright, use set_counter.")]
        public static Task<CounterResult> UpdateCounter(
            [Description("The name of the counter.")] string counterName,
            [Description("The amount to add to the current value. Negative values subtract. Defaults to 1.")] double amount = 1)
        {
            return ToolHelpers.RunWithTimeout("update_counter", async () =>
            {
                CounterModel counter = ToolHelpers.GetCounterOrThrow(counterName);

                double previous = counter.Amount;
                await counter.UpdateAmount(amount);

                ToolHelpers.LogToolAction("update_counter", $"'{counter.Name}' changed by {amount}, from {previous} to {counter.Amount}");

                return ToResult(counter);
            });
        }

        [McpServerTool(Name = "reset_counter", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Reset a counter to zero, discarding its current value. Counters can be shown on overlays and read by commands, so a reset can be visible on stream.")]
        public static Task<CounterResult> ResetCounter(
            [Description("The name of the counter.")] string counterName)
        {
            return ToolHelpers.RunWithTimeout("reset_counter", async () =>
            {
                CounterModel counter = ToolHelpers.GetCounterOrThrow(counterName);

                double previous = counter.Amount;
                await counter.ResetAmount();

                ToolHelpers.LogToolAction("reset_counter", $"'{counter.Name}' reset from {previous} to 0");

                return ToResult(counter);
            });
        }

        [McpServerTool(Name = "delete_counter", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
        [Description("Permanently delete a counter and its value. This cannot be undone, and any command or overlay that reads the counter will stop finding it.")]
        public static Task<ActionResult> DeleteCounter(
            [Description("The name of the counter to delete.")] string counterName)
        {
            return ToolHelpers.RunWithTimeout("delete_counter", () =>
            {
                CounterModel counter = ToolHelpers.GetCounterOrThrow(counterName);

                ChannelSession.Settings.Counters.Remove(counter.Name.ToLower());

                ToolHelpers.LogToolAction("delete_counter", $"deleted counter '{counter.Name}' which held {counter.Amount}");

                return Task.FromResult(new ActionResult($"Deleted counter '{counter.Name}'."));
            });
        }

        private static CounterResult ToResult(CounterModel counter)
        {
            return new CounterResult()
            {
                Name = counter.Name,
                Amount = counter.Amount,
                SaveToFile = counter.SaveToFile,
                ResetOnLoad = counter.ResetOnLoad,
            };
        }

        public class ListCountersResult
        {
            [Description("Total number of counters configured.")]
            public int TotalCount { get; set; }

            [Description("Every counter, ordered by name.")]
            public List<CounterResult> Counters { get; set; } = new List<CounterResult>();
        }

        public class CounterResult
        {
            [Description("The name of the counter, used to address it in the other counter tools.")]
            public string Name { get; set; }

            [Description("The counter's current amount.")]
            public double Amount { get; set; }

            [Description("Whether the counter mirrors its value to a text file for use in stream software.")]
            public bool SaveToFile { get; set; }

            [Description("Whether the counter resets to zero each time Mix It Up loads.")]
            public bool ResetOnLoad { get; set; }
        }
    }
}
