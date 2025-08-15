using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Pronto.Middleware.Models;
using Pronto.Middleware.Models.ProjectManagementModels;
using System.Globalization;
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
        [ProducesResponseType(typeof(List<PMMilestone>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMilestones(
            [FromQuery] int limit = 100,
            [FromQuery] int? offset = null,
            [FromQuery(Name = "fields")] string? fields = "_ALL",
            [FromQuery(Name = "filters")] string? filters = null)
        {
            if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
                return Unauthorized("Authorization header is missing.");

            if (!AuthenticationHeaderValue.TryParse(authorizationHeader!, out var headerValue))
                return Unauthorized("Invalid Authorization header.");

            var accessToken = headerValue.Parameter!;
            var milestones = await FetchMilestonesAsync(accessToken, limit, offset, fields, filters);
            return Ok(milestones);
        }

        private async Task<List<PMMilestone>> FetchMilestonesAsync(string accessToken, int limit, int? offset, string? fields, string? filters)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var queryParams = new Dictionary<string, string?>
            {
                ["_limit"] = limit.ToString(CultureInfo.InvariantCulture),
                ["_fields"] = fields ?? "_ALL"
            };
            if (!string.IsNullOrWhiteSpace(filters)) queryParams["_filters"] = filters;
            if (offset.HasValue && offset.Value > 0) queryParams["_offset"] = offset.Value.ToString(CultureInfo.InvariantCulture);

            var endpoint = $"{_baseUrl}milestones";
            var query = QueryHelpers.AddQueryString(endpoint, queryParams!);

            _logger.LogInformation("Requesting Accelo Milestones: {Query}", query);

            using var response = await client.GetAsync(query);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Accelo API Response Status: {StatusCode}", response.StatusCode);
            _logger.LogDebug("Raw JSON: {Json}", json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch milestones from Accelo: {StatusCode} - {Json}", response.StatusCode, json);
                return new List<PMMilestone>();
            }

            var parsed = JsonConvert.DeserializeObject<AcceloApiResponse<PMMilestoneResponse>>(json);

            var results = parsed?.Response?.Select(m => new PMMilestone
            {
                Id = SafeInt(m.Id),
                Title = m.Title ?? string.Empty,
                Job = SafeInt(m.Job),

                Description = m.Description,
                Standing = m.Standing,

                // Prefer the new field
                MilestoneStatus = m.MilestoneStatus,
                Status = m.Status, // keep for now

                Rate = TryParseDecimal(m.Rate),
                RateCharged = TryParseDecimal(m.RateCharged),

                MilestoneObjectBudget = m.MilestoneObjectBudget,
                Ordering = TryParseIntNullable(m.Ordering),
                Manager = TryParseIntNullable(m.Manager),
                Parent = TryParseIntNullable(m.Parent),

                DateCommenced = TryParseLong(m.DateCommenced),
                DateStarted = TryParseLong(m.DateStarted),
                DateDue = TryParseLong(m.DateDue),
                DateCreated = TryParseLong(m.DateCreated),
                DateModified = TryParseLong(m.DateModified),
                DateCompleted = TryParseLong(m.DateCompleted),

                // NEW: map the nested schedule object safely
                MilestoneObjectSchedule = m.MilestoneObjectSchedule == null ? null : new PMMilestoneSchedule
                {
                    Id = SafeInt(m.MilestoneObjectSchedule.Id),
                    AgainstType = m.MilestoneObjectSchedule.AgainstType,
                    AgainstId = TryParseIntNullable(m.MilestoneObjectSchedule.AgainstId),

                    DateCommenced = TryParseLong(m.MilestoneObjectSchedule.DateCommenced),

                    DatePlannedStart = TryParseLong(m.MilestoneObjectSchedule.DatePlannedStart),
                    DateFixedStart = TryParseLong(m.MilestoneObjectSchedule.DateFixedStart),
                    DatePredictedStart = TryParseLong(m.MilestoneObjectSchedule.DatePredictedStart),
                    DateUserEstimatedStart = TryParseLong(m.MilestoneObjectSchedule.DateUserEstimatedStart),
                    DateTargetedStart = TryParseLong(m.MilestoneObjectSchedule.DateTargetedStart),

                    DatePlannedDue = TryParseLong(m.MilestoneObjectSchedule.DatePlannedDue),
                    DateFixedDue = TryParseLong(m.MilestoneObjectSchedule.DateFixedDue),
                    DatePredictedDue = TryParseLong(m.MilestoneObjectSchedule.DatePredictedDue),
                    DateUserEstimatedDue = TryParseLong(m.MilestoneObjectSchedule.DateUserEstimatedDue),
                    DateTargetedDue = TryParseLong(m.MilestoneObjectSchedule.DateTargetedDue),

                    DateCompleted = TryParseLong(m.MilestoneObjectSchedule.DateCompleted),
                }
            }).ToList() ?? new List<PMMilestone>();

            return results;
        }

        private static int SafeInt(string? value)
            => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : 0;

        private static int? TryParseIntNullable(string? value)
            => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : (int?)null;

        private static long? TryParseLong(string? value)
            => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : (long?)null;

        private static decimal? TryParseDecimal(string? value)
            => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : (decimal?)null;
    }
}
