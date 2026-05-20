using Pronto.Middleware.Models.EmployeeInsights;

namespace Pronto.Middleware.Services.EmployeeInsights
{
    public interface IEmployeeInsightsActivitySummaryService
    {
        Task<List<ActivitySummary>> GetActivitySummaryAsync(
            string token,
            int limit,
            long? startDate,
            long? endDate,
            int? ownerId,
            string? timeZone,
            string? requestId = null,
            CancellationToken cancellationToken = default);

        Task WarmCurrentYearAsync(string token, CancellationToken cancellationToken = default);

        Task RefreshRecentWeeksAsync(string token, CancellationToken cancellationToken = default);

        string? GetLatestWarmupToken();
    }
}
