
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;

        // Constructor that injects the logger
        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            var sessionId = HttpContext.Session.Id;
            HttpContext.Session.SetString("Initiated", "true"); // Ensure session starts

            _logger.LogInformation($"Login initiated. Session ID: {sessionId}");

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
            return RedirectToAction("Success");
        }

        [HttpGet("status")]
        public IActionResult GetTokenStatus()
        {
            var token = HttpContext.Session.GetString("AccessToken");
            var isAuthenticated = HttpContext.User.Identity.IsAuthenticated;  

            return Ok(new { Token = token, IsAuthenticated = isAuthenticated });
        }



        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            // Clear the access token from the session
            HttpContext.Session.Remove("AccessToken");

            // Sign out the user
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Optionally clear the entire session
            HttpContext.Session.Clear();  // Clearing the session explicitly

            // Set no-cache headers to prevent the browser from caching sensitive data
            Response.Headers.Add("Cache-Control", "no-store, max-age=0");

            return Redirect("/");
        }
    }
}
