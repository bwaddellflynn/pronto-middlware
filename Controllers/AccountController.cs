using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        [HttpGet]
        [Route("/callback")]
        public async Task<IActionResult> Callback(string code, string state)
        {
            _logger.LogInformation("Callback endpoint called with code: {Code}", code);

            var expectedState = HttpContext.Session.GetString("oauth-state");

            if (state != expectedState)
            {
                _logger.LogWarning("Invalid OAuth state received: {State}", state);
                return BadRequest("Invalid OAuth state received.");
            }
            // Exchange the code for an access token
            var tokenResponse = await ExchangeAuthorizationCodeForToken(code);

            if (string.IsNullOrEmpty(tokenResponse))
            {
                _logger.LogError("Error exchanging token");
                return BadRequest("Error exchanging token");
            }

            var token = JObject.Parse(tokenResponse);
            var accessToken = token["access_token"]?.ToString();
            var refreshToken = token["refresh_token"]?.ToString();

            // Create the claims for the user based on the token received
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, accessToken),
        };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) // Set cookie expiration as needed
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            return Redirect("http://localhost:8080"); // Redirect to the frontend application
        }

        [HttpGet]
        [Route("login")]
        public IActionResult Login(string returnUrl = "https://localhost:7250/auth/callback")
        {
            // Challenge the OAuth middleware to start the auth flow
            var state = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("oauth-state", state);
            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl,
                Items = { { "state", state } }
            };

            return Challenge(properties, "AcceloOAuth");
        }

        [HttpGet]
        [Route("logout")]
        public IActionResult Logout()
        {
            // Sign out of the cookie authentication scheme.
            return SignOut(new AuthenticationProperties
            {
                // Redirect to the home page after signing out.
                RedirectUri = "/"
            }, CookieAuthenticationDefaults.AuthenticationScheme);
        }

        [HttpGet]
        [Route("status")]
        public IActionResult Status()
        {
            // Return the authentication status
            return Ok(new { IsAuthenticated = User.Identity.IsAuthenticated });
        }

        private async Task<string> ExchangeAuthorizationCodeForToken(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var tokenRequestBody = new Dictionary<string, string>
            {
                { "client_id", "eeb2543954@perbyte.accelo.com" },
                { "client_secret", ".zj7iFztugU4QjqRG58GSQM9zA4iw2ci" }, 
                { "grant_type", "authorization_code" },
                { "redirect_uri", "https://localhost:7250/auth/callback" } 
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://perbyte.api.accelo.com/oauth2/v0/token")
            {
                Content = new FormUrlEncodedContent(tokenRequestBody)
            };

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }
    }
}
