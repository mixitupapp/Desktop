using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.VPZone.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    /// <summary>
    /// The VPZone actions. Moderator management is absent because VPZone exposes no API for it, and
    /// there is no VIP role on the platform at all.
    /// </summary>
    public enum VPZoneActionType
    {
        SetTitle,
        SetGame,
        SetTags,
        Announce,
        ClearChat,
        BanUser,
        UnbanUser,
        TimeoutUser,
        UntimeoutUser,

        // Held back from the editor's action list until a channel-bound grant key has somewhere to
        // live. See VPZoneActionEditorControlViewModel.ActionTypes. The execution path below is
        // complete and works the moment the filter comes off.
        GrantChannelPoints,

        PinMessage,
        UnpinMessage,
    }

    [DataContract]
    public class VPZoneActionModel : ActionModelBase
    {
        public static VPZoneActionModel CreateTextAction(VPZoneActionType type, string text)
        {
            return new VPZoneActionModel(type) { Text = text };
        }

        public static VPZoneActionModel CreateAction(VPZoneActionType type)
        {
            return new VPZoneActionModel(type);
        }

        [DataMember]
        public VPZoneActionType ActionType { get; set; }

        // Multi-purpose text field: stream title, category, tag list, or announcement message.
        [DataMember]
        public string Text { get; set; }

        // Target user for a ban, timeout, unban or channel-points grant.
        [DataMember]
        public string TargetUsername { get; set; }

        // Timeout duration in seconds, or the channel-points amount.
        [DataMember]
        public string Amount { get; set; }

        // Optional reason for a ban or timeout.
        [DataMember]
        public string Reason { get; set; }

        private VPZoneActionModel(VPZoneActionType type)
            : base(ActionTypeEnum.VPZone)
        {
            this.ActionType = type;
        }

        [Obsolete]
        public VPZoneActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            VPZoneSession session = ServiceManager.Get<VPZoneSession>();
            if (!session.IsConnected)
            {
                return;
            }

            switch (this.ActionType)
            {
                case VPZoneActionType.SetTitle:
                    {
                        string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        Result result = await session.SetStreamTitle(text);
                        if (!result.Success)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.FailedToUpdateChannelInformation, parameters);
                        }
                    }
                    break;
                case VPZoneActionType.SetGame:
                    {
                        string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        Result result = await session.SetStreamCategory(text);
                        if (!result.Success)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.ErrorFailedToUpdateCategory, parameters);
                        }
                    }
                    break;
                case VPZoneActionType.SetTags:
                    {
                        string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        string[] tags = (text ?? string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < tags.Length; i++)
                        {
                            tags[i] = tags[i].Trim();
                        }

                        Result result = await session.SetStreamTags(tags);
                        if (!result.Success)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.FailedToUpdateChannelInformation, parameters);
                        }
                    }
                    break;
                case VPZoneActionType.Announce:
                    {
                        string message = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            await session.SendAnnouncement(message);
                        }
                    }
                    break;
                case VPZoneActionType.ClearChat:
                    // Routed through ChatService rather than straight at the session so the local chat
                    // list is emptied too, the same as the Clear Chat button and the Moderation action.
                    // The clear_chat frame that comes back only raises an alert, matching how Twitch
                    // handles its own echo, so nothing else would empty the window.
                    await ServiceManager.Get<ChatService>().ClearMessages(StreamingPlatformTypeEnum.VPZone);
                    break;
                case VPZoneActionType.PinMessage:
                    {
                        string message = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            await session.PinMessage(message);
                        }
                    }
                    break;
                case VPZoneActionType.UnpinMessage:
                    await session.UnpinMessage();
                    break;
                default:
                    await this.PerformUserAction(session, parameters);
                    break;
            }
        }

        private async Task PerformUserAction(VPZoneSession session, CommandParametersModel parameters)
        {
            // Resolve the target on VPZone: an explicit username (special identifiers allowed), else the
            // command's own user. A target that cannot be resolved is a no-op, matching the Moderation action.
            UserV2ViewModel targetUser;
            if (!string.IsNullOrEmpty(this.TargetUsername))
            {
                string username = await ReplaceStringWithSpecialModifiers(this.TargetUsername, parameters);
                targetUser = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.VPZone, platformUsername: username, performPlatformSearch: true);
            }
            else
            {
                targetUser = parameters.User;
            }

            if (targetUser == null)
            {
                return;
            }

            string reason = string.IsNullOrEmpty(this.Reason) ? null : await ReplaceStringWithSpecialModifiers(this.Reason, parameters);

            switch (this.ActionType)
            {
                // A timeout and a ban share one endpoint on VPZone, so lifting either is the same call.
                case VPZoneActionType.UnbanUser:
                case VPZoneActionType.UntimeoutUser:
                    await session.UnbanUser(targetUser);
                    break;
                case VPZoneActionType.BanUser:
                    await session.BanUser(targetUser, reason);
                    break;
                case VPZoneActionType.TimeoutUser:
                    {
                        int duration = 300;
                        string amountString = await ReplaceStringWithSpecialModifiers(this.Amount, parameters);
                        if (!string.IsNullOrWhiteSpace(amountString) && int.TryParse(amountString, out int parsed) && parsed > 0)
                        {
                            duration = parsed;
                        }
                        await session.TimeoutUser(targetUser, duration, reason);
                    }
                    break;
                case VPZoneActionType.GrantChannelPoints:
                    {
                        string amountString = await ReplaceStringWithSpecialModifiers(this.Amount, parameters);
                        if (!string.IsNullOrWhiteSpace(amountString) && int.TryParse(amountString, out int amount) && amount > 0)
                        {
                            Result result = await session.GrantChannelPoints(targetUser, amount, reason);
                            if (!result.Success)
                            {
                                await ServiceManager.Get<ChatService>().SendMessage(result.Message, parameters);
                            }
                        }
                    }
                    break;
            }
        }
    }
}
