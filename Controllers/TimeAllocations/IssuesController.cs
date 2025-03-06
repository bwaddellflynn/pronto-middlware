using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class IssuesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IssuesController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public IssuesController(IHttpClientFactory httpClientFactory, ILogger<IssuesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet()]
        public async Task<IActionResult> GetIssues(string issueId, long startDate, long endDate)
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
            var issues = await GetIssuesFromAcceloAsync(accessToken, issueId, startDate, endDate);
            return Ok(issues);
        }
        private async Task<List<Issue>> GetIssuesFromAcceloAsync(string accessToken, string issueId, long startDate, long endDate)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);


            var response = await client.GetAsync($"{_baseUrl}issues?_limit=100&_filters=id({issueId})&_fields=against_id,description,date_modified,standing,date_opened,billable_seconds,class(title)");

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
                Description = issueResp.Description,
                Standing = issueResp.Standing,
                Date_Opened = long.Parse(issueResp.Date_Opened),
                Billable_Seconds = int.Parse(issueResp.Billable_Seconds),
                Class = issueResp.Class.Title,
            }).ToList();

            return issues;
        
        }
    }
}