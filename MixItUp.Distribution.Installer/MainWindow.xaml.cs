using System.Diagnostics;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Navigation;
using MaterialDesignThemes.Wpf;

namespace MixItUp.Distribution.Installer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this.viewModel = new MainWindowViewModel();
            this.viewModel.PolicyAcceptanceHandler = this.ShowPolicyDialogAsync;

            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await this.viewModel.RunAsync();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            string path = (e.Uri.IsAbsoluteUri) ? e.Uri.AbsoluteUri : e.Uri.OriginalString;
            ProcessStartInfo processInfo = new ProcessStartInfo(path) { UseShellExecute = true };
            Process.Start(processInfo);
            e.Handled = true;
        }

        private async Task<bool> ShowPolicyDialogAsync(IReadOnlyList<PolicyDocumentViewModel> policies)
        {
            if (policies == null || policies.Count == 0)
            {
                return true;
            }

            PolicyDialog dialog = new PolicyDialog
            {
                DataContext = new PolicyDialogViewModel(policies),
            };

            object result = await DialogHost.Show(dialog);
            return result is bool accepted && accepted;
        }
    }
}

