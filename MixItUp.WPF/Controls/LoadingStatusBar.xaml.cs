using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls
{
    /// <summary>
    /// Interaction logic for LoadingStatusBar.xaml
    /// </summary>
    public partial class LoadingStatusBar : UserControl
    {
        public static readonly DependencyProperty BarHeightProperty =
            DependencyProperty.Register(nameof(BarHeight), typeof(double), typeof(LoadingStatusBar), new PropertyMetadata(4.0));

        public double BarHeight
        {
            get => (double)GetValue(BarHeightProperty);
            set => SetValue(BarHeightProperty, value);
        }

        public static readonly DependencyProperty BarColorProperty =
            DependencyProperty.Register(nameof(BarColor), typeof(System.Windows.Media.Brush), typeof(LoadingStatusBar),
                new PropertyMetadata(null, (d, e) => { if (d is LoadingStatusBar b && e.NewValue is System.Windows.Media.Brush br) b.StatusBar.Foreground = br; }));

        public System.Windows.Media.Brush BarColor
        {
            get => (System.Windows.Media.Brush)GetValue(BarColorProperty);
            set => SetValue(BarColorProperty, value);
        }

        public static readonly DependencyProperty BarBackgroundProperty =
            DependencyProperty.Register(nameof(BarBackground), typeof(System.Windows.Media.Brush), typeof(LoadingStatusBar),
                new PropertyMetadata(null, (d, e) => { if (d is LoadingStatusBar b && e.NewValue is System.Windows.Media.Brush br) b.StatusBar.Background = br; }));

        public System.Windows.Media.Brush BarBackground
        {
            get => (System.Windows.Media.Brush)GetValue(BarBackgroundProperty);
            set => SetValue(BarBackgroundProperty, value);
        }

        public LoadingStatusBar()
        {
            InitializeComponent();
        }

        public void ShowProgressBar() { this.StatusBar.Visibility = Visibility.Visible; }

        public void HideProgressBar() { this.StatusBar.Visibility = Visibility.Hidden; }
    }
}
