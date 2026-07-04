namespace MixItUp.Base.Model.Twitch.EventSub
{
    public class UnbanRequestNotification
    {
        public string id { get; set; }
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string user_id { get; set; }
        public string user_login { get; set; }
        public string user_name { get; set; }
        public string moderator_id { get; set; }
        public string moderator_login { get; set; }
        public string moderator_name { get; set; }
        public string text { get; set; }
        public string status { get; set; }
        public string resolution_text { get; set; }
        public string created_at { get; set; }
    }
}
