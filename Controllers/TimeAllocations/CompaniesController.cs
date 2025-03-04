using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CompaniesController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CompaniesController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public CompaniesController(IHttpClientFactory httpClientFactory, ILogger<CompaniesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies()
        {
            // Extract the token from the Authorization header
            if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return Unauthorized("Authorization header is missing.");
            }

            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
            {
                return Unauthorized("Invalid Authorization header.");
            }

            var accessToken = headerValue.Parameter;
            var contracts = await GetCompaniesFromAcceloAsync(accessToken);
            return Ok(contracts);
        }

        private async Task<List<Company>> GetCompaniesFromAcceloAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}companies?_limit=100&_filters=status(3)&_fields=company_status");

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<Company>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<CompanyResponse>>(jsonResponse);

            var companies = acceloResponse.Response
                .Select(companyResp => new Company
                {
                    Id = int.Parse(companyResp.Id),
                    Name = companyResp.Name,
                    Company_Status = int.Parse(companyResp.Company_Status),
                })
                .ToList();

            return companies;
        }
    }
}
