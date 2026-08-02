using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    [DataContract]
    public class PlatformMessageActionModel : ActionModelBase
    {
        [DataMember]
        public string ChatText { get; set; }

        [DataMember]
        public bool SendAsStreamer { get; set; }

        [DataMember]
        public StreamingPlatformTypeEnum Platform { get; set; }

        public PlatformMessageActionModel(string chatText, StreamingPlatformTypeEnum platform, bool sendAsStreamer = false)
            : base(ActionTypeEnum.PlatformMessage)
        {
            this.ChatText = chatText;
            this.Platform = platform;
            this.SendAsStreamer = sendAsStreamer;
        }

        [Obsolete]
        public PlatformMessageActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            string message = await ReplaceStringWithSpecialModifiers(this.ChatText, parameters);
            await ServiceManager.Get<ChatService>().SendMessage(message, this.Platform, this.SendAsStreamer);
        }
    }
}
