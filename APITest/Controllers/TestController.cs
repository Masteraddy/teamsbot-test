using Microsoft.AspNetCore.Mvc;

namespace APITest.Controllers
{
    [ApiController]
    [Route("[controller]")]

    public class TestController : ControllerBase
    {
        [HttpGet(Name = "GetTest")]
        public async Task<string> Get()
        {
            return "Hello World!";
        }

        [HttpPost(Name = "PostTest")]
        public async Task<string> Post()
        {
            return "Hello World!";
        }
    }

}