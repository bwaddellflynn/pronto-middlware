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
    public class JobsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<JobsController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public JobsController(IHttpClientFactory httpClientFactory, ILogger<JobsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet()]
        public async Task<IActionResult> GetJobs(string jobId, long startDate, long endDate)
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
            var jobs = await GetJobsFromAcceloAsync(accessToken, jobId, startDate, endDate);
            return Ok(jobs);
        }
        private async Task<List<Job>> GetJobsFromAcceloAsync(string accessToken, string jobId, long startDate, long endDate)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);


            var response = await client.GetAsync($"{_baseUrl}jobs?_limit=100&_filters=id({jobId})&_fields=against_id,date_modified,standing,date_commenced,billable_seconds");

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<Job>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<JobResponse>>(jsonResponse);

            var jobs = acceloResponse.Response.Select(jobResp => new Job
            {
                Id = int.Parse(jobResp.Id),
                Title = jobResp.Title,
                Against_Id = int.Parse(jobResp.Against_Id),
                Standing = jobResp.Standing,
                Date_Commenced = long.Parse(jobResp.Date_Commenced),
                Date_Modified = long.Parse(jobResp.Date_Modified),
            }).ToList();

            return jobs;

        }
    }
}