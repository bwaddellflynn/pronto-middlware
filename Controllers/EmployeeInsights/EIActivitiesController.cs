using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pronto.Middleware.Models.EmployeeInsights;
using Pronto.Middleware.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        private const int MaxConcurrentActivityPageRequests = 30;
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

            var filters = BuildActivityFilters(startDate, endDate, ownerId);

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
            var endpointStopwatch = Stopwatch.StartNew();
            var phaseStopwatch = Stopwatch.StartNew();
            var requestId = HttpContext.TraceIdentifier;

            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, StartDate={StartDate}, EndDate={EndDate}, OwnerId={OwnerId}, Limit={Limit}, TimeZone={TimeZone}",
                requestId,
                "start",
                0,
                startDate,
                endDate,
                ownerId,
                limit,
                timeZone);

            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                return Unauthorized("Missing Authorization header.");
            if (!AuthenticationHeaderValue.TryParse(authHeader, out var headerValue))
                return Unauthorized("Invalid Authorization header.");

            var token = headerValue.Parameter!;

            var filters = BuildActivityFilters(startDate, endDate, ownerId);

            var fields = "id,date_logged,billable,nonbillable,staff,owner_id,activity_class,against_type,against_id,breadcrumbs";
            var cacheKey = BuildSummaryCacheKey(token, limit, startDate, endDate, ownerId, timeZone, fields);
            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, Filters={Filters}, CacheKey={CacheKey}",
                requestId,
                "setup",
                phaseStopwatch.ElapsedMilliseconds,
                endpointStopwatch.ElapsedMilliseconds,
                filters ?? "none",
                cacheKey);
            phaseStopwatch.Restart();

            if (_cache.TryGetValue(cacheKey, out List<ActivitySummary>? cachedSummary))
            {
                _logger.LogInformation(
                    "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, SummaryRows={SummaryRows}, CacheKey={CacheKey}",
                    requestId,
                    "cache-hit",
                    phaseStopwatch.ElapsedMilliseconds,
                    endpointStopwatch.ElapsedMilliseconds,
                    cachedSummary.Count,
                    cacheKey);
                return Ok(cachedSummary);
            }

            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, CacheKey={CacheKey}",
                requestId,
                "cache-miss",
                phaseStopwatch.ElapsedMilliseconds,
                endpointStopwatch.ElapsedMilliseconds,
                cacheKey);
            phaseStopwatch.Restart();

            var summary = await FetchActivitySummaryAsync(token, limit, fields, filters, timeZone);
            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, SummaryRows={SummaryRows}",
                requestId,
                "fetch-and-build-summary",
                phaseStopwatch.ElapsedMilliseconds,
                endpointStopwatch.ElapsedMilliseconds,
                summary.Count);
            phaseStopwatch.Restart();

            _cache.Set(cacheKey, summary, BuildSummaryCacheOptions(endDate));
            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, SummaryRows={SummaryRows}",
                requestId,
                "cache-set",
                phaseStopwatch.ElapsedMilliseconds,
                endpointStopwatch.ElapsedMilliseconds,
                summary.Count);

            endpointStopwatch.Stop();
            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, SummaryRows={SummaryRows}",
                requestId,
                "controller-before-ok",
                endpointStopwatch.ElapsedMilliseconds,
                summary.Count);

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
                StaffId = r.StaffId,
                TaskId = r.Task,
                AgainstType = r.AgainstType,
                AgainstId = r.AgainstId,
                ActivityClass = r.ActivityClass,
                ActivityPriority = r.ActivityPriority,
                Breadcrumbs = r.Breadcrumbs,
                Status = r.Standing
            }).ToList();
        }

        private static string? BuildActivityFilters(long? startDate, long? endDate, int? staffId)
        {
            var filterList = new List<string>();
            if (staffId.HasValue)
                filterList.Add($"staff({staffId.Value})");
            if (startDate.HasValue)
                filterList.Add($"date_logged_after({startDate.Value})");
            if (endDate.HasValue)
                filterList.Add($"date_logged_before({endDate.Value})");

            return filterList.Any() ? string.Join(",", filterList) : null;
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

            try
            {
                var count = await FetchActivityCountAsync(client, filters);
                if (count == 0)
                    return new List<ActivityResponse>();

                var offsets = Enumerable.Range(0, (int)Math.Ceiling(count / (double)limit))
                    .Select(page => page * limit)
                    .ToList();
                var stopwatch = Stopwatch.StartNew();

                _logger.LogInformation(
                    "Fetching Accelo Activities concurrently: Count={Count}, Pages={Pages}, Limit={Limit}, MaxConcurrency={MaxConcurrency}, Filters={Filters}",
                    count,
                    offsets.Count,
                    limit,
                    MaxConcurrentActivityPageRequests,
                    filters ?? "none");

                using var throttler = new SemaphoreSlim(MaxConcurrentActivityPageRequests);
                var tasks = offsets.Select(async offset =>
                {
                    await throttler.WaitAsync();
                    try
                    {
                        return await FetchActivityPageAsync(client, limit, offset, fields, filters);
                    }
                    finally
                    {
                        throttler.Release();
                    }
                });

                var pages = await Task.WhenAll(tasks);
                stopwatch.Stop();

                var allResponses = pages.SelectMany(page => page).ToList();
                _logger.LogInformation(
                    "Fetched Accelo Activities concurrently: Count={Count}, Pages={Pages}, Activities={Activities}, DurationMs={DurationMs}",
                    count,
                    offsets.Count,
                    allResponses.Count,
                    stopwatch.ElapsedMilliseconds);

                return allResponses;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Concurrent Accelo Activities fetch failed; falling back to sequential pagination.");
                return await FetchActivityResponsesSequentialAsync(client, limit, fields, filters);
            }
        }

        private async Task<List<ActivitySummary>> FetchActivitySummaryAsync(
            string token,
            int limit,
            string? fields,
            string? filters,
            string? timeZone)
        {
            limit = Math.Clamp(limit, 1, 100);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var totalStopwatch = Stopwatch.StartNew();
                var phaseStopwatch = Stopwatch.StartNew();
                var count = await FetchActivityCountAsync(client, filters);
                _logger.LogInformation(
                    "Employee Insights summary timing: Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, RawCount={RawCount}, Filters={Filters}",
                    "accelo-count",
                    phaseStopwatch.ElapsedMilliseconds,
                    totalStopwatch.ElapsedMilliseconds,
                    count,
                    filters ?? "none");

                if (count == 0)
                    return new List<ActivitySummary>();

                phaseStopwatch.Restart();
                var offsets = Enumerable.Range(0, (int)Math.Ceiling(count / (double)limit))
                    .Select(page => page * limit)
                    .ToList();

                _logger.LogInformation(
                    "Fetching Accelo Activity summary pages concurrently: Count={Count}, Pages={Pages}, Limit={Limit}, MaxConcurrency={MaxConcurrency}, Filters={Filters}",
                    count,
                    offsets.Count,
                    limit,
                    MaxConcurrentActivityPageRequests,
                    filters ?? "none");

                using var throttler = new SemaphoreSlim(MaxConcurrentActivityPageRequests);
                var tasks = offsets.Select(async offset =>
                {
                    await throttler.WaitAsync();
                    try
                    {
                        var page = await FetchActivityPageAsync(client, limit, offset, fields, filters);
                        return BuildActivitySummary(page, timeZone);
                    }
                    finally
                    {
                        throttler.Release();
                    }
                });

                var pageSummaries = await Task.WhenAll(tasks);
                _logger.LogInformation(
                    "Employee Insights summary timing: Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, RawCount={RawCount}, Pages={Pages}, PageSummaryRows={PageSummaryRows}",
                    "accelo-page-fetch-and-page-summary",
                    phaseStopwatch.ElapsedMilliseconds,
                    totalStopwatch.ElapsedMilliseconds,
                    count,
                    offsets.Count,
                    pageSummaries.Sum(page => page.Count));

                phaseStopwatch.Restart();
                var summary = MergeActivitySummaries(pageSummaries.SelectMany(page => page));
                totalStopwatch.Stop();

                _logger.LogInformation(
                    "Fetched Accelo Activity summaries concurrently: RawCount={RawCount}, Pages={Pages}, SummaryRows={SummaryRows}, DurationMs={DurationMs}",
                    count,
                    offsets.Count,
                    summary.Count,
                    totalStopwatch.ElapsedMilliseconds);
                _logger.LogInformation(
                    "Employee Insights summary timing: Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, RawCount={RawCount}, Pages={Pages}, SummaryRows={SummaryRows}",
                    "merge-page-summaries",
                    phaseStopwatch.ElapsedMilliseconds,
                    totalStopwatch.ElapsedMilliseconds,
                    count,
                    offsets.Count,
                    summary.Count);

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Concurrent Accelo Activity summary fetch failed; falling back to sequential summary pagination.");
                return await FetchActivitySummarySequentialAsync(client, limit, fields, filters, timeZone);
            }
        }

        private async Task<List<ActivitySummary>> FetchActivitySummarySequentialAsync(
            HttpClient client,
            int limit,
            string? fields,
            string? filters,
            string? timeZone)
        {
            var pageSummaries = new List<ActivitySummary>();
            int offset = 0;

            while (true)
            {
                var batch = await FetchActivityPageAsync(client, limit, offset, fields, filters);
                if (batch.Count == 0)
                    break;

                pageSummaries.AddRange(BuildActivitySummary(batch, timeZone));
                if (batch.Count < limit)
                    break;

                offset += batch.Count;
            }

            return MergeActivitySummaries(pageSummaries);
        }

        private async Task<int> FetchActivityCountAsync(HttpClient client, string? filters)
        {
            var url = $"{BaseUrl}activities/count";
            if (!string.IsNullOrWhiteSpace(filters))
                url += $"?_filters={filters}";

            _logger.LogInformation("Fetching Accelo Activities count: {Url}", url);
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Accelo Activities count response [{Status}]: {Json}", response.StatusCode, json);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error fetching activities count: {response.StatusCode} {json}");
            }

            var parsed = JObject.Parse(json);
            var responseToken = parsed["response"];
            var countToken = responseToken?["count"] ?? responseToken?.First?["count"];
            if (countToken == null || !int.TryParse(countToken.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                throw new InvalidOperationException($"Unable to parse activities count response: {json}");
            }

            return count;
        }

        private async Task<List<ActivityResponse>> FetchActivityPageAsync(
            HttpClient client,
            int limit,
            int offset,
            string? fields,
            string? filters)
        {
            var url = $"{BaseUrl}activities?_limit={limit}&_offset={offset}&_fields={fields}";
            if (!string.IsNullOrWhiteSpace(filters))
                url += $"&_filters={filters}";

            _logger.LogInformation("Fetching Accelo Activities page: Offset={Offset}, Url={Url}", offset, url);
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Accelo Activities page response [{Status}] Offset={Offset}: {Json}", response.StatusCode, offset, json);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error fetching activities page at offset {offset}: {response.StatusCode} {json}");
            }

            var parsed = JsonConvert.DeserializeObject<AcceloApiResponse<ActivityResponse>>(json);
            return parsed?.Response ?? new List<ActivityResponse>();
        }

        private async Task<List<ActivityResponse>> FetchActivityResponsesSequentialAsync(
            HttpClient client,
            int limit,
            string? fields,
            string? filters)
        {
            var allResponses = new List<ActivityResponse>();
            int offset = 0;

            while (true)
            {
                var batch = await FetchActivityPageAsync(client, limit, offset, fields, filters);
                if (batch.Count == 0)
                    break;

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
                var staffId = StaffIdForActivity(activity);
                if (string.IsNullOrWhiteSpace(staffId))
                    continue;

                var date = DateKeyInTimeZone(activity.DateLogged, timeZone);
                var key = $"{staffId}:{date}";
                if (!summaryByStaffDate.TryGetValue(key, out var summary))
                {
                    summary = new ActivitySummary
                    {
                        StaffId = staffId,
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

        private static List<ActivitySummary> MergeActivitySummaries(IEnumerable<ActivitySummary> summaries)
        {
            var summaryByStaffDate = new Dictionary<string, ActivitySummary>();

            foreach (var source in summaries)
            {
                if (string.IsNullOrWhiteSpace(source.StaffId) || string.IsNullOrWhiteSpace(source.Date))
                    continue;

                var key = $"{source.StaffId}:{source.Date}";
                if (!summaryByStaffDate.TryGetValue(key, out var target))
                {
                    target = new ActivitySummary
                    {
                        StaffId = source.StaffId,
                        Date = source.Date
                    };
                    summaryByStaffDate[key] = target;
                }

                target.BillableSeconds += source.BillableSeconds;
                target.ClientNonBillableSeconds += source.ClientNonBillableSeconds;
                target.InternalNonBillableSeconds += source.InternalNonBillableSeconds;
                target.TotalSeconds += source.TotalSeconds;
            }

            return summaryByStaffDate.Values
                .OrderBy(s => s.StaffId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Date, StringComparer.Ordinal)
                .ToList();
        }

        private static string? StaffIdForActivity(ActivityResponse activity)
        {
            return string.IsNullOrWhiteSpace(activity.StaffId)
                ? activity.OwnerId
                : activity.StaffId;
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
                StaffId = r.StaffId,
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
