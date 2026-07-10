using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls
{
    /// <summary>
    /// Interaction logic for IconButton.xaml
    /// </summary>
    public partial class IconButton : Button
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                "Icon",
                typeof(string),
                typeof(IconButton),
                new FrameworkPropertyMetadata(
                    "delete",
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    new PropertyChangedCallback(OnIconChanged)
                ));

        public IconButton()
        {
            InitializeComponent();
        }

        /// <summary>The Material Symbols icon name (e.g. "help") shown in the button.</summary>
        public string Icon
        {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            IconButton iconButton = (IconButton)d;
            if (iconButton != null && iconButton.ButtonIcon != null)
            {
                iconButton.ButtonIcon.IconName = iconButton.Icon;
            }
        }
    }
}
