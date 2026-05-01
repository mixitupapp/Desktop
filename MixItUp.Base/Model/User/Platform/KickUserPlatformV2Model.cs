using MixItUp.Base.Model.Kick.Users;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

// TODO: hook this up
namespace MixItUp.Base.Model.User.Platform
{
    [DataContract]
    public class KickUserPlatformV2Model : UserPlatformV2ModelBase
    {
        public KickUserPlatformV2Model(KickUserModel user)
        {
            this.Platform = StreamingPlatformTypeEnum.Kick;
            this.SetUserProperties(user);
        }

        public KickUserPlatformV2Model(string id, string username, string displayName)
        {
            this.Platform = StreamingPlatformTypeEnum.Kick;
            this.ID = id;
            this.Username = username;
            this.DisplayName = displayName;
        }

        [Obsolete]
        public KickUserPlatformV2Model() : base() { }

        public override async Task Refresh()
        {
            if (ServiceManager.Get<KickSession>().IsConnected)
            {
                KickUserModel user = await ServiceManager.Get<KickSession>().StreamerService.GetCurrentUser();
                if (user != null && string.Equals(this.ID, user.UserID.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    this.SetUserProperties(user);
                }
            }
        }

        public void SetUserProperties(KickUserModel user)
        {
            this.ID = user.UserID.ToString();
            this.Username = user.Name;
            this.DisplayName = user.Name;
            this.AvatarLink = user.ProfilePicture;

            if (string.Equals(this.ID, ServiceManager.Get<KickSession>().StreamerID, StringComparison.OrdinalIgnoreCase))
            {
                this.Roles.Add(UserRoleEnum.Streamer);
            }
            else
            {
                this.Roles.Remove(UserRoleEnum.Streamer);
            }
        }
    }
}

