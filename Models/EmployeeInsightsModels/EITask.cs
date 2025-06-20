namespace Pronto.Middleware.Models.EmployeeInsights
{
    /// <summary>
    /// DTO returned to frontend for tasks in Employee Insights.
    /// </summary>
    public class EITask
    {
        public string Id { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string? Description { get; set; }

        public string Status { get; set; } = default!;
        public string Standing { get; set; } = default!;
        public string Assignee { get; set; } = default!;

        public long DateCreated { get; set; }
        public long? DateDue { get; set; }
        public long? DateCompleted { get; set; }

        public string Billable { get; set; } = default!;
        public string NonBillable { get; set; } = default!;

        public string AgainstType { get; set; } = default!;
        public string AgainstId { get; set; } = default!;
    }
}
