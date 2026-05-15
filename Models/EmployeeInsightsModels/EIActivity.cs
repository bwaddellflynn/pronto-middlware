// src/Pronto.Middleware/Models/EmployeeInsights/ActivityModels.cs
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pronto.Middleware.Models.EmployeeInsights
{
    /// <summary>
    /// DTO returned to frontend for activities in Employee Insights.
    /// </summary>
    public class EIActivity
    {
        public string Id { get; set; } = default!;
        public string Subject { get; set; } = default!;
        public string? Details { get; set; }
        public long DateLogged { get; set; }
        public string Billable { get; set; } = default!;
        public string NonBillable { get; set; } = default!;
        public string OwnerId { get; set; } = default!;
        public string? StaffId { get; set; }
        public string TaskId { get; set; } = default!;
        public string AgainstType { get; set; } = default!;
        public string AgainstId { get; set; } = default!;
        [JsonProperty("activity_class")]
        public string ActivityClass { get; set; } = default!;
        public string? ActivityPriority { get; set; }
        public List<ActivityBreadcrumb>? Breadcrumbs { get; set; }
        public string Status { get; set; } = default!;
    }

    /// <summary>
    /// For deserializing a single activity record from Accelo API.
    /// </summary>
    public class ActivityResponse
    {
        [JsonProperty("id")] public string Id { get; set; } = default!;
        [JsonProperty("subject")] public string Subject { get; set; } = default!;
        [JsonProperty("details")] public string? Details { get; set; }
        [JsonProperty("date_logged")] public long DateLogged { get; set; }
        [JsonProperty("billable")] public string Billable { get; set; } = default!;
        [JsonProperty("nonbillable")] public string NonBillable { get; set; } = default!;
        [JsonProperty("owner_id")] public string OwnerId { get; set; } = default!;
        [JsonProperty("staff")] public string? StaffId { get; set; }
        [JsonProperty("task")] public string Task { get; set; } = default!;
        [JsonProperty("against_type")] public string AgainstType { get; set; } = default!;
        [JsonProperty("against_id")] public string AgainstId { get; set; } = default!;
        [JsonProperty("activity_class")] public string ActivityClass { get; set; } = default!;
        [JsonProperty("activity_priority")] public string? ActivityPriority { get; set; }
        [JsonProperty("breadcrumbs")] public List<ActivityBreadcrumb>? Breadcrumbs { get; set; }
        [JsonProperty("standing")] public string Standing { get; set; } = default!;
        // Add additional properties if needed
    }

    public class ActivityBreadcrumb
    {
        [JsonProperty("id")] public string Id { get; set; } = default!;
        [JsonProperty("table")] public string Table { get; set; } = default!;
        [JsonProperty("title")] public string Title { get; set; } = default!;
    }

    public class ActivitySummary
    {
        public string StaffId { get; set; } = default!;
        public string Date { get; set; } = default!;
        public int BillableSeconds { get; set; }
        public int ClientNonBillableSeconds { get; set; }
        public int InternalNonBillableSeconds { get; set; }
        public int PtoSeconds { get; set; }
        public int TotalSeconds { get; set; }
    }
}
