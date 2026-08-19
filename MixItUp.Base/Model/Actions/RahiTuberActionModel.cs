using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum RahiTuberActionTypeEnum
    {
        TriggerState,
        StopState,
    }

    [DataContract]
    public class RahiTuberActionModel : ActionModelBase
    {
        public static RahiTuberActionModel CreateForState(RahiTuberActionTypeEnum actionType, string state)
        {
            return new RahiTuberActionModel(actionType) { State = state };
        }

        [DataMember]
        public RahiTuberActionTypeEnum ActionType { get; set; }

        /// <summary>
        /// The state's name, or its zero-based index. RahiTuber has no way to list what it has, so
        /// this is whatever the user typed and is sent through as-is.
        /// </summary>
        [DataMember]
        public string State { get; set; }

        public RahiTuberActionModel(RahiTuberActionTypeEnum actionType)
            : base(ActionTypeEnum.RahiTuber)
        {
            this.ActionType = actionType;
        }

        [Obsolete]
        public RahiTuberActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (ChannelSession.Settings.RahiTuberEnabled && !ServiceManager.Get<RahiTuberService>().IsConnected)
            {
                Result connection = await ServiceManager.Get<RahiTuberService>().Connect();
                if (!connection.Success)
                {
                    return;
                }
            }

            if (!ServiceManager.Get<RahiTuberService>().IsConnected)
            {
                return;
            }

            string state = await ReplaceStringWithSpecialModifiers(this.State, parameters);

            Result result = await ServiceManager.Get<RahiTuberService>().SetState(state, this.ActionType == RahiTuberActionTypeEnum.TriggerState);
            if (!result.Success)
            {
                Logger.Log(LogLevel.Error, "RahiTuber Action - State " + state + " did not change: " + result.Message);
            }
        }
    }
}
