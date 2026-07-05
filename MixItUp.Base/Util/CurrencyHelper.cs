using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MixItUp.Base.Util
{
    public static class CurrencyHelper
    {
        private static readonly Dictionary<string, NumberFormatInfo> FormatsByISOCode = new Dictionary<string, NumberFormatInfo>(StringComparer.OrdinalIgnoreCase);

        public static string ToCurrencyString(double amount) { return CurrencyHelper.ToCurrencyString(null, amount); }

        public static string ToCurrencyString(string code, double amount)
        {
            amount = Math.Round(amount, 2);
            if (!string.IsNullOrEmpty(code))
            {
                NumberFormatInfo format = CurrencyHelper.GetCurrencyFormat(code);
                if (format != null)
                {
                    return amount.ToString("C", format);
                }
            }
            return amount.ToString("C2");
        }

        public static bool ParseCurrency(this string str, out double result)
        {
            // First try the current culture and then the invariant culture if that fails.
            if (!double.TryParse(str, NumberStyles.Currency, NumberFormatInfo.CurrentInfo, out result))
            {
                return double.TryParse(str, NumberStyles.Currency, NumberFormatInfo.InvariantInfo, out result);
            }
            return true;
        }

        private static NumberFormatInfo GetCurrencyFormat(string code)
        {
            if (!CurrencyHelper.FormatsByISOCode.TryGetValue(code, out NumberFormatInfo format))
            {
                // The user's formatting conventions, but with the ISO 4217 currency's own symbol
                // and decimal digits (e.g. JPY has 0), taken from a culture native to that currency
                CultureInfo native = CultureInfo.GetCultures(CultureTypes.SpecificCultures).FirstOrDefault(c =>
                {
                    try { return string.Equals(new RegionInfo(c.Name).ISOCurrencySymbol, code, StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                });

                if (native != null)
                {
                    format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
                    format.CurrencySymbol = native.NumberFormat.CurrencySymbol;
                    format.CurrencyDecimalDigits = native.NumberFormat.CurrencyDecimalDigits;
                }
                CurrencyHelper.FormatsByISOCode[code] = format;
            }
            return format;
        }
    }
}
