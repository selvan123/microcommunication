using System;
using Microsoft.AspNetCore.Mvc;

namespace MicroCommunication.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ErrorController : ControllerBase
    {
        // GET api/values
        [HttpGet]
        [Route("generate")]
        public ActionResult<int> Generate()
        {
            // Throw an exception
            throw new Exception("This demo error was intended.");
        }
    }
}
