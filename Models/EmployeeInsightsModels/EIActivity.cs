// src/Pronto.Middleware/Models/EmployeeInsights/ActivityModels.cs
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
        public string TaskId { get; set; } = default!;
        public string AgainstType { get; set; } = default!;
        public string AgainstId { get; set; } = default!;
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
        [JsonProperty("task")] public string Task { get; set; } = default!;
        [JsonProperty("against_type")] public string AgainstType { get; set; } = default!;
        [JsonProperty("against_id")] public string AgainstId { get; set; } = default!;
        [JsonProperty("standing")] public string Standing { get; set; } = default!;
        // Add additional properties if needed
    }
}
