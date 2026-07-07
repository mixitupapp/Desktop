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
        private const int MinimizedGroupBoxHeight = 35;

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
            this.CategoryGroupBox.Height = MinimizedGroupBoxHeight;
            this.ExpandIcon.IconName = "chevron_right";
        }

        public void Expand()
        {
            this.CategoryGroupBox.Height = Double.NaN;
            this.ExpandIcon.IconName = "expand_more";
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