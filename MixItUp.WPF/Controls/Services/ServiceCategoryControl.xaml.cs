using MixItUp.WPF.Branding;
using MixItUp.WPF.Windows;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for ServiceCategoryControl.xaml
    /// </summary>
    public partial class ServiceCategoryControl : LoadingControlBase
    {
        public string CategoryName { get; private set; }
        private LoadingWindowBase window;
        private List<ServiceContainerControl> services;

        public ServiceCategoryControl(LoadingWindowBase window, string categoryName, Feature feature = null)
        {
            this.window = window;
            this.CategoryName = categoryName;
            this.services = new List<ServiceContainerControl>();
            this.DataContext = this;

            InitializeComponent();

            if (feature != null)
            {
                this.CategoryIcon.IconName = feature.IconName;
                this.CategoryIcon.Visibility = Visibility.Visible;
            }
        }

        public void AddService(ServiceControlBase serviceControl)
        {
            ServiceContainerControl container = new ServiceContainerControl(this.window, serviceControl);
            this.services.Add(container);

            Border border = new Border();
            border.BorderBrush = (System.Windows.Media.Brush)this.FindResource("MaterialDesign.Brush.Foreground");
            border.BorderThickness = new Thickness(1);
            border.Child = container;

            this.ServicesPanel.Children.Add(border);
        }

        public void Minimize()
        {
            this.CategoryGroupBox.Minimize();
        }

        public void Expand()
        {
            this.CategoryGroupBox.Maximize();
        }

        private void CategoryGroupBox_Minimized(object sender, RoutedEventArgs e)
        {
            this.ExpandIcon.IconName = "chevron_right";
        }

        private void CategoryGroupBox_Maximized(object sender, RoutedEventArgs e)
        {
            this.ExpandIcon.IconName = "expand_more";
        }

        protected override Task OnLoaded()
        {
            this.Minimize();
            return base.OnLoaded();
        }
    }
}