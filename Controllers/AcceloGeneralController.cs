using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AcceloGeneralController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AcceloGeneralController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public AcceloGeneralController(IHttpClientFactory httpClientFactory, ILogger<AcceloGeneralController> logger)
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

        [HttpGet("issues")]
        public async Task<IActionResult> GetIssues(int companyId, long startDate, long endDate)
        {
            if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return Unauthorized("Authorization header is missing.");
            }

            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
            {
                return Unauthorized("Invalid Authorization header.");
            }

            var accessToken = headerValue.Parameter;
            var issues = await GetIssuesFromAcceloAsync(accessToken, companyId, startDate, endDate);
            return Ok(issues);
        }
        private async Task<List<Issue>> GetIssuesFromAcceloAsync(string accessToken, int companyId, long startDate, long endDate)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}issues?_filters=against_id({companyId}),date_opened_greater_than_or_equal({startDate}),date_opened_less_than_or_equal({endDate})&_fields=against_id,resolution_detail,standing,date_opened,billable_seconds,class(title)");

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<Issue>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<IssueResponse>>(jsonResponse);

            var issues = acceloResponse.Response.Select(issueResp => new Issue
            {
                Id = int.Parse(issueResp.Id),
                Title = issueResp.Title,
                Against_Id = int.Parse(issueResp.Against_Id),
                Resolution_Detail = issueResp.Resolution_Detail,
                Standing = issueResp.Standing,
                Date_Opened = long.Parse(issueResp.Date_Opened),
                Billable_Seconds = int.Parse(issueResp.Billable_Seconds),
                Class = issueResp.Class.Title,
            }).ToList();

            return issues;
        }


        public class Company
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Company_Status { get; set; }
        }

        public class AcceloApiResponse<T>
        {
            public List<T> Response { get; set; }
        }

        public class CompanyResponse
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Company_Status { get; set; }
        }

        public class Issue
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public int Against_Id { get; set; }
            public string Resolution_Detail { get; set; }
            public string Standing { get; set; }
            public long Date_Opened { get; set; }
            public int Billable_Seconds { get; set; }
            public string Class { get; set; }
        }

        public class ClassResponse
        {
            public string Id { get; set; }
            public string Title { get; set; }
        }

        public class IssueResponse
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Against_Id { get; set; }
            public string Resolution_Detail { get; set; }
            public string Standing { get; set; }
            public string Date_Opened { get; set; }
            public string Billable_Seconds { get; set; }
            public ClassResponse Class { get; set; }
        }
    }
}
