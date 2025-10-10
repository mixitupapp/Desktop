using System;
using System.Globalization;
using System.Windows.Data;

namespace MixItUp.Distribution.Launcher
{
    public sealed class BooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean)
            {
                return !boolean;
            }

            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean)
            {
                return !boolean;
            }

            return Binding.DoNothing;
        }
    }
}
