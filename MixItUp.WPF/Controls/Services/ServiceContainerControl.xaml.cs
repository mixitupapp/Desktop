using MixItUp.Base.ViewModels;
using MixItUp.WPF.Windows;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for ServiceContainerControl.xaml
    /// </summary>
    public partial class ServiceContainerControl : LoadingControlBase
    {
        private const int MinimizedGroupBoxHeight = 35;

        public LoadingWindowBase window { get; private set; }
        private ServiceControlBase serviceControl;

        public ServiceContainerControl(LoadingWindowBase window, ServiceControlBase serviceControl)
        {
            this.window = window;
            this.serviceControl = serviceControl;
            this.serviceControl.Initialize(this);
            this.DataContext = this.serviceControl.ViewModel;

            InitializeComponent();

            // Containers are rebuilt each time the Services page is shown, so the theme-appropriate
            // brand mark is resolved once at construction rather than tracking theme changes.
            if (this.serviceControl.Brand != null)
            {
                this.BrandImage.Source = this.serviceControl.Brand.Mono.OnPrimary.Small;
                this.BrandImage.Visibility = Visibility.Visible;
                this.BrandIconContainer.Visibility = Visibility.Visible;
            }
            else if (this.serviceControl.Feature != null)
            {
                this.FeatureIcon.IconName = this.serviceControl.Feature.IconName;
                this.FeatureIcon.Visibility = Visibility.Visible;
                this.BrandIconContainer.Visibility = Visibility.Visible;
            }

            this.InnerContentControl.Content = serviceControl;
        }

        public void Minimize() { this.GroupBox.Height = MinimizedGroupBoxHeight; }

        public void GroupBoxHeader_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.GroupBox.Height == MinimizedGroupBoxHeight)
            {
                this.GroupBox.Height = Double.NaN;
            }
            else
            {
                this.Minimize();
            }
        }

        protected override Task OnLoaded()
        {
            if (this.serviceControl.DataContext != null && this.serviceControl.DataContext is UIViewModelBase)
            {
                UIViewModelBase viewModel = (UIViewModelBase)this.serviceControl.DataContext;
                if (viewModel != null)
                {
                    viewModel.StartLoadingOperationOccurred += ViewModel_StartLoadingOperationOccurred;
                    viewModel.EndLoadingOperationOccurred += ViewModel_EndLoadingOperationOccurred;
                }
            }

            this.Minimize();

            return base.OnLoaded();
        }

        private void ViewModel_StartLoadingOperationOccurred(object sender, EventArgs e) { this.window.StartLoadingOperation(); }

        private void ViewModel_EndLoadingOperationOccurred(object sender, EventArgs e) { this.window.EndLoadingOperation(); }
    }
}
