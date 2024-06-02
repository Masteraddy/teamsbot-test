using System.Net;
using APITest.Bot;
using APITest.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Skype.Bots.Media;

namespace APITest.Controllers
{

    [Route("[controller]")]
    [ApiController]
    public class CallsController : ControllerBase
    {
        private readonly ILogger<CallsController> _logger;
        private readonly IBotService _botService;
        private readonly AppSettings _settings;

        public CallsController(ILogger<CallsController> logger,
            IOptions<AppSettings> settings,
            IBotService botService)
        {
            _logger = logger;
            _settings = settings.Value;
            _botService = botService;
        }

        [HttpPost]
        public async Task<IActionResult> JoinCallAsync([FromBody] JoinCallBody joinCallBody)
        {
            try
            {
                var call = await _botService.JoinCallAsync(joinCallBody).ConfigureAwait(false);
                var values = new
                {
                    CallId = call.Id,
                    ScenarioId = call.ScenarioId,
                    ThreadId = call.Resource.ChatInfo.ThreadId,
                    Port = _settings.BotInstanceExternalPort.ToString()
                };

                return Ok(values);
            }
            catch (Exception e)
            {
                return Problem(detail: e.StackTrace, statusCode: (int)HttpStatusCode.InternalServerError, title: e.Message);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> OnEndCallAsync(string threadId)
        {
            try
            {
                await _botService.EndCallByThreadIdAsync(threadId).ConfigureAwait(false);
                return NoContent();
            }
            catch (Exception e)
            {
                return Problem(detail: e.StackTrace, statusCode: (int)HttpStatusCode.InternalServerError, title: e.Message);
            }
        }
    }

}