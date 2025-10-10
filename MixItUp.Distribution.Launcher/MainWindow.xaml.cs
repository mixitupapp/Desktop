using System.Windows;

namespace MixItUp.Distribution.Launcher
{
    public partial class MainWindow : Window
    {
        private readonly LauncherViewModel viewModel = new LauncherViewModel();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this.viewModel;
            this.Loaded += this.MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= this.MainWindow_Loaded;
            await this.viewModel.InitializeAsync();
        }
    }
}
