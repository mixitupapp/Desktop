using Google.Apis.YouTube.v3.Data;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.ViewModel.User;
using System;

namespace MixItUp.Base.ViewModel.Chat.YouTube
{
    public class YouTubeJewelsGiftViewModel
    {
        public UserV2ViewModel User { get; set; }

        public int JewelsAmount { get; set; }
        public string GiftName { get; set; }
        public string GiftImageUrl { get; set; }
        public string GiftDurationSeconds { get; set; }
        public bool HasVisualEffect { get; set; }
        public int ComboCount { get; set; }

        public string Message { get; set; }

        public YouTubeJewelsGiftViewModel(LiveChatGiftDetails giftDetails, UserV2ViewModel user, string message)
        {
            this.User = user;

            this.JewelsAmount = giftDetails?.JewelsAmount.GetValueOrDefault() ?? 0;
            this.GiftName = giftDetails?.GiftName;
            this.GiftImageUrl = giftDetails?.GiftUrl;
            if (!string.IsNullOrEmpty(this.GiftImageUrl) && this.GiftImageUrl.StartsWith("//"))
            {
                this.GiftImageUrl = $"https:{this.GiftImageUrl}";
            }

            string giftDurationSeconds = null;
            if (giftDetails?.GiftDuration is string duration)
            {
                giftDurationSeconds = duration.Trim();
                if (giftDurationSeconds.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                {
                    giftDurationSeconds = giftDurationSeconds.Substring(0, giftDurationSeconds.Length - 1);
                }
            }
            this.GiftDurationSeconds = giftDurationSeconds;

            this.HasVisualEffect = giftDetails?.HasVisualEffect.GetValueOrDefault() ?? false;
            this.ComboCount = giftDetails?.ComboCount.GetValueOrDefault() ?? 0;

            this.Message = message;
        }

        public void SetCommandParameterData(CommandParametersModel parameters)
        {
            parameters.SetArguments(this.Message);

            parameters.SpecialIdentifiers["message"] = this.Message;
            parameters.SpecialIdentifiers["jewelsamount"] = this.JewelsAmount.ToString();
            parameters.SpecialIdentifiers["giftname"] = this.GiftName;
            parameters.SpecialIdentifiers["giftimageurl"] = this.GiftImageUrl;
            parameters.SpecialIdentifiers["giftdurationseconds"] = this.GiftDurationSeconds;
            parameters.SpecialIdentifiers["gifthasvisualeffect"] = this.HasVisualEffect.ToString();
            parameters.SpecialIdentifiers["giftcombocount"] = this.ComboCount.ToString();
        }
    }
}
