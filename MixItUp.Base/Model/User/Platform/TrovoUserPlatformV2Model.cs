using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.User.Platform
{
    [DataContract]
    public class TrovoUserPlatformV2Model : UserPlatformV2ModelBase
    {
        [Obsolete]
        public TrovoUserPlatformV2Model() : base() { }

        public override Task Refresh()
        {
            return Task.CompletedTask;
        }
    }
}
