using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MixItUp.Base.Model.Actions
{
    [DataContract]
    public class SpecialIdentifierActionModel : ActionModelBase
    {
        private static readonly TimeSpan CountFunctionTimeout = TimeSpan.FromSeconds(1);

        [DataMember]
        public string SpecialIdentifierName { get; set; }

        [DataMember]
        public string ReplacementText { get; set; }

        [DataMember]
        public bool MakeGloballyUsable { get; set; }

        [DataMember]
        public bool ShouldProcessMath { get; set; }

        [DataMember]
        public bool ReplaceSpecialIdentifiersInFunctions { get; set; }

        public SpecialIdentifierActionModel(string specialIdentifierName, string replacementText, bool makeGloballyUsable, bool shouldProcessMath, bool replaceSpecialIdentifiersInFunctions)
            : base(ActionTypeEnum.SpecialIdentifier)
        {
            this.SpecialIdentifierName = specialIdentifierName;
            this.ReplacementText = replacementText;
            this.MakeGloballyUsable = makeGloballyUsable;
            this.ShouldProcessMath = shouldProcessMath;
            this.ReplaceSpecialIdentifiersInFunctions = replaceSpecialIdentifiersInFunctions;
        }

        [Obsolete]
        public SpecialIdentifierActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            string replacementText = this.ReplacementText;
            if (!this.ReplaceSpecialIdentifiersInFunctions)
            {
                replacementText = await ReplaceStringWithSpecialModifiers(replacementText, parameters);
            }

            if (replacementText.Contains("(") || replacementText.Contains(")"))
            {
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "removespaces", 1, (arguments) => { return Task.FromResult(arguments.First().Replace(" ", string.Empty)); });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "removecommas", 1, (arguments) => { return Task.FromResult(arguments.First().Replace(",", string.Empty)); });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "tolower", 1, (arguments) => { return Task.FromResult(arguments.First().ToLower()); });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "toupper", 1, (arguments) => { return Task.FromResult(arguments.First().ToUpper()); });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "length", 1, (arguments) => { return Task.FromResult(arguments.First().Length.ToString()); });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "urlencode", 1, (arguments) => { return Task.FromResult(HttpUtility.UrlEncode(arguments.First())); });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "urldecode", 1, (arguments) => { return Task.FromResult(HttpUtility.UrlDecode(arguments.First())); });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "uriescape", 1, (arguments) => { return Task.FromResult(Uri.EscapeDataString(arguments.First())); });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "replace", 3, (arguments) =>
                {
                    if (arguments.Count() == 3)
                    {
                        // A viewer can supply the text being searched for, and there is nothing to
                        // replace when it comes through empty
                        if (string.IsNullOrEmpty(arguments.ElementAt(1)))
                        {
                            return Task.FromResult(arguments.ElementAt(0));
                        }
                        return Task.FromResult(arguments.ElementAt(0).Replace(arguments.ElementAt(1), arguments.ElementAt(2)));
                    }
                    return Task.FromResult<string>(null);
                });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "count", 2, (arguments) =>
                {
                    if (arguments.Count() == 2)
                    {
                        try
                        {
                            return Task.FromResult(Regex.Matches(arguments.ElementAt(0), arguments.ElementAt(1), RegexOptions.None, CountFunctionTimeout).Count.ToString());
                        }
                        catch (ArgumentException)
                        {
                            // A viewer can put ( or [ into the pattern, which is not a valid regex
                            return Task.FromResult<string>(null);
                        }
                        catch (RegexMatchTimeoutException)
                        {
                            return Task.FromResult<string>(null);
                        }
                    }
                    return Task.FromResult<string>(null);
                });
                replacementText = await this.ProcessStringFunction(parameters, replacementText, "datefrom", 1, (arguments) =>
                {
                    if (arguments.Count() == 1)
                    {
                        try
                        {
                            DateTime targetDate;
                            if (!DateTime.TryParse(arguments.First(), out targetDate))
                            {
                                return Task.FromResult(MixItUp.Base.Resources.InvalidDate);
                            }

                            DateTime now = DateTime.Now;
                            TimeSpan difference = now - targetDate;

                            if (difference.TotalSeconds < 0)
                            {
                                return Task.FromResult(MixItUp.Base.Resources.DateNotOccurred);
                            }

                            return Task.FromResult(this.FormatDateDifference(targetDate, now));
                        }
                        catch
                        {
                            return Task.FromResult(MixItUp.Base.Resources.InvalidDate);
                        }
                    }
                    return Task.FromResult<string>(null);
                });

                replacementText = await this.ProcessStringFunction(parameters, replacementText, "dateto", 1, (arguments) =>
                {
                    if (arguments.Count() == 1)
                    {
                        try
                        {
                            DateTime targetDate;
                            if (!DateTime.TryParse(arguments.First(), out targetDate))
                            {
                                return Task.FromResult(MixItUp.Base.Resources.InvalidDate);
                            }

                            DateTime now = DateTime.Now;
                            TimeSpan difference = targetDate - now;

                            if (difference.TotalSeconds < 0)
                            {
                                return Task.FromResult(MixItUp.Base.Resources.DateHasPassed);
                            }

                            return Task.FromResult(this.FormatDateDifference(now, targetDate));
                        }
                        catch
                        {
                            return Task.FromResult(MixItUp.Base.Resources.InvalidDate);
                        }
                    }
                    return Task.FromResult<string>(null);
                });

            }

            if (this.ShouldProcessMath)
            {
                replacementText = MathHelper.ProcessMathEquation(replacementText).ToString();
            }

            if (this.ReplaceSpecialIdentifiersInFunctions)
            {
                replacementText = await ReplaceStringWithSpecialModifiers(replacementText, parameters);
            }

            if (this.MakeGloballyUsable)
            {
                SpecialIdentifierStringBuilder.AddGlobalSpecialIdentifier(this.SpecialIdentifierName, replacementText);
            }
            else
            {
                parameters.SpecialIdentifiers[this.SpecialIdentifierName] = replacementText;
            }
        }

        private async Task<string> ProcessStringFunction(CommandParametersModel parameters, string text, string functionName, int expectedArgumentNumber, Func<IEnumerable<string>, Task<string>> processor)
        {
            string functionStart = functionName + "(";

            // A processor can put the function name back into its own result, so bound the number of
            // expansions by the length of the text we started with
            int remainingPasses = text.Length;

            int index = text.IndexOf(functionStart);
            while (index >= 0 && remainingPasses-- > 0)
            {
                int rightIndex = this.FindFunctionEndIndex(text, index + functionName.Length);
                if (rightIndex < 0)
                {
                    // Nothing closes the call, leave the text exactly as it was found
                    break;
                }

                string result = await this.PerformStringFunction(parameters, text, functionName, expectedArgumentNumber, processor, index, rightIndex);
                if (string.Equals(result, text, StringComparison.Ordinal))
                {
                    // The call could not be processed, leave the text exactly as it was found
                    break;
                }

                // Successful match, reset and check again
                text = result;
                index = text.IndexOf(functionStart);
            }
            return text;
        }

        private int FindFunctionEndIndex(string text, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    depth++;
                }
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            // Viewers put unbalanced ( into chat, which never balances, so fall back to the last )
            // in the text as the end of the call
            int lastIndex = text.LastIndexOf(')');
            return lastIndex > openIndex ? lastIndex : -1;
        }

        private string FormatDateDifference(DateTime startDate, DateTime endDate)
        {
            int years = 0;
            DateTime yearCheckDate = startDate.AddYears(1);
            while (yearCheckDate <= endDate)
            {
                years++;
                yearCheckDate = startDate.AddYears(years + 1);
            }

            DateTime afterYears = startDate.AddYears(years);

            int months = 0;
            DateTime monthCheckDate = afterYears.AddMonths(1);
            while (monthCheckDate <= endDate)
            {
                months++;
                monthCheckDate = afterYears.AddMonths(months + 1);
            }

            DateTime afterMonths = afterYears.AddMonths(months);
            TimeSpan remainingDifference = endDate - afterMonths;

            int days = (int)remainingDifference.TotalDays;
            int hours = remainingDifference.Hours;
            int minutes = remainingDifference.Minutes;
            int seconds = remainingDifference.Seconds;

            List<string> parts = new List<string>();
            if (years > 0) parts.Add(years + " " + MixItUp.Base.Resources.TimeYears);
            if (months > 0) parts.Add(months + " " + MixItUp.Base.Resources.TimeMonths);
            if (days > 0) parts.Add(days + " " + MixItUp.Base.Resources.TimeDays);
            if (hours > 0) parts.Add(hours + " " + MixItUp.Base.Resources.TimeHours);
            if (minutes > 0) parts.Add(minutes + " " + MixItUp.Base.Resources.TimeMinutes);
            if (seconds > 0 || parts.Count == 0) parts.Add(seconds + " " + MixItUp.Base.Resources.Seconds);
            return string.Join(", ", parts);
        }

        private async Task<string> PerformStringFunction(CommandParametersModel parameters, string text, string functionName, int expectedArgumentNumber, Func<IEnumerable<string>, Task<string>> processor, int startIndex, int endIndex)
        {
            string functionText = text.Substring(startIndex, endIndex - startIndex + 1);
            string textToProcess = functionText.Substring(functionName.Length + 1, functionText.Length - functionName.Length - 2);

            List<string> arguments = new List<string>();
            if (expectedArgumentNumber > 1)
            {
                string[] splits = textToProcess.Split(new char[] { ',' });
                if (splits != null && splits.Length == expectedArgumentNumber)
                {
                    arguments.AddRange(splits);
                }
            }
            else
            {
                arguments.Add(textToProcess);
            }

            // Arguments failed to process, abort and leave the text as it was found
            if (arguments.Count == 0)
            {
                return text;
            }

            if (this.ReplaceSpecialIdentifiersInFunctions)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    arguments[i] = await ReplaceStringWithSpecialModifiers(arguments[i], parameters);
                }
            }

            string result = await processor(arguments);
            if (result == null)
            {
                // The processor rejected the arguments, leave the text as it was found
                return text;
            }

            return text.Replace(functionText, result);
        }


    }
}
