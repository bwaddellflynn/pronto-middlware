using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Pronto.Middleware.Models;
using Pronto.Middleware.Models.ProjectManagementModels;
using System.Net.Http.Headers;

namespace Pronto.Middleware.Controllers.ProjectManagement
{
    [ApiController]
    [Route("projectmanagement/milestones")]
    public class PMMilestonesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PMMilestonesController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public PMMilestonesController(IHttpClientFactory httpClientFactory, ILogger<PMMilestonesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetMilestones(
            [FromQuery] int limit = 100,
            [FromQuery(Name = "fields")] string? fields = "_ALL",
            [FromQuery(Name = "filters")] string? filters = null)
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

            var milestones = await FetchMilestonesAsync(accessToken, limit, fields, filters);

            return Ok(milestones);
        }

        private async Task<List<PMMilestone>> FetchMilestonesAsync(string accessToken, int limit, string? fields, string? filters)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var query = $"{_baseUrl}milestones?_limit={limit}&_fields={fields}";

            if (!string.IsNullOrWhiteSpace(filters))
            {
                query += $"&_filters={filters}";
            }

            _logger.LogInformation("Requesting Accelo Milestones: {Query}", query);

            var response = await client.GetAsync(query);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Accelo API Response Status: {StatusCode}", response.StatusCode);
            _logger.LogDebug("Raw JSON: {Json}", json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch milestones from Accelo: {StatusCode} - {Json}", response.StatusCode, json);
                return new List<PMMilestone>();
            }

            var parsed = JsonConvert.DeserializeObject<AcceloApiResponse<PMMilestoneResponse>>(json);

            var results = parsed?.Response.Select(m => new PMMilestone
            {
                Id = int.Parse(m.Id),
                Title = m.Title,
                Job = int.Parse(m.Job),
                Description = m.Description,
                Standing = m.Standing,
                DateCommenced = TryParseLong(m.DateCommenced),
                DateStarted = TryParseLong(m.DateStarted),
                DateDue = TryParseLong(m.DateDue),
                DateCreated = TryParseLong(m.DateCreated),
                DateModified = TryParseLong(m.DateModified),
                DateCompleted = TryParseLong(m.DateCompleted),
            }).ToList() ?? new List<PMMilestone>();

            return results;
        }

        private static long? TryParseLong(string? value)
        {
            return long.TryParse(value, out var result) ? result : null;
        }
    }
}
