namespace Pronto.Middleware.Services.EmployeeInsights
{
    public class EmployeeInsightsCacheOptions
    {
        public bool WarmupEnabled { get; set; } = true;
        public string WarmupStartMonthDay { get; set; } = "01-01";
        public int WarmupMaxConcurrency { get; set; } = 2;
        public int CurrentWeekRefreshMinutes { get; set; } = 60;
        public int RecentWeeksToRefresh { get; set; } = 4;
        public int Limit { get; set; } = 100;
        public string TimeZone { get; set; } = "America/Chicago";
        public string? WarmupAccessToken { get; set; }
    }
}
