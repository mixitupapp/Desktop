using System.Windows;
using System.Windows.Input;

namespace MixItUp.Uninstaller
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();

            this.viewModel = new MainWindowViewModel();
            this.DataContext = this.viewModel;

            this.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            UninstallButton.IsEnabled = false;
            CancelButton.IsEnabled = false;

            bool success = await this.viewModel.RunUninstallAsync();

            if (success)
            {
                this.viewModel.ShowCompletionState();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
