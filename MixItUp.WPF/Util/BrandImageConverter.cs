using MixItUp.WPF.Branding;
using System;
using System.Globalization;
using System.Windows.Data;

namespace MixItUp.WPF.Util
{
    /// <summary>
    /// Converts a Brand (or brand ID string) to the theme-appropriate brand mark ImageSource.
    /// Defaults to the full-color symbol at small (64px) size; the converter parameter may combine
    /// "Mono" (monochrome set), "Medium" (128px size), and "OnPrimary" (resolve dark/light against
    /// the primary surface foreground instead of the theme mode), e.g. "Mono|OnPrimary".
    /// </summary>
    public class BrandImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Brand brand = value as Brand ?? Brands.Get(value as string);
            if (brand == null)
            {
                return null;
            }

            string options = parameter as string ?? string.Empty;
            BrandImageSet set = options.IndexOf("Mono", StringComparison.OrdinalIgnoreCase) >= 0 ? brand.Mono : brand.Symbol;
            BrandImageVariant variant = options.IndexOf("OnPrimary", StringComparison.OrdinalIgnoreCase) >= 0 ? set.OnPrimary : set.Current;
            return options.IndexOf("Medium", StringComparison.OrdinalIgnoreCase) >= 0 ? variant.Medium : variant.Small;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
