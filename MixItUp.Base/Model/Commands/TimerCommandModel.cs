using System;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class TimerCommandModel : CommandModelBase
    {
        [DataMember]
        public StreamingPlatformTypeEnum Platform { get; set; } = StreamingPlatformTypeEnum.All;

        public TimerCommandModel(string name) : base(name, CommandTypeEnum.Timer) { }

        [Obsolete]
        public TimerCommandModel() : base() { }
    }
}
