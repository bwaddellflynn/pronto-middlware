using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestAuthController : ControllerBase
    {
        [HttpGet("login")]
        public IActionResult TestLogin()
        {
            var token = "z8Y0yy9s0w_Ylh967TpyLM0Zn.44sxi9p8~nQSs_U8ASLwUk7RURBuHo5ztkIKnL";
            return Ok(new { Token = token });
        }

        [HttpGet("logout")]
        public IActionResult TestLogout()
        {
            return Ok();
        }
    }
}