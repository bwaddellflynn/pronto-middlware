using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Pronto.Middleware.Models.EmployeeInsights;
using Pronto.Middleware.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Pronto.Middleware.Controllers.EmployeeInsights
{
    [ApiController]
    [Route("employeeinsights/activities")]
    public class EIActivitiesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EIActivitiesController> _logger;
        private const string BaseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public EIActivitiesController(
            IHttpClientFactory httpClientFactory,
            ILogger<EIActivitiesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EIActivity>>> GetActivitiesAsync(
            [FromQuery] int limit = 100,
            [FromQuery(Name = "fields")] string? fields = "_ALL",
            [FromQuery] long? startDate = null,
            [FromQuery] long? endDate = null,
            [FromQuery] int? ownerId = null)
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                return Unauthorized("Missing Authorization header.");
            if (!AuthenticationHeaderValue.TryParse(authHeader, out var headerValue))
                return Unauthorized("Invalid Authorization header.");

            var token = headerValue.Parameter!;

            // build filter string as before
            var filterList = new List<string>();
            if (ownerId.HasValue)
                filterList.Add($"owner_id({ownerId.Value})");
            if (startDate.HasValue)
                filterList.Add($"date_logged_after({startDate.Value})");
            if (endDate.HasValue)
                filterList.Add($"date_logged_before({endDate.Value})");
            var filters = filterList.Any() ? string.Join(",", filterList) : null;

            var activities = await FetchActivitiesAsync(token, limit, fields, filters);
            return Ok(activities);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<EIActivity>> GetActivityByIdAsync(
            string id,
            [FromQuery(Name = "fields")] string? fields = "_ALL")
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                return Unauthorized("Missing Authorization header.");
            if (!AuthenticationHeaderValue.TryParse(authHeader, out var headerValue))
                return Unauthorized("Invalid Authorization header.");

            var token = headerValue.Parameter!;
            var activity = await FetchActivityByIdAsync(token, id, fields);
            if (activity == null)
                return NotFound();
            return Ok(activity);
        }

        private async Task<List<EIActivity>> FetchActivitiesAsync(
            string token,
            int limit,
            string? fields,
            string? filters)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var allResponses = new List<ActivityResponse>();
            int offset = 0;

            while (true)
            {
                // build URL with _limit and _offset
                var url = $"{BaseUrl}activities?_limit={limit}&_offset={offset}&_fields={fields}";
                if (!string.IsNullOrWhiteSpace(filters))
                    url += $"&_filters={filters}";

                _logger.LogInformation("Fetching Accelo Activities: {Url}", url);
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("Accelo Response [{Status}]: {Json}", response.StatusCode, json);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error fetching activities: {Status} {Json}",
                                     response.StatusCode, json);
                    break;
                }

                var parsed = JsonConvert.DeserializeObject<AcceloApiResponse<ActivityResponse>>(json);
                var batch = parsed?.Response ?? new List<ActivityResponse>();
                if (batch.Count == 0)
                    break;    // no more pages

                allResponses.AddRange(batch);
                offset += batch.Count;
            }

            // map to your EIActivity model
            return allResponses.Select(r => new EIActivity
            {
                Id = r.Id,
                Subject = r.Subject,
                Details = r.Details,
                DateLogged = r.DateLogged,
                Billable = r.Billable,
                NonBillable = r.NonBillable,
                OwnerId = r.OwnerId,
                TaskId = r.Task,
                AgainstType = r.AgainstType,
                AgainstId = r.AgainstId,
                Status = r.Standing
            }).ToList();
        }

        private async Task<EIActivity?> FetchActivityByIdAsync(
            string token,
            string id,
            string? fields)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var url = $"{BaseUrl}activities/{id}?_fields={fields}";
            _logger.LogInformation("Fetching Accelo Activity by ID: {Url}", url);

            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Accelo Activity Response [{Status}]: {Json}", response.StatusCode, json);
            if (!response.IsSuccessStatusCode)
                return null;

            var parsed = JsonConvert.DeserializeObject<AcceloApiResponse<ActivityResponse>>(json);
            var r = parsed?.Response.FirstOrDefault();
            if (r == null)
                return null;

            return new EIActivity
            {
                Id = r.Id,
                Subject = r.Subject,
                Details = r.Details,
                DateLogged = r.DateLogged,
                Billable = r.Billable,
                NonBillable = r.NonBillable,
                OwnerId = r.OwnerId,
                TaskId = r.Task,
                AgainstType = r.AgainstType,
                AgainstId = r.AgainstId,
                Status = r.Standing
            };
        }
    }
}
