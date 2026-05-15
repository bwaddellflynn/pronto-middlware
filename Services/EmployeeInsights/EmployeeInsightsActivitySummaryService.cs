using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pronto.Middleware.Models;
using Pronto.Middleware.Models.EmployeeInsights;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Pronto.Middleware.Services.EmployeeInsights
{
    public class EmployeeInsightsActivitySummaryService : IEmployeeInsightsActivitySummaryService
    {
        private const string BaseUrl = "https://perbyte.api.accelo.com/api/v0/";
        private const int MaxConcurrentActivityPageRequests = 30;
        private const string SummaryFields = "id,date_logged,billable,nonbillable,staff,owner_id,activity_class,against_type,against_id,breadcrumbs";

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
        private static readonly HashSet<string> PtoMilestoneTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            "benefits"
        };
        private static readonly HashSet<string> PtoTaskTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            "pto"
        };
        private static readonly string[] InternalJobTitleKeywords = { "perbyte internal activities" };
        private static readonly ConcurrentDictionary<string, Lazy<Task<List<ActivitySummary>>>> PendingSummaryRequests = new();
        private static readonly ConcurrentDictionary<string, byte> WarmedTokenHashes = new();
        private static string? LatestWarmupToken;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EmployeeInsightsActivitySummaryService> _logger;
        private readonly EmployeeInsightsCacheOptions _options;

        public EmployeeInsightsActivitySummaryService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            ILogger<EmployeeInsightsActivitySummaryService> logger,
            IOptions<EmployeeInsightsCacheOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<List<ActivitySummary>> GetActivitySummaryAsync(
            string token,
            int limit,
            long? startDate,
            long? endDate,
            int? ownerId,
            string? timeZone,
            string? requestId = null,
            CancellationToken cancellationToken = default)
        {
            RememberWarmupToken(token);
            var endpointStopwatch = Stopwatch.StartNew();
            var phaseStopwatch = Stopwatch.StartNew();
            var normalizedLimit = Math.Clamp(limit, 1, 100);
            var normalizedTimeZone = NormalizeTimeZone(timeZone);
            var filters = BuildActivityFilters(startDate, endDate, ownerId);
            var cacheKey = BuildSummaryCacheKey(token, normalizedLimit, startDate, endDate, ownerId, normalizedTimeZone, SummaryFields);

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
                return cachedSummary;
            }

            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, CacheKey={CacheKey}",
                requestId,
                "cache-miss",
                phaseStopwatch.ElapsedMilliseconds,
                endpointStopwatch.ElapsedMilliseconds,
                cacheKey);

            var pendingRequest = PendingSummaryRequests.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<List<ActivitySummary>>>(() => FetchAndCacheSummaryAsync(
                    token,
                    normalizedLimit,
                    startDate,
                    endDate,
                    filters,
                    normalizedTimeZone,
                    cacheKey,
                    requestId,
                    cancellationToken)));

            if (pendingRequest.IsValueCreated)
            {
                _logger.LogInformation(
                    "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, TotalDurationMs={TotalDurationMs}, CacheKey={CacheKey}",
                    requestId,
                    "in-flight-reuse",
                    phaseStopwatch.ElapsedMilliseconds,
                    endpointStopwatch.ElapsedMilliseconds,
                    cacheKey);
            }

            try
            {
                return await pendingRequest.Value;
            }
            finally
            {
                PendingSummaryRequests.TryRemove(cacheKey, out _);
            }
        }

        public async Task WarmCurrentYearAsync(string token, CancellationToken cancellationToken = default)
        {
            RememberWarmupToken(token);
            if (!_options.WarmupEnabled)
            {
                _logger.LogInformation("Employee Insights cache warm-up skipped because warm-up is disabled.");
                return;
            }

            var tokenHash = TokenHash(token);
            if (!WarmedTokenHashes.TryAdd(tokenHash, 0))
            {
                _logger.LogInformation("Employee Insights cache warm-up skipped because this token has already been warmed. TokenHash={TokenHash}", tokenHash);
                return;
            }

            var warmupStopwatch = Stopwatch.StartNew();
            var ranges = BuildCurrentYearWeekRanges(_options.WarmupStartMonthDay);
            var maxConcurrency = Math.Clamp(_options.WarmupMaxConcurrency, 1, 5);

            _logger.LogInformation(
                "Employee Insights cache warm-up started. TokenHash={TokenHash}, WeekCount={WeekCount}, MaxConcurrency={MaxConcurrency}, TimeZone={TimeZone}",
                tokenHash,
                ranges.Count,
                maxConcurrency,
                _options.TimeZone);

            var nextRangeIndex = 0;
            var completed = 0;
            var failed = 0;

            async Task RunWorker()
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var rangeIndex = Interlocked.Increment(ref nextRangeIndex) - 1;
                    if (rangeIndex >= ranges.Count)
                        return;

                    var range = ranges[rangeIndex];
                    var weekStopwatch = Stopwatch.StartNew();
                    try
                    {
                        _logger.LogInformation(
                            "Employee Insights cache warm-up week started. WeekStart={WeekStart}, WeekEnd={WeekEnd}",
                            range.StartIso,
                            range.EndIso);

                        var summary = await GetActivitySummaryAsync(
                            token,
                            _options.Limit,
                            range.StartDate,
                            range.EndDate,
                            ownerId: null,
                            timeZone: _options.TimeZone,
                            requestId: "cache-warmup",
                            cancellationToken);

                        Interlocked.Increment(ref completed);
                        _logger.LogInformation(
                            "Employee Insights cache warm-up week completed. WeekStart={WeekStart}, WeekEnd={WeekEnd}, DurationMs={DurationMs}, SummaryRows={SummaryRows}",
                            range.StartIso,
                            range.EndIso,
                            weekStopwatch.ElapsedMilliseconds,
                            summary.Count);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        _logger.LogWarning(
                            ex,
                            "Employee Insights cache warm-up week failed. WeekStart={WeekStart}, WeekEnd={WeekEnd}, DurationMs={DurationMs}",
                            range.StartIso,
                            range.EndIso,
                            weekStopwatch.ElapsedMilliseconds);
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(0, Math.Min(maxConcurrency, ranges.Count)).Select(_ => RunWorker()));

            _logger.LogInformation(
                "Employee Insights cache warm-up completed. TokenHash={TokenHash}, DurationMs={DurationMs}, WeekCount={WeekCount}, CompletedWeeks={CompletedWeeks}, FailedWeeks={FailedWeeks}",
                tokenHash,
                warmupStopwatch.ElapsedMilliseconds,
                ranges.Count,
                completed,
                failed);
        }

        public async Task RefreshRecentWeeksAsync(string token, CancellationToken cancellationToken = default)
        {
            if (!_options.WarmupEnabled)
                return;

            RememberWarmupToken(token);
            var refreshStopwatch = Stopwatch.StartNew();
            var now = DateTimeOffset.UtcNow;
            var start = now.AddDays(-Math.Max(1, _options.RecentWeeksToRefresh) * 7);
            var startDay = new DateTimeOffset(start.Year, start.Month, start.Day, 0, 0, 0, TimeSpan.Zero);
            var endDay = new DateTimeOffset(now.Year, now.Month, now.Day, 23, 59, 59, TimeSpan.Zero);
            var ranges = BuildWeeklySummaryRanges(startDay.ToUnixTimeSeconds(), endDay.ToUnixTimeSeconds());
            var maxConcurrency = Math.Clamp(_options.WarmupMaxConcurrency, 1, 5);
            var completed = 0;
            var failed = 0;
            var nextRangeIndex = 0;

            _logger.LogInformation(
                "Employee Insights recent-week cache refresh started. WeekCount={WeekCount}, MaxConcurrency={MaxConcurrency}, TimeZone={TimeZone}",
                ranges.Count,
                maxConcurrency,
                _options.TimeZone);

            async Task RunWorker()
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var rangeIndex = Interlocked.Increment(ref nextRangeIndex) - 1;
                    if (rangeIndex >= ranges.Count)
                        return;

                    var range = ranges[rangeIndex];
                    var cacheKey = BuildSummaryCacheKey(
                        token,
                        _options.Limit,
                        range.StartDate,
                        range.EndDate,
                        ownerId: null,
                        timeZone: _options.TimeZone,
                        fields: SummaryFields);
                    var weekStopwatch = Stopwatch.StartNew();

                    try
                    {
                        _cache.Remove(cacheKey);
                        var summary = await GetActivitySummaryAsync(
                            token,
                            _options.Limit,
                            range.StartDate,
                            range.EndDate,
                            ownerId: null,
                            timeZone: _options.TimeZone,
                            requestId: "recent-week-refresh",
                            cancellationToken);

                        Interlocked.Increment(ref completed);
                        _logger.LogInformation(
                            "Employee Insights recent-week cache refresh completed. WeekStart={WeekStart}, WeekEnd={WeekEnd}, DurationMs={DurationMs}, SummaryRows={SummaryRows}",
                            range.StartIso,
                            range.EndIso,
                            weekStopwatch.ElapsedMilliseconds,
                            summary.Count);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        _logger.LogWarning(
                            ex,
                            "Employee Insights recent-week cache refresh failed. WeekStart={WeekStart}, WeekEnd={WeekEnd}, DurationMs={DurationMs}",
                            range.StartIso,
                            range.EndIso,
                            weekStopwatch.ElapsedMilliseconds);
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(0, Math.Min(maxConcurrency, ranges.Count)).Select(_ => RunWorker()));

            _logger.LogInformation(
                "Employee Insights recent-week cache refresh finished. DurationMs={DurationMs}, WeekCount={WeekCount}, CompletedWeeks={CompletedWeeks}, FailedWeeks={FailedWeeks}",
                refreshStopwatch.ElapsedMilliseconds,
                ranges.Count,
                completed,
                failed);
        }

        public string? GetLatestWarmupToken()
        {
            return LatestWarmupToken;
        }

        private async Task<List<ActivitySummary>> FetchAndCacheSummaryAsync(
            string token,
            int limit,
            long? startDate,
            long? endDate,
            string? filters,
            string timeZone,
            string cacheKey,
            string? requestId,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var summary = await FetchActivitySummaryAsync(token, limit, SummaryFields, filters, timeZone, cancellationToken);

            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, SummaryRows={SummaryRows}",
                requestId,
                "fetch-and-build-summary",
                stopwatch.ElapsedMilliseconds,
                summary.Count);

            _cache.Set(cacheKey, summary, BuildSummaryCacheOptions(endDate));
            _logger.LogInformation(
                "Employee Insights summary timing: RequestId={RequestId}, Phase={Phase}, DurationMs={DurationMs}, SummaryRows={SummaryRows}, CacheKey={CacheKey}",
                requestId,
                "cache-set",
                stopwatch.ElapsedMilliseconds,
                summary.Count,
                cacheKey);

            return summary;
        }

        private async Task<List<ActivitySummary>> FetchActivitySummaryAsync(
            string token,
            int limit,
            string? fields,
            string? filters,
            string? timeZone,
            CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var totalStopwatch = Stopwatch.StartNew();
                var phaseStopwatch = Stopwatch.StartNew();
                var count = await FetchActivityCountAsync(client, filters, cancellationToken);
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
                    await throttler.WaitAsync(cancellationToken);
                    try
                    {
                        var page = await FetchActivityPageAsync(client, limit, offset, fields, filters, cancellationToken);
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Concurrent Accelo Activity summary fetch failed; falling back to sequential summary pagination.");
                return await FetchActivitySummarySequentialAsync(client, limit, fields, filters, timeZone, cancellationToken);
            }
        }

        private async Task<List<ActivitySummary>> FetchActivitySummarySequentialAsync(
            HttpClient client,
            int limit,
            string? fields,
            string? filters,
            string? timeZone,
            CancellationToken cancellationToken)
        {
            var pageSummaries = new List<ActivitySummary>();
            int offset = 0;

            while (true)
            {
                var batch = await FetchActivityPageAsync(client, limit, offset, fields, filters, cancellationToken);
                if (batch.Count == 0)
                    break;

                pageSummaries.AddRange(BuildActivitySummary(batch, timeZone));
                if (batch.Count < limit)
                    break;

                offset += batch.Count;
            }

            return MergeActivitySummaries(pageSummaries);
        }

        private async Task<int> FetchActivityCountAsync(HttpClient client, string? filters, CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}activities/count";
            if (!string.IsNullOrWhiteSpace(filters))
                url += $"?_filters={filters}";

            _logger.LogInformation("Fetching Accelo Activities count: {Url}", url);
            var response = await client.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Accelo Activities count response [{Status}]: {Json}", response.StatusCode, json);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Error fetching activities count: {response.StatusCode} {json}");

            var parsed = JObject.Parse(json);
            var responseToken = parsed["response"];
            var countToken = responseToken?["count"] ?? responseToken?.First?["count"];
            if (countToken == null || !int.TryParse(countToken.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                throw new InvalidOperationException($"Unable to parse activities count response: {json}");

            return count;
        }

        private async Task<List<ActivityResponse>> FetchActivityPageAsync(
            HttpClient client,
            int limit,
            int offset,
            string? fields,
            string? filters,
            CancellationToken cancellationToken)
        {
            var url = $"{BaseUrl}activities?_limit={limit}&_offset={offset}&_fields={fields}";
            if (!string.IsNullOrWhiteSpace(filters))
                url += $"&_filters={filters}";

            _logger.LogInformation("Fetching Accelo Activities page: Offset={Offset}, Url={Url}", offset, url);
            var response = await client.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Accelo Activities page response [{Status}] Offset={Offset}: {Json}", response.StatusCode, offset, json);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Error fetching activities page at offset {offset}: {response.StatusCode} {json}");

            var parsed = JsonConvert.DeserializeObject<AcceloApiResponse<ActivityResponse>>(json);
            return parsed?.Response ?? new List<ActivityResponse>();
        }

        private static List<ActivitySummary> BuildActivitySummary(IEnumerable<ActivityResponse> activities, string? timeZone)
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

                if (IsPtoActivity(activity))
                    summary.PtoSeconds += nonBillableSeconds;
                else if (IsInternalNonBillable(activity))
                    summary.InternalNonBillableSeconds += nonBillableSeconds;
                else
                    summary.ClientNonBillableSeconds += nonBillableSeconds;
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
                target.PtoSeconds += source.PtoSeconds;
                target.TotalSeconds += source.TotalSeconds;
            }

            return summaryByStaffDate.Values
                .OrderBy(s => s.StaffId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Date, StringComparer.Ordinal)
                .ToList();
        }

        private List<EmployeeInsightsWeekRange> BuildCurrentYearWeekRanges(string startMonthDay)
        {
            var now = DateTimeOffset.UtcNow;
            var startParts = startMonthDay.Split('-', StringSplitOptions.RemoveEmptyEntries);
            var month = startParts.Length == 2 && int.TryParse(startParts[0], out var parsedMonth) ? parsedMonth : 1;
            var day = startParts.Length == 2 && int.TryParse(startParts[1], out var parsedDay) ? parsedDay : 1;
            var start = new DateTimeOffset(now.Year, Math.Clamp(month, 1, 12), Math.Clamp(day, 1, 28), 0, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(now.Year, now.Month, now.Day, 23, 59, 59, TimeSpan.Zero);
            return BuildWeeklySummaryRanges(start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());
        }

        private static List<EmployeeInsightsWeekRange> BuildWeeklySummaryRanges(long startDate, long endDate)
        {
            var ranges = new List<EmployeeInsightsWeekRange>();
            var end = DateTimeOffset.FromUnixTimeSeconds(endDate).UtcDateTime;
            var cursor = DateTimeOffset.FromUnixTimeSeconds(startDate).UtcDateTime;
            cursor = new DateTime(cursor.Year, cursor.Month, cursor.Day, 0, 0, 0, DateTimeKind.Utc);
            var endUtc = new DateTime(end.Year, end.Month, end.Day, 23, 59, 59, DateTimeKind.Utc);

            while (cursor <= endUtc)
            {
                var weekStart = cursor;
                var weekEnd = cursor.AddDays(6 - (((int)cursor.DayOfWeek + 6) % 7));
                weekEnd = new DateTime(weekEnd.Year, weekEnd.Month, weekEnd.Day, 23, 59, 59, DateTimeKind.Utc);

                if (weekEnd > endUtc)
                    weekEnd = endUtc;

                var startEpoch = new DateTimeOffset(weekStart).ToUnixTimeSeconds();
                var endEpoch = new DateTimeOffset(weekEnd).ToUnixTimeSeconds();
                ranges.Add(new EmployeeInsightsWeekRange
                {
                    Key = $"{startEpoch}-{endEpoch}",
                    StartDate = startEpoch,
                    EndDate = endEpoch,
                    StartIso = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    EndIso = weekEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                });

                cursor = weekEnd.AddDays(1).Date;
            }

            return ranges;
        }

        private static string? StaffIdForActivity(ActivityResponse activity)
        {
            return string.IsNullOrWhiteSpace(activity.StaffId) ? activity.OwnerId : activity.StaffId;
        }

        private static int ParseSeconds(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ? seconds : 0;
        }

        private static bool IsInternalNonBillable(ActivityResponse activity)
        {
            if (int.TryParse(activity.ActivityClass, NumberStyles.Integer, CultureInfo.InvariantCulture, out var activityClass)
                && InternalNonBillableClasses.Contains(activityClass))
                return true;

            if (activity.Breadcrumbs == null)
                return false;

            foreach (var crumb in activity.Breadcrumbs)
            {
                var table = crumb.Table?.Trim();
                var title = crumb.Title?.Trim();
                if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(title))
                    continue;

                if (table.Equals("milestone", StringComparison.OrdinalIgnoreCase) && InternalMilestoneTitles.Contains(title))
                    return true;

                if (table.Equals("job", StringComparison.OrdinalIgnoreCase)
                    && InternalJobTitleKeywords.Any(keyword => title.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        private static bool IsPtoActivity(ActivityResponse activity)
        {
            if (activity.Breadcrumbs == null)
                return false;

            var hasBenefitsMilestone = false;
            var hasPtoTask = false;

            foreach (var crumb in activity.Breadcrumbs)
            {
                var table = crumb.Table?.Trim();
                var title = crumb.Title?.Trim();
                if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(title))
                    continue;

                if (table.Equals("milestone", StringComparison.OrdinalIgnoreCase) && PtoMilestoneTitles.Contains(title))
                    hasBenefitsMilestone = true;

                if (table.Equals("task", StringComparison.OrdinalIgnoreCase) && PtoTaskTitles.Contains(title))
                    hasPtoTask = true;
            }

            return hasBenefitsMilestone && hasPtoTask;
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
                NormalizeTimeZone(timeZone),
                fields);
        }

        private static string TokenHash(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).Substring(0, 16);
        }

        private MemoryCacheEntryOptions BuildSummaryCacheOptions(long? endDate)
        {
            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };

            if (!endDate.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CurrentWeekRefreshMinutes);
                return options;
            }

            var rangeEnd = DateTimeOffset.FromUnixTimeSeconds(endDate.Value);
            var age = DateTimeOffset.UtcNow - rangeEnd;
            if (age.TotalDays <= 7)
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CurrentWeekRefreshMinutes);
            else if (age.TotalDays <= (_options.RecentWeeksToRefresh * 7))
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            else
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(72);

            return options;
        }

        private static string DateKeyInTimeZone(long epochSeconds, string? timeZone)
        {
            var zoneId = NormalizeTimeZone(timeZone);
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

        private static string NormalizeTimeZone(string? timeZone)
        {
            return string.IsNullOrWhiteSpace(timeZone) ? "America/Chicago" : timeZone.Trim();
        }

        private static void RememberWarmupToken(string token)
        {
            if (!string.IsNullOrWhiteSpace(token))
                LatestWarmupToken = token;
        }
    }
}
