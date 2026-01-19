using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MixItUp.WPF.Controls
{
    public partial class ColorPickerTextBox : UserControl
    {
        public static readonly DependencyProperty ColorTextProperty =
            DependencyProperty.Register(
                "ColorText",
                typeof(string),
                typeof(ColorPickerTextBox),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnColorTextChanged));

        public static readonly DependencyProperty HintTextProperty =
            DependencyProperty.Register(
                "HintText",
                typeof(string),
                typeof(ColorPickerTextBox),
                new PropertyMetadata(string.Empty));

        private Color _currentPickerColor = Colors.Black;
        private bool _isUpdatingFromPicker = false;
        private bool _isUpdatingFromHex = false;

        public ColorPickerTextBox()
        {
            InitializeComponent();
        }

        public string ColorText
        {
            get { return (string)GetValue(ColorTextProperty); }
            set { SetValue(ColorTextProperty, value); }
        }

        public string HintText
        {
            get { return (string)GetValue(HintTextProperty); }
            set { SetValue(HintTextProperty, value); }
        }

        private static void OnColorTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ColorPickerTextBox control = (ColorPickerTextBox)d;
            control.UpdateColorPreview();
        }

        private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            Color? currentColor = ParseColorString(ColorText);
            if (currentColor.HasValue)
            {
                _currentPickerColor = currentColor.Value;
            }
            else
            {
                _currentPickerColor = Colors.Black;
            }

            SquarePicker.SelectedColor = _currentPickerColor;
            UpdateCurrentColorPreview();
            HexTextBox.Text = ColorToHex(_currentPickerColor);

            ColorPickerPopup.IsOpen = true;
        }

        private void SquarePicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFromHex) return;

            _isUpdatingFromPicker = true;
            _currentPickerColor = SquarePicker.SelectedColor;
            UpdateCurrentColorPreview();
            HexTextBox.Text = ColorToHex(_currentPickerColor);
            _isUpdatingFromPicker = false;
        }

        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromPicker) return;

            string hexText = HexTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(hexText)) return;

            if (!hexText.StartsWith("#"))
            {
                hexText = "#" + hexText;
            }

            Color? parsed = ParseColorString(hexText);
            if (parsed.HasValue)
            {
                _isUpdatingFromHex = true;
                _currentPickerColor = parsed.Value;
                SquarePicker.SelectedColor = _currentPickerColor;
                UpdateCurrentColorPreview();
                _isUpdatingFromHex = false;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ColorText = ColorToHex(_currentPickerColor);
            ColorPickerPopup.IsOpen = false;
        }

        private void UpdateCurrentColorPreview()
        {
            CurrentColorPreview.Background = new SolidColorBrush(_currentPickerColor);
        }

        private void UpdateColorPreview()
        {
            Color? color = ParseColorString(ColorText);
            if (color.HasValue)
            {
                ColorPreviewBorder.Background = new SolidColorBrush(color.Value);
            }
            else
            {
                ColorPreviewBorder.Background = Brushes.Transparent;
            }
        }

        private string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private Color? ParseColorString(string colorString)
        {
            if (string.IsNullOrWhiteSpace(colorString))
            {
                return null;
            }

            try
            {
                colorString = colorString.Trim();

                if (colorString.StartsWith("#"))
                {
                    return (Color)ColorConverter.ConvertFromString(colorString);
                }

                if (colorString.StartsWith("rgba", StringComparison.OrdinalIgnoreCase) ||
                    colorString.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseRgbaString(colorString);
                }

                object result = ColorConverter.ConvertFromString(colorString);
                if (result != null)
                {
                    return (Color)result;
                }
            }
            catch
            {
            }

            return null;
        }

        private Color? ParseRgbaString(string rgbaString)
        {
            try
            {
                rgbaString = rgbaString.Trim();
                bool hasAlpha = rgbaString.StartsWith("rgba", StringComparison.OrdinalIgnoreCase);

                int startIndex = rgbaString.IndexOf('(');
                int endIndex = rgbaString.LastIndexOf(')');
                if (startIndex < 0 || endIndex < 0)
                {
                    return null;
                }

                string values = rgbaString.Substring(startIndex + 1, endIndex - startIndex - 1);
                string[] parts = values.Split(',');

                if (parts.Length < 3)
                {
                    return null;
                }

                byte r = byte.Parse(parts[0].Trim());
                byte g = byte.Parse(parts[1].Trim());
                byte b = byte.Parse(parts[2].Trim());
                byte a = 255;

                if (hasAlpha && parts.Length >= 4)
                {
                    double alphaValue = double.Parse(parts[3].Trim(), CultureInfo.InvariantCulture);
                    if (alphaValue <= 1.0)
                    {
                        a = (byte)(alphaValue * 255);
                    }
                    else
                    {
                        a = (byte)alphaValue;
                    }
                }

                return Color.FromArgb(a, r, g, b);
            }
            catch
            {
                return null;
            }
        }
    }
}
