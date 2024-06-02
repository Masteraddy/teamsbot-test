using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace APITest.Controllers
{

    [Route("[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        // private readonly ILogger<HealthController> _logger;

        // public HealthController(ILogger<HealthController> logger)
        // {
        //     _logger = logger;
        // }

        /// <summary>
        /// Health check endpoint.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                // _logger.LogInformation("HEALTH CALL");
                return Ok();
            }
            catch (Exception e)
            {

                return Problem(detail: e.StackTrace, statusCode: (int)HttpStatusCode.InternalServerError, title: e.Message);
            }
        }
    }
}
