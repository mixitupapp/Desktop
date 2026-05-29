using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Twitch.Chat
{
    [DataContract]
    public class PinnedChatMessageModel
    {
        [DataMember]
        public string message_id { get; set; }
        [DataMember]
        public string broadcaster_id { get; set; }
        [DataMember]
        public string sender_user_id { get; set; }
        [DataMember]
        public string sender_user_login { get; set; }
        [DataMember]
        public string sender_user_name { get; set; }
        [DataMember]
        public string pinned_by_user_id { get; set; }
        [DataMember]
        public string pinned_by_user_login { get; set; }
        [DataMember]
        public string pinned_by_user_name { get; set; }
        [DataMember]
        public string starts_at { get; set; }
        [DataMember]
        public string ends_at { get; set; }
        [DataMember]
        public string updated_at { get; set; }
    }
}
