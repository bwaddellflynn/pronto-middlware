using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure OAuth authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Accelo";
})
.AddCookie(options =>
{
    options.LogoutPath = "/logout";
})
.AddOAuth("Accelo", options =>
{
    options.ClientId = "eeb2543954@perbyte.accelo.com";
    options.ClientSecret = ".zj7iFztugU4QjqRG58GSQM9zA4iw2ci";
    options.CallbackPath = new PathString("/auth/callback");
    options.AuthorizationEndpoint = "https://perbyte.api.accelo.com/oauth2/v0/authorize";
    options.TokenEndpoint = "https://perbyte.api.accelo.com/oauth2/v0/token";
    options.SaveTokens = true;
    options.Scope.Add("read(all)");

    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            var accessToken = context.AccessToken;

            context.HttpContext.Session.SetString("AccessToken", accessToken);

            // Directly log the access token
            context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogInformation($"OAuth login successful. AccessToken: {accessToken}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, accessToken)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(claimsIdentity);
            context.Principal = principal;

            await context.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            await context.HttpContext.Session.CommitAsync();
        },
        OnTicketReceived = context =>
        {
            context.ReturnUri = "https://pronto.com:8080";
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            context.Response.Redirect("/error?message=" + context.Failure.Message);
            context.HandleResponse();
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder.WithOrigins("https://red-forest-020d52110.4.azurestaticapps.net",
                            "https://pronto.perbyte.me",
                            "http://localhost:8080",
                            "https://localhost:8080",
                            "https://pronto.com:8080")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("CorsPolicy");

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

app.Run(); 