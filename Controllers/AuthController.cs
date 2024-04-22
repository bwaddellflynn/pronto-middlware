
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

        [HttpGet("statustest")]
        public IActionResult GetAuthStatus()
        {
            _logger.LogInformation("Accessing GetAuthStatus endpoint");
            _logger.LogInformation($"Session Token: {HttpContext.Session.GetString("AccessToken")}");
            _logger.LogInformation($"User Identity Name: {User?.Identity?.Name}");
            _logger.LogInformation($"Is Authenticated: {User.Identity.IsAuthenticated}");

            if (User.Identity.IsAuthenticated)
            {
                return Ok(new { Authenticated = true, User = User.Identity.Name });
            }
            else
            {
                return Ok(new { Authenticated = false });
            }
        }


        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        [HttpGet("set")]
        public IActionResult SetSession()
        {
            // Set a test value in the session
            HttpContext.Session.SetString("TestSessionKey", "TestSessionValue");
            return Ok("Session value set.");
        }

        [HttpGet("get")]
        public IActionResult GetSession()
        {
            // Retrieve the test value from the session
            var sessionValue = HttpContext.Session.GetString("TestSessionKey");
            var token = HttpContext.Session.GetString("AccessToken");
            return Ok($"Session value: {sessionValue ?? "Not set"}{token ?? "No token"}");
        }
    }
}
