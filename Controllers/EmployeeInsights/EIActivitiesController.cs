using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Pronto.Middleware.Models.EmployeeInsights;
using Pronto.Middleware.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Pronto.Middleware.Controllers.EmployeeInsights
{
    [ApiController]
    [Route("employeeinsights/activities")]
    public class EIActivitiesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EIActivitiesController> _logger;
        private const string BaseUrl = "https://perbyte.api.accelo.com/api/v0/";
        private static readonly HashSet<int> InternalNonBillableClasses = new() { 3, 5, 11, 12, 13, 14, 15 };
        private static readonly HashSet<string> InternalMilestoneTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            "internal meetings",
            "benefits",
            "client engagement",
            "operations",
            "training",
            "other",
            "internal technical activities"
        };
        private static readonly string[] InternalJobTitleKeywords = { "perbyte internal activities" };

        public EIActivitiesController(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            ILogger<EIActivitiesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
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

        [HttpGet("summary")]
        public async Task<ActionResult<IEnumerable<ActivitySummary>>> GetActivitySummaryAsync(
            [FromQuery] int limit = 100,
            [FromQuery] long? startDate = null,
            [FromQuery] long? endDate = null,
            [FromQuery] int? ownerId = null,
            [FromQuery] string? timeZone = "America/Chicago")
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                return Unauthorized("Missing Authorization header.");
            if (!AuthenticationHeaderValue.TryParse(authHeader, out var headerValue))
                return Unauthorized("Invalid Authorization header.");

            var token = headerValue.Parameter!;

            var filterList = new List<string>();
            if (ownerId.HasValue)
                filterList.Add($"owner_id({ownerId.Value})");
            if (startDate.HasValue)
                filterList.Add($"date_logged_after({startDate.Value})");
            if (endDate.HasValue)
                filterList.Add($"date_logged_before({endDate.Value})");
            var filters = filterList.Any() ? string.Join(",", filterList) : null;

            var fields = "id,date_logged,billable,nonbillable,owner_id,activity_class,against_type,against_id";
            var cacheKey = BuildSummaryCacheKey(token, limit, startDate, endDate, ownerId, timeZone, fields);
            if (_cache.TryGetValue(cacheKey, out List<ActivitySummary>? cachedSummary))
            {
                _logger.LogInformation("Employee Insights activity summary cache hit: {CacheKey}", cacheKey);
                return Ok(cachedSummary);
            }

            _logger.LogInformation("Employee Insights activity summary cache miss: {CacheKey}", cacheKey);
            var responses = await FetchActivityResponsesAsync(token, limit, fields, filters);
            var summary = BuildActivitySummary(responses, timeZone);
            _cache.Set(cacheKey, summary, BuildSummaryCacheOptions(endDate));

            return Ok(summary);
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
            var allResponses = await FetchActivityResponsesAsync(token, limit, fields, filters);

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
                ActivityClass = r.ActivityClass,
                ActivityPriority = r.ActivityPriority,
                Breadcrumbs = r.Breadcrumbs,
                Status = r.Standing
            }).ToList();
        }

        private async Task<List<ActivityResponse>> FetchActivityResponsesAsync(
            string token,
            int limit,
            string? fields,
            string? filters)
        {
            limit = Math.Clamp(limit, 1, 100);

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
                if (batch.Count < limit)
                    break;

                offset += batch.Count;
            }

            return allResponses;
        }

        private static List<ActivitySummary> BuildActivitySummary(
            IEnumerable<ActivityResponse> activities,
            string? timeZone)
        {
            var summaryByStaffDate = new Dictionary<string, ActivitySummary>();

            foreach (var activity in activities)
            {
                if (string.IsNullOrWhiteSpace(activity.OwnerId))
                    continue;

                var date = DateKeyInTimeZone(activity.DateLogged, timeZone);
                var key = $"{activity.OwnerId}:{date}";
                if (!summaryByStaffDate.TryGetValue(key, out var summary))
                {
                    summary = new ActivitySummary
                    {
                        StaffId = activity.OwnerId,
                        Date = date
                    };
                    summaryByStaffDate[key] = summary;
                }

                var billableSeconds = ParseSeconds(activity.Billable);
                var nonBillableSeconds = ParseSeconds(activity.NonBillable);
                summary.BillableSeconds += billableSeconds;
                summary.TotalSeconds += billableSeconds + nonBillableSeconds;

                if (nonBillableSeconds <= 0)
                    continue;

                if (IsInternalNonBillable(activity))
                {
                    summary.InternalNonBillableSeconds += nonBillableSeconds;
                }
                else
                {
                    summary.ClientNonBillableSeconds += nonBillableSeconds;
                }
            }

            return summaryByStaffDate.Values
                .OrderBy(s => s.StaffId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Date, StringComparer.Ordinal)
                .ToList();
        }

        private static int ParseSeconds(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : 0;
        }

        private static bool IsInternalNonBillable(ActivityResponse activity)
        {
            if (int.TryParse(activity.ActivityClass, NumberStyles.Integer, CultureInfo.InvariantCulture, out var activityClass)
                && InternalNonBillableClasses.Contains(activityClass))
            {
                return true;
            }

            if (activity.Breadcrumbs == null)
                return false;

            foreach (var crumb in activity.Breadcrumbs)
            {
                var table = crumb.Table?.Trim();
                var title = crumb.Title?.Trim();
                if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(title))
                    continue;

                if (table.Equals("milestone", StringComparison.OrdinalIgnoreCase)
                    && InternalMilestoneTitles.Contains(title))
                {
                    return true;
                }

                if (table.Equals("job", StringComparison.OrdinalIgnoreCase)
                    && InternalJobTitleKeywords.Any(keyword =>
                        title.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildSummaryCacheKey(
            string token,
            int limit,
            long? startDate,
            long? endDate,
            int? ownerId,
            string? timeZone,
            string fields)
        {
            return string.Join(
                ":",
                "employee-insights-summary",
                TokenHash(token),
                Math.Clamp(limit, 1, 100).ToString(CultureInfo.InvariantCulture),
                startDate?.ToString(CultureInfo.InvariantCulture) ?? "none",
                endDate?.ToString(CultureInfo.InvariantCulture) ?? "none",
                ownerId?.ToString(CultureInfo.InvariantCulture) ?? "all",
                string.IsNullOrWhiteSpace(timeZone) ? "America/Chicago" : timeZone.Trim(),
                fields);
        }

        private static string TokenHash(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).Substring(0, 16);
        }

        private static MemoryCacheEntryOptions BuildSummaryCacheOptions(long? endDate)
        {
            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };

            if (endDate.HasValue)
            {
                var rangeEnd = DateTimeOffset.FromUnixTimeSeconds(endDate.Value);
                var isHistorical = rangeEnd < DateTimeOffset.UtcNow.AddDays(-2);
                options.AbsoluteExpirationRelativeToNow = isHistorical
                    ? TimeSpan.FromHours(12)
                    : TimeSpan.FromMinutes(5);
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            }

            return options;
        }

        private static string DateKeyInTimeZone(long epochSeconds, string? timeZone)
        {
            var zoneId = string.IsNullOrWhiteSpace(timeZone) ? "America/Chicago" : timeZone;
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(epochSeconds), tz);
                return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch (TimeZoneNotFoundException)
            {
                if (zoneId.Equals("America/Chicago", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(epochSeconds), tz);
                        return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                    catch (TimeZoneNotFoundException) { }
                    catch (InvalidTimeZoneException) { }
                }

                return DateTimeOffset.FromUnixTimeSeconds(epochSeconds).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch (InvalidTimeZoneException)
            {
                return DateTimeOffset.FromUnixTimeSeconds(epochSeconds).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
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
                ActivityClass = r.ActivityClass,
                ActivityPriority = r.ActivityPriority,
                Breadcrumbs = r.Breadcrumbs,
                Status = r.Standing
            };
        }
    }
}
