using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum VeadotubeActionTypeEnum
    {
        SetAvatarState,
        PushAvatarState,
        PopAvatarState,
        ToggleAvatarState,
        SetRandomAvatarState,
        SetPushToTalk,
    }

    public enum VeadotubePushToTalkTypeEnum
    {
        On,
        Off,
        Toggle,
    }

    [DataContract]
    public class VeadotubeActionModel : ActionModelBase
    {
        public static VeadotubeActionModel CreateForState(VeadotubeActionTypeEnum actionType, string stateID, string stateName)
        {
            return new VeadotubeActionModel(actionType) { StateID = stateID, StateName = stateName };
        }

        public static VeadotubeActionModel CreateForRandomState() { return new VeadotubeActionModel(VeadotubeActionTypeEnum.SetRandomAvatarState); }

        public static VeadotubeActionModel CreateForPushToTalk(VeadotubePushToTalkTypeEnum pushToTalkType)
        {
            return new VeadotubeActionModel(VeadotubeActionTypeEnum.SetPushToTalk) { PushToTalkType = pushToTalkType };
        }

        [DataMember]
        public VeadotubeActionTypeEnum ActionType { get; set; }

        /// <summary>
        /// The state is stored by id, which is what every operation sends over the wire. The name is
        /// carried alongside so the editor can still show something readable when veadotube is closed.
        /// </summary>
        [DataMember]
        public string StateID { get; set; }
        [DataMember]
        public string StateName { get; set; }

        [DataMember]
        public VeadotubePushToTalkTypeEnum PushToTalkType { get; set; }

        public VeadotubeActionModel(VeadotubeActionTypeEnum actionType)
            : base(ActionTypeEnum.Veadotube)
        {
            this.ActionType = actionType;
        }

        [Obsolete]
        public VeadotubeActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (ChannelSession.Settings.VeadotubeEnabled && !ServiceManager.Get<VeadotubeService>().IsConnected)
            {
                Result result = await ServiceManager.Get<VeadotubeService>().Connect();
                if (!result.Success)
                {
                    return;
                }
            }

            if (ServiceManager.Get<VeadotubeService>().IsConnected)
            {
                if (this.ActionType == VeadotubeActionTypeEnum.SetAvatarState)
                {
                    await ServiceManager.Get<VeadotubeService>().SetState(this.StateID);
                }
                else if (this.ActionType == VeadotubeActionTypeEnum.PushAvatarState)
                {
                    await ServiceManager.Get<VeadotubeService>().PushState(this.StateID);
                }
                else if (this.ActionType == VeadotubeActionTypeEnum.PopAvatarState)
                {
                    await ServiceManager.Get<VeadotubeService>().PopState(this.StateID);
                }
                else if (this.ActionType == VeadotubeActionTypeEnum.ToggleAvatarState)
                {
                    await ServiceManager.Get<VeadotubeService>().ToggleState(this.StateID);
                }
                else if (this.ActionType == VeadotubeActionTypeEnum.SetRandomAvatarState)
                {
                    await ServiceManager.Get<VeadotubeService>().SetRandomState();
                }
                else if (this.ActionType == VeadotubeActionTypeEnum.SetPushToTalk)
                {
                    bool? value = null;
                    if (this.PushToTalkType == VeadotubePushToTalkTypeEnum.On) { value = true; }
                    else if (this.PushToTalkType == VeadotubePushToTalkTypeEnum.Off) { value = false; }

                    await ServiceManager.Get<VeadotubeService>().SetPushToTalk(value);
                }
            }
        }
    }
}
