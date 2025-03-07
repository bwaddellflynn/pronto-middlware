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
    public class MilestonesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MilestonesController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public MilestonesController(IHttpClientFactory httpClientFactory, ILogger<MilestonesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet()]
        public async Task<IActionResult> GetMilestones(string milestoneId, long startDate, long endDate)
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
            var issues = await GetMilestonesFromAcceloAsync(accessToken, milestoneId, startDate, endDate);
            return Ok(issues);
        }
        private async Task<List<Milestone>> GetMilestonesFromAcceloAsync(string accessToken, string milestoneId, long startDate, long endDate)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);


            var response = await client.GetAsync($"{_baseUrl}milestones?_limit=100&_filters=id({milestoneId})&_fields=against_id,job, date_commenced,description,date_modified,standing,date_opened,billable_seconds,title");

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<Milestone>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<MilestoneResponse>>(jsonResponse);

            var milestones = acceloResponse.Response.Select(milestoneResp => new Milestone
            {
                Id = int.Parse(milestoneResp.Id),
                Title = milestoneResp.Title,
                Job = int.Parse(milestoneResp.Job),
                Description = milestoneResp.Description,
                Standing = milestoneResp.Standing,
                Date_Commenced = long.Parse(milestoneResp.Date_Commenced),
            }).ToList();

            return milestones;

        }
    }
}