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
            var token = "njjT8CeWB2ARWV8slT1-~1O3JBWXpDHarm4D-BF.XR-vB2bSpb-I835Meg3zMRxV";
            return Ok(new { Token = token });
        }

        [HttpGet("logout")]
        public IActionResult TestLogout()
        {
            return Ok();
        }
    }
}