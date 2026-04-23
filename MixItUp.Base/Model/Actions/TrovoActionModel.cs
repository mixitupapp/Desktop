using MixItUp.Base.Model.Commands;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum TrovoActionType
    {
        Host,
        EnableSlowMode,
        DisableSlowMode,
        EnableFollowerMode,
        DisableFollowerMode,
        AddUserRole,
        RemoveUserRole,
        FastClip90Seconds,
        SetTitle,
        SetGame,
        EnableSubscriberMode,
        DisableSubscriberMode,
    }

    [Obsolete]
    [DataContract]
    public class TrovoActionModel : ActionModelBase
    {
        public static TrovoActionModel CreateHostAction(string username)
        {
            TrovoActionModel action = new TrovoActionModel(TrovoActionType.Host);
            action.Username = username;
            return action;
        }

        public static TrovoActionModel CreateTextAction(TrovoActionType actionType, string text)
        {
            TrovoActionModel action = new TrovoActionModel(actionType);
            action.Text = text;
            return action;
        }

        public static TrovoActionModel CreateEnableSlowModeAction(int amount)
        {
            TrovoActionModel action = new TrovoActionModel(TrovoActionType.EnableSlowMode);
            action.Amount = amount;
            return action;
        }

        public static TrovoActionModel CreateUserRoleAction(TrovoActionType actionType, string username, string roleName)
        {
            TrovoActionModel action = new TrovoActionModel(actionType);
            action.Username = username;
            action.RoleName = roleName;
            return action;
        }

        public static TrovoActionModel CreateBasicAction(TrovoActionType actionType)
        {
            return new TrovoActionModel(actionType);
        }

        [DataMember]
        public TrovoActionType ActionType { get; set; }

        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public string RoleName { get; set; }

        [DataMember]
        public int Amount { get; set; }

        private TrovoActionModel(TrovoActionType type)
            : base(ActionTypeEnum.Trovo)
        {
            this.ActionType = type;
        }

        [Obsolete]
        public TrovoActionModel() { }

        protected override Task PerformInternal(CommandParametersModel parameters) { return Task.CompletedTask; }
    }
}
