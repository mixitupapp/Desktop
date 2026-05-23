namespace MixItUp.Base.Model.Twitch.EventSub
{
    public class ShieldModeNotification
    {
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string moderator_user_id { get; set; }
        public string moderator_user_login { get; set; }
        public string moderator_user_name { get; set; }
        public string started_at { get; set; }
        public string ended_at { get; set; }
    }
}
