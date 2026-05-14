namespace Pronto.Middleware.Services.EmployeeInsights
{
    public class EmployeeInsightsWeekRange
    {
        public string Key { get; set; } = default!;
        public long StartDate { get; set; }
        public long EndDate { get; set; }
        public string StartIso { get; set; } = default!;
        public string EndIso { get; set; } = default!;
    }
}
