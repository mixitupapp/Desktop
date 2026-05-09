using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Kick.ChannelRewards;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum KickActionType
    {
        SetTitle,
        SetGame,
        SetCustomTags,
        UpdateChannelPointReward,
    }

    [DataContract]
    public class KickActionModel : ActionModelBase
    {
        public static KickActionModel CreateTextAction(KickActionType type, string text)
        {
            KickActionModel action = new KickActionModel(type);
            action.Text = text;
            return action;
        }

        public static KickActionModel CreateSetCustomTagsAction(IEnumerable<string> customTags)
        {
            KickActionModel action = new KickActionModel(KickActionType.SetCustomTags);
            action.CustomTags.AddRange(customTags);
            return action;
        }

        public static KickActionModel CreateUpdateChannelPointReward(string id, string name, string description, bool state, bool paused, string backgroundColor, string cost)
        {
            KickActionModel action = new KickActionModel(KickActionType.UpdateChannelPointReward);
            action.ChannelPointRewardID = id;
            action.ChannelPointRewardName = name;
            action.ChannelPointRewardDescription = description;
            action.ChannelPointRewardState = state;
            action.ChannelPointRewardPaused = paused;
            action.ChannelPointRewardBackgroundColor = backgroundColor;
            action.ChannelPointRewardCostString = cost;
            return action;
        }

        public static KickActionModel CreateAction(KickActionType type)
        {
            return new KickActionModel(type);
        }

        [DataMember]
        public KickActionType ActionType { get; set; }

        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public List<string> CustomTags { get; set; } = new List<string>();

        [DataMember]
        public string ChannelPointRewardID { get; set; }
        [DataMember]
        public string ChannelPointRewardName { get; set; }
        [DataMember]
        public string ChannelPointRewardDescription { get; set; }
        [DataMember]
        public bool ChannelPointRewardState { get; set; }
        [DataMember]
        public bool ChannelPointRewardPaused { get; set; }
        [DataMember]
        public string ChannelPointRewardBackgroundColor { get; set; }
        [DataMember]
        public string ChannelPointRewardCostString { get; set; }

        private KickActionModel(KickActionType type)
            : base(ActionTypeEnum.Kick)
        {
            this.ActionType = type;
        }

        [Obsolete]
        public KickActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (ServiceManager.Get<KickSession>().IsConnected)
            {
                if (this.ActionType == KickActionType.SetTitle)
                {
                    string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                    Result result = await ServiceManager.Get<KickSession>().StreamerService.UpdateChannel(title: text);
                    if (!result.Success)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.FailedToUpdateChannelInformation, parameters);
                    }
                }
                else if (this.ActionType == KickActionType.SetGame)
                {
                    string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                    Result result = await ServiceManager.Get<KickSession>().SetStreamCategory(text);
                    if (!result.Success)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.ErrorFailedToUpdateCategory, parameters);
                    }
                }
                else if (this.ActionType == KickActionType.SetCustomTags)
                {
                    Result result = await ServiceManager.Get<KickSession>().StreamerService.UpdateChannel(customTags: this.CustomTags);
                    if (!result.Success)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.FailedToUpdateChannelInformation, parameters);
                    }
                }
                else if (this.ActionType == KickActionType.UpdateChannelPointReward)
                {
                    string name = !string.IsNullOrEmpty(this.ChannelPointRewardName) ? await ReplaceStringWithSpecialModifiers(this.ChannelPointRewardName, parameters) : null;
                    string description = !string.IsNullOrEmpty(this.ChannelPointRewardDescription) ? await ReplaceStringWithSpecialModifiers(this.ChannelPointRewardDescription, parameters) : null;
                    string backgroundColor = !string.IsNullOrEmpty(this.ChannelPointRewardBackgroundColor) ? await ReplaceStringWithSpecialModifiers(this.ChannelPointRewardBackgroundColor, parameters) : null;

                    int? cost = null;
                    if (!string.IsNullOrEmpty(this.ChannelPointRewardCostString))
                    {
                        string costStr = await ReplaceStringWithSpecialModifiers(this.ChannelPointRewardCostString, parameters);
                        if (int.TryParse(costStr, out int parsedCost) && parsedCost > 0)
                        {
                            cost = parsedCost;
                        }
                    }

                    ChannelRewardModel reward = await ServiceManager.Get<KickSession>().StreamerService.UpdateChannelReward(
                        this.ChannelPointRewardID,
                        title: name,
                        cost: cost,
                        description: description,
                        backgroundColor: backgroundColor,
                        isEnabled: this.ChannelPointRewardState,
                        isPaused: this.ChannelPointRewardPaused);

                    if (reward == null)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.KickActionChannelPointRewardCouldNotBeUpdated, parameters);
                    }
                }
            }
        }
    }
}
