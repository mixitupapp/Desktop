using MixItUp.Base.Model.Commands;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum InfiniteAlbumActionTypeEnum
    {
        Styles,
        Emotions,
        Instruments,
        SoundEffects,
    }

    [DataContract]
    [Obsolete]
    public class InfiniteAlbumActionModel : ActionModelBase
    {
        public static InfiniteAlbumActionModel Create(InfiniteAlbumActionTypeEnum actionType, object command = null)
        {
            return new InfiniteAlbumActionModel
            {
                ActionType = actionType,
                Command = command,
            };
        }

        [DataMember]
        public InfiniteAlbumActionTypeEnum ActionType { get; set; }

        [DataMember]
        public object Command { get; set; }

        public InfiniteAlbumActionModel()
            : base(ActionTypeEnum.InfiniteAlbum)
        {
        }

        protected override Task PerformInternal(CommandParametersModel parameters)
        {
            return Task.CompletedTask;
        }
    }
}
