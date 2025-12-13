using System;

namespace MixItUp.Base.Model.API
{
    public class NotificationModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public string Icon { get; set; }
        public string IconColor { get; set; }
        public string Url { get; set; }
        public bool IsPinned { get; set; }
    }
}