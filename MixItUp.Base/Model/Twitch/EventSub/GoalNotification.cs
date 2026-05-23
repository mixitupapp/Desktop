namespace MixItUp.Base.Model.Twitch.EventSub
{
    public class GoalNotification
    {
        public string id { get; set; }
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string type { get; set; }
        public string description { get; set; }
        public bool is_achieved { get; set; }
        public int current_amount { get; set; }
        public int target_amount { get; set; }
        public string started_at { get; set; }
        public string ended_at { get; set; }
    }
}
