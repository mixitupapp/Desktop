using MixItUp.WPF.Windows;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for ServiceCategoryControl.xaml
    /// </summary>
    public partial class ServiceCategoryControl : LoadingControlBase
    {
        private const int MinimizedGroupBoxHeight = 35;

        public string CategoryName { get; private set; }
        private LoadingWindowBase window;
        private List<ServiceContainerControl> services;

        public ServiceCategoryControl(LoadingWindowBase window, string categoryName)
        {
            this.window = window;
            this.CategoryName = categoryName;
            this.services = new List<ServiceContainerControl>();
            this.DataContext = this;

            InitializeComponent();
        }

        public void AddService(ServiceControlBase serviceControl)
        {
            ServiceContainerControl container = new ServiceContainerControl(this.window, serviceControl);
            container.Margin = new Thickness(0, 0.5, 0, 0.5);
            this.services.Add(container);
            this.ServicesPanel.Children.Add(container);
        }

        public void Minimize()
        {
            this.CategoryGroupBox.Height = MinimizedGroupBoxHeight;
            this.ExpandIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.ChevronDown;
        }

        public void Expand()
        {
            this.CategoryGroupBox.Height = Double.NaN;
            this.ExpandIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.ChevronRight;
        }

        public void CategoryGroupBoxHeader_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.CategoryGroupBox.Height == MinimizedGroupBoxHeight)
            {
                this.Expand();
            }
            else
            {
                this.Minimize();
            }
        }

        protected override Task OnLoaded()
        {
            this.Minimize();
            return base.OnLoaded();
        }
    }
}