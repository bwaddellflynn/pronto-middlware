using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".pronto.Session";
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "AcceloOAuth";
})
.AddCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; 
    options.SlidingExpiration = true; 
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); 
})
.AddOAuth("AcceloOAuth", options =>
{
    options.ClientId = "eeb2543954@perbyte.accelo.com";
    options.ClientSecret = ".zj7iFztugU4QjqRG58GSQM9zA4iw2ci";
    options.CallbackPath = "/auth/callback";

    options.AuthorizationEndpoint = "https://perbyte.api.accelo.com/oauth2/v0/authorize";
    options.TokenEndpoint = "https://perbyte.api.accelo.com/oauth2/v0/token";
    options.SaveTokens = true;

    options.Scope.Add("read(all)");

    options.Events = new OAuthEvents
    {
        OnCreatingTicket = context =>
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, context.AccessToken),
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            context.Principal.AddIdentity(claimsIdentity);

            return Task.CompletedTask;
        }
    };
});

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyCorsPolicy", builder =>
    {
        builder.WithOrigins("https://red-forest-020d52110.4.azurestaticapps.net")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials(); // Use with caution, only if necessary
    });
});

builder.Services.AddRouting(options => options.LowercaseUrls = true);

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();
app.UseRouting();

app.UseCors("CorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
