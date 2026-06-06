namespace MixItUp.Base.Model.Twitch.EventSub
{
    public class ShoutoutReceiveNotification
    {
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string from_broadcaster_user_id { get; set; }
        public string from_broadcaster_user_login { get; set; }
        public string from_broadcaster_user_name { get; set; }
        public int viewer_count { get; set; }
        public string started_at { get; set; }
    }
}
