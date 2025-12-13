using MixItUp.Base;
using MixItUp.Base.Model.API;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MixItUp.WPF.Controls
{
    public partial class NotificationCenterControl : UserControl
    {
        public ObservableCollection<NotificationItem> Notifications { get; set; }
        private bool isLoading = false;
        public event EventHandler<bool> UnreadStatusChanged;

        public NotificationCenterControl()
        {
            InitializeComponent();

            Notifications = new ObservableCollection<NotificationItem>();
            NotificationsItemsControl.ItemsSource = Notifications;
        }

        public void Initialize()
        {
            ServiceManager.Get<MixItUpService>().NotificationStatusChanged += MixItUpService_NotificationStatusChanged;
            UnreadStatusChanged?.Invoke(this, ServiceManager.Get<MixItUpService>().HasUnreadNotifications);
        }

        private void MixItUpService_NotificationStatusChanged(object sender, bool hasUnread)
        {
            Application.Current?.Dispatcher.Invoke(() => UnreadStatusChanged?.Invoke(this, hasUnread));
        }

        public async Task LoadNotifications()
        {
            if (isLoading) return;

            isLoading = true;
            Notifications.Clear();

            try
            {
                var notifications = await ServiceManager.Get<MixItUpService>().GetNotifications();
                int lastReadId = ChannelSession.AppSettings.LastReadNotificationId;

                foreach (var notif in notifications)
                {
                    Color color = (Color)ColorConverter.ConvertFromString(notif.IconColor);
                    string notifTime = NotifDateTime(notif.Timestamp);

                    Notifications.Add(new NotificationItem
                    {
                        Id = notif.Id.ToString(),
                        Title = notif.Title,
                        Message = notif.Message,
                        Icon = notif.Icon,
                        IconColor = new SolidColorBrush(color),
                        TimeAgo = notifTime,
                        Url = notif.Url,
                        IsPinned = notif.IsPinned
                    });
                }

                ServiceManager.Get<MixItUpService>().MarkNotificationsAsRead();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                isLoading = false;
            }
        }

        private string NotifDateTime(DateTime timestamp)
        {
            DateTime utcTimestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            DateTime localTime = utcTimestamp.ToLocalTime();
            return localTime.ToString("g", CultureInfo.CurrentCulture);
        }

        private void NotificationItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128));
            }
        }

        private void NotificationItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = Brushes.Transparent;
            }
        }

        private void LinkText_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is StackPanel stackPanel && stackPanel.DataContext is NotificationItem item)
            {
                if (!string.IsNullOrEmpty(item.Url))
                {
                    ServiceManager.Get<IProcessService>().LaunchLink(item.Url);
                }
            }
        }
    }

    public class NotificationItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _url;

        public string Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string TimeAgo { get; set; }
        public string Icon { get; set; }
        public Brush IconColor { get; set; }
        public Brush Background { get; set; } = Brushes.Transparent;
        public bool IsPinned { get; set; }
        public string Url
        {
            get => _url;
            set
            {
                _url = value;
                OnPropertyChanged(nameof(Url));
                OnPropertyChanged(nameof(LinkVisibility));
            }
        }

        public Visibility LinkVisibility => !string.IsNullOrEmpty(Url) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PinVisibility => IsPinned ? Visibility.Visible : Visibility.Collapsed;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}