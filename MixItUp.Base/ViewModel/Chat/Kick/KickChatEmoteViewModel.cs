namespace MixItUp.Base.ViewModel.Chat.Kick
{
    public class KickChatEmoteViewModel : ChatEmoteViewModelBase
    {
        public KickChatEmoteViewModel(string emoteID, string emoteName)
        {
            this.ID = emoteID;
            this.Name = emoteName;

            // Kick webhook chat payload includes emote id and code. This URL format is used by Kick CDN.
            this.ImageURL = $"https://files.kick.com/emotes/{this.ID}/fullsize";
        }
    }
}
