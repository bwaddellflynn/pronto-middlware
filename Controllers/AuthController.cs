
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        [HttpGet("login")]
        public IActionResult Login()
        {
            var authenticationProperties = new AuthenticationProperties
            {
                Items =
                {
                    { "LoginProvider", "Accelo" }
                }
            };
            return Challenge(authenticationProperties, "Accelo");
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string code, string state, string error, string error_description)
        {
            if (!string.IsNullOrEmpty(error))
            {
                return RedirectToAction("Error", new { message = error_description });
            }

            return RedirectToAction("Error", new { message = Uri.EscapeDataString(error_description) });
        }

        [HttpGet("status")]
        public IActionResult GetAuthStatus()
        {

            var token = HttpContext.Session.GetString("AccessToken");

            var response = new
            {
                Token = token ?? string.Empty
            };

            return Ok(response);
        }




        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }
    }
}
