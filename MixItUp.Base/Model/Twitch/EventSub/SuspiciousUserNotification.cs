using System.Collections.Generic;

namespace MixItUp.Base.Model.Twitch.EventSub
{
    public class SuspiciousUserUpdateNotification
    {
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string moderator_user_id { get; set; }
        public string moderator_user_login { get; set; }
        public string moderator_user_name { get; set; }
        public string user_id { get; set; }
        public string user_login { get; set; }
        public string user_name { get; set; }
        public string low_trust_status { get; set; }
    }

    public class SuspiciousUserMessageNotification
    {
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string user_id { get; set; }
        public string user_login { get; set; }
        public string user_name { get; set; }
        public string low_trust_status { get; set; }
        public List<string> shared_ban_channel_ids { get; set; } = new List<string>();
        public List<string> types { get; set; } = new List<string>();
        public string ban_evasion_evaluation { get; set; }
        public SuspiciousUserMessageContent message { get; set; }
    }

    public class SuspiciousUserMessageContent
    {
        public string message_id { get; set; }
        public string text { get; set; }
    }
}
