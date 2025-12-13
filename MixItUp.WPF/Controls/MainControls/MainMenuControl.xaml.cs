using MaterialDesignThemes.Wpf;
using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.WPF.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MixItUp.WPF.Controls.MainControls
{
    public class MainMenuItem : NotifyPropertyChangedBase
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public MainControlBase Control { get; private set; }
        public string HelpLink { get; private set; }

        public bool Visible
        {
            get { return this.visible; }
            set
            {
                this.visible = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool visible = true;
        public bool CanHide { get; private set; }

        public Visibility HelpLinkVisibility { get { return (!string.IsNullOrEmpty(this.HelpLink)) ? Visibility.Visible : Visibility.Collapsed; } }

        public MainMenuItem(string id, string name, MainControlBase control, string helpLink = null, bool canHide = true)
        {
            this.Id = id;
            this.Name = name;
            this.Control = control;
            this.HelpLink = helpLink;
            this.CanHide = canHide;

            if (!canHide)
            {
                this.visible = true;
            }
        }
    }

    /// <summary>
    /// Interaction logic for MainMenuControl.xaml
    /// </summary>
    public partial class MainMenuControl : MainControlBase
    {
        public static event EventHandler<bool> OnMainMenuStateChanged = delegate { };

        private static readonly string DisconnectedServicesHeader = MixItUp.Base.Resources.ServiceDisconnectedLine1 + Environment.NewLine + MixItUp.Base.Resources.ServiceDisconnectedLine2;

        private HashSet<string> serviceDisconnections = new HashSet<string>();

        private ObservableCollection<MainMenuItem> visibleMenuItems = new ObservableCollection<MainMenuItem>();
        private ObservableCollection<MainMenuItem> orderedMenuItems = new ObservableCollection<MainMenuItem>();
        private Dictionary<string, MainMenuItem> menuItemLookup = new Dictionary<string, MainMenuItem>();

        private bool isEditMode = false;

        public MainMenuControl()
        {
            InitializeComponent();

            ServiceManager.OnServiceDisconnect += ServiceManager_OnServiceDisconnect;
            ServiceManager.OnServiceReconnect += ServiceManager_OnServiceReconnect;
        }

        public async Task<MainMenuItem> AddMenuItem(string name, MainControlBase control, string helpLink = null, bool canHide = true)
        {
            // Use the control type name as the ID (e.g., "ChatControl", "ChannelControl")
            string id = control.GetType().Name;

            await control.Initialize(this.Window);
            MainMenuItem item = new MainMenuItem(id, name, control, helpLink, canHide);

            this.menuItemLookup[id] = item;
            this.orderedMenuItems.Add(item);

            return item;
        }

        public void ShowMenuItem(MainMenuItem item)
        {
            if (!this.visibleMenuItems.Contains(item))
            {
                this.visibleMenuItems.Add(item);
            }
        }

        public void HideMenuItem(MainMenuItem item)
        {
            this.visibleMenuItems.Remove(item);
        }

        public void MenuItemSelected(string name)
        {
            MainMenuItem item = this.visibleMenuItems.FirstOrDefault(i => i.Name.Equals(name));
            if (item != null)
            {
                this.MenuItemSelected(item);
            }
        }

        public void MenuItemSelected(MainMenuItem item)
        {
            if (item.Control != null)
            {
                this.DataContext = item;
                this.ActiveControlContentControl.Content = item.Control;
            }
            this.MenuToggleButton.IsChecked = false;
        }

        protected override async Task InitializeInternal()
        {
            this.MenuItemsListBox.ItemsSource = this.visibleMenuItems;

            await this.MainSettings.Initialize(this.Window);

            this.NotificationCenter.Initialize();
            this.NotificationCenter.UnreadStatusChanged += NotificationCenter_UnreadStatusChanged;

            await base.InitializeInternal();
        }

        public void LoadMenuOrder()
        {
            List<MainMenuControlSettings> savedControls = ChannelSession.AppSettings.MainMenuControls;

            // Apply settings from App Settings
            if (savedControls != null && savedControls.Count > 0)
            {
                foreach (var controlSettings in savedControls)
                {
                    if (this.menuItemLookup.TryGetValue(controlSettings.Id, out MainMenuItem item))
                    {
                        item.Visible = !controlSettings.Hidden;
                    }
                }
            }

            this.visibleMenuItems.Clear();

            if (savedControls != null && savedControls.Count > 0)
            {
                // Add items in saved order (only if visible)
                foreach (var controlSettings in savedControls)
                {
                    if (this.menuItemLookup.TryGetValue(controlSettings.Id, out MainMenuItem item))
                    {
                        if (item.Visible && !this.visibleMenuItems.Contains(item))
                        {
                            this.visibleMenuItems.Add(item);
                        }
                    }
                }

                // Add any new items that weren't in saved order (new menu items added in updates; visible by default)
                var savedIds = savedControls.Select(c => c.Id).ToList();
                foreach (var kvp in this.menuItemLookup)
                {
                    if (!savedIds.Contains(kvp.Key))
                    {
                        kvp.Value.Visible = true;
                        if (!this.visibleMenuItems.Contains(kvp.Value))
                        {
                            this.visibleMenuItems.Add(kvp.Value);
                        }
                    }
                }
            }
            else
            {
                // No saved order - add all visible items
                foreach (var item in this.menuItemLookup.Values)
                {
                    if (item.Visible)
                    {
                        this.visibleMenuItems.Add(item);
                    }
                }
            }
        }

        public async Task SaveMenuOrder()
        {
            // Save the order of ALL items (including hidden ones) with their visibility state
            ChannelSession.AppSettings.MainMenuControls = this.orderedMenuItems
                .Select(i => new MainMenuControlSettings
                {
                    Id = i.Id,
                    Hidden = !i.Visible
                })
                .ToList();

            await ChannelSession.AppSettings.Save();
        }

        private void UIElement_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var dependencyObject = Mouse.Captured as DependencyObject;
            while (dependencyObject != null)
            {
                if (dependencyObject is ScrollBar)
                {
                    return;
                }
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }

            if (!this.isEditMode)
            {
                this.MenuToggleButton.IsChecked = false;
            }
        }

        private void MenuItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.MenuItemsListBox.SelectedIndex >= 0 && !this.isEditMode)
            {
                MainMenuItem item = (MainMenuItem)this.MenuItemsListBox.SelectedItem;
                this.MenuItemSelected(item);
            }
        }

        private void SectionHelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null && this.DataContext is MainMenuItem)
            {
                MainMenuItem menuItem = (MainMenuItem)this.DataContext;
                if (!string.IsNullOrEmpty(menuItem.HelpLink))
                {
                    ServiceManager.Get<IProcessService>().LaunchLink(menuItem.HelpLink);
                }
            }
        }

        private async void ServiceManager_OnServiceDisconnect(object sender, string serviceName)
        {
            if (!string.IsNullOrEmpty(ChannelSession.Settings.NotificationServiceDisconnectSoundFilePath))
            {
                await ServiceManager.Get<IAudioService>().PlayNotification(ChannelSession.Settings.NotificationServiceDisconnectSoundFilePath, ChannelSession.Settings.NotificationServiceDisconnectSoundVolume);
            }

            lock (this.serviceDisconnections)
            {
                this.serviceDisconnections.Add(serviceName);
            }
            this.RefreshServiceDisconnectionsAlertTooltip();
        }

        private async void ServiceManager_OnServiceReconnect(object sender, string serviceName)
        {
            if (!string.IsNullOrEmpty(ChannelSession.Settings.NotificationServiceConnectSoundFilePath))
            {
                await ServiceManager.Get<IAudioService>().PlayNotification(ChannelSession.Settings.NotificationServiceConnectSoundFilePath, ChannelSession.Settings.NotificationServiceConnectSoundVolume);
            }

            lock (this.serviceDisconnections)
            {
                this.serviceDisconnections.Remove(serviceName);
            }
            this.RefreshServiceDisconnectionsAlertTooltip();
        }

        private void RefreshServiceDisconnectionsAlertTooltip()
        {
            this.Dispatcher.Invoke(() =>
            {
                StringBuilder tooltip = new StringBuilder();
                tooltip.AppendLine(DisconnectedServicesHeader);
                lock (this.serviceDisconnections)
                {
                    foreach (string serviceName in this.serviceDisconnections.OrderBy(s => s))
                    {
                        tooltip.AppendLine();
                        tooltip.Append("- " + serviceName);
                    }
                    this.DisconnectionAlertButton.Visibility = (serviceDisconnections.Count == 0) ? Visibility.Collapsed : Visibility.Visible;
                }
                this.DisconnectionAlertButton.ToolTip = tooltip.ToString();
            });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            this.FlyoutMenuDialog.Visibility = Visibility.Collapsed;
            this.SettingsGrid.Visibility = Visibility.Visible;
        }

        private async void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Window.RunAsyncOperation(async () =>
            {
                await ChannelSession.AppSettings.Save();
                if (ChannelSession.AppSettings.SettingsChangeRestartRequired && await DialogHelper.ShowConfirmation(MixItUp.Base.Resources.SettingsChangedRestartPrompt))
                {
                    ((MainWindow)this.Window).Restart();
                }
                else
                {
                    this.FlyoutMenuDialog.Visibility = Visibility.Visible;
                    this.SettingsGrid.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void MenuToggleButton_Checked(object sender, RoutedEventArgs e) { OnMainMenuStateChanged(null, true); }

        private void MenuToggleButton_Unchecked(object sender, RoutedEventArgs e) { OnMainMenuStateChanged(null, false); }

        private void EditMenuButton_Checked(object sender, RoutedEventArgs e)
        {
            this.isEditMode = true;

            this.MenuItemsListBox.ItemsSource = this.orderedMenuItems;
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDragSource(this.MenuItemsListBox, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this.MenuItemsListBox, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this.MenuItemsListBox, MainMenuDragDropHandler.Instance);
        }

        private async void EditMenuButton_Unchecked(object sender, RoutedEventArgs e)
        {
            this.isEditMode = false;
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDragSource(this.MenuItemsListBox, false);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this.MenuItemsListBox, false);
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this.MenuItemsListBox, null);

            this.visibleMenuItems.Clear();
            foreach (var item in this.orderedMenuItems.Where(i => i.Visible))
            {
                this.visibleMenuItems.Add(item);
            }

            this.MenuItemsListBox.ItemsSource = this.visibleMenuItems;

            await this.SaveMenuOrder();
        }

        private async void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            if (!NotificationPopup.IsOpen)
            {
                await NotificationCenter.LoadNotifications();
                NotificationPopup.IsOpen = true;
            }
            else
            {
                NotificationPopup.IsOpen = false;
            }
        }

        private void NotificationCenter_UnreadStatusChanged(object sender, bool hasUnread)
        {
            if (hasUnread)
            {
                NotificationButton.Icon = PackIconKind.BellBadge;
                ((Storyboard)this.Resources["BellBlinkAnimation"]).Begin(NotificationButton);
            }
            else
            {
                NotificationButton.Icon = PackIconKind.Bell;
                NotificationButton.Opacity = 1.0;
            }
        }
    }
}