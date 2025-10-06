using MixItUp.API.V1.Models;
using MixItUp.Base.Model;
using MixItUp.Base.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V1
{
    [Route("api/chat")]
    [ApiController]
    public class ChatV1Controller : ControllerBase
    {
        [Route("users")]
        [HttpGet]
        public Task<IEnumerable<User>> GetChatUsers()
        {
            List<User> users = new List<User>();

            var chatUsers = ServiceManager.Get<UserService>().GetActiveUsers();
            foreach (var chatUser in chatUsers)
            {
                users.Add(UserV1Controller.UserFromUserDataViewModel(chatUser));
            }

            return Task.FromResult<IEnumerable<User>>(users);
        }

        [Route("message")]
        [HttpDelete]
        public async Task<IActionResult> ClearChat()
        {
            await ServiceManager.Get<ChatService>().ClearMessages(StreamingPlatformTypeEnum.All);
            return Ok();
        }

        [Route("message")]
        [HttpPost]
        public async Task<IActionResult> SendChatMessage([FromBody] SendChatMessage chatMessage)
        {
            if (chatMessage == null)
            {
                return BadRequest(new Error { Message = "Unable to parse chat message from POST body." });
            }

            await ServiceManager.Get<ChatService>().SendMessage(chatMessage.Message, StreamingPlatformTypeEnum.All, chatMessage.SendAsStreamer);
            return Ok();
        }

        [Route("whisper")]
        [HttpPost]
        public async Task<IActionResult> SendWhisper([FromBody] SendChatWhisper chatWhisper)
        {
            if (chatWhisper == null)
            {
                return BadRequest(new Error { Message = "Unable to parse chat whisper from POST body." });
            }

            await ServiceManager.Get<ChatService>().Whisper(chatWhisper.UserName, StreamingPlatformTypeEnum.All, chatWhisper.Message, chatWhisper.SendAsStreamer);
            return Ok();
        }
    }
}
