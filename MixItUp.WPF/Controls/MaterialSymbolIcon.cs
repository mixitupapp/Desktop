using MixItUp.WPF.Branding;
using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls
{
    /// <summary>
    /// Renders a Material Design 3 icon glyph using the embedded Material Symbols Sharp font. Set
    /// IconName to a Material Symbols icon name (e.g. "flash_on"), or bind Text directly to a
    /// MaterialSymbols glyph constant.
    /// </summary>
    public class MaterialSymbolIcon : TextBlock
    {
        public static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(
            nameof(IconName), typeof(string), typeof(MaterialSymbolIcon), new PropertyMetadata(null, OnIconNameChanged));

        public string IconName
        {
            get { return (string)this.GetValue(IconNameProperty); }
            set { this.SetValue(IconNameProperty, value); }
        }

        static MaterialSymbolIcon()
        {
            FontSizeProperty.OverrideMetadata(typeof(MaterialSymbolIcon), new FrameworkPropertyMetadata((d, e) => ((MaterialSymbolIcon)d).LineHeight = (double)e.NewValue));
        }

        public MaterialSymbolIcon()
        {
            this.FontFamily = MaterialSymbols.Font;
            this.TextAlignment = TextAlignment.Center;

            // The font's line height is 1.2em; lock the line box to the font size so the icon's
            // layout bounds stay square and don't overflow tight containers (e.g. GroupBox headers).
            this.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            this.LineHeight = this.FontSize;
        }

        private static void OnIconNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MaterialSymbolIcon)d).Text = MaterialSymbols.GetGlyph(e.NewValue as string) ?? string.Empty;
        }
    }
}
