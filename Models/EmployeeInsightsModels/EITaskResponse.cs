using Newtonsoft.Json;

namespace Pronto.Middleware.Models.EmployeeInsights
{
    /// <summary>
    /// For deserializing a single task record from Accelo API.
    /// </summary>
    public class EITaskResponse
    {
        [JsonProperty("id")] public string Id { get; set; } = default!;
        [JsonProperty("title")] public string Title { get; set; } = default!;
        [JsonProperty("description")] public string? Description { get; set; }

        [JsonProperty("status")] public string Status { get; set; } = default!;
        [JsonProperty("standing")] public string Standing { get; set; } = default!;
        [JsonProperty("assignee")] public string Assignee { get; set; } = default!;

        [JsonProperty("date_created")] public long DateCreated { get; set; }
        [JsonProperty("date_due")] public long? DateDue { get; set; }
        [JsonProperty("date_completed")] public long? DateCompleted { get; set; }

        [JsonProperty("billable")] public string Billable { get; set; } = default!;
        [JsonProperty("nonbillable")] public string NonBillable { get; set; } = default!;

        [JsonProperty("against_type")] public string AgainstType { get; set; } = default!;
        [JsonProperty("against_id")] public string AgainstId { get; set; } = default!;

        // 🆕 Fields for task progress / status bar
        [JsonProperty("remaining")] public int Remaining { get; set; }
        [JsonProperty("logged")] public int Logged { get; set; }
        [JsonProperty("object_budget")] public int ObjectBudget { get; set; }
    }
}
