using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Util;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum VeloraActionType
    {
        SetTitle,
        SetGame,
    }

    [DataContract]
    public class VeloraActionModel : ActionModelBase
    {
        public static VeloraActionModel CreateTextAction(VeloraActionType type, string text)
        {
            VeloraActionModel action = new VeloraActionModel(type);
            action.Text = text;
            return action;
        }

        public static VeloraActionModel CreateAction(VeloraActionType type)
        {
            return new VeloraActionModel(type);
        }

        [DataMember]
        public VeloraActionType ActionType { get; set; }

        [DataMember]
        public string Text { get; set; }

        private VeloraActionModel(VeloraActionType type)
            : base(ActionTypeEnum.Velora)
        {
            this.ActionType = type;
        }

        [Obsolete]
        public VeloraActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (ServiceManager.Get<VeloraSession>().IsConnected)
            {
                if (this.ActionType == VeloraActionType.SetTitle)
                {
                    string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                    Result result = await ServiceManager.Get<VeloraSession>().SetStreamTitle(text);
                    if (!result.Success)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.FailedToUpdateChannelInformation, parameters);
                    }
                }
                else if (this.ActionType == VeloraActionType.SetGame)
                {
                    string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                    Result result = await ServiceManager.Get<VeloraSession>().SetStreamCategory(text);
                    if (!result.Success)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.ErrorFailedToUpdateCategory, parameters);
                    }
                }
            }
        }
    }
}
