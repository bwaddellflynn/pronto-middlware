using Newtonsoft.Json;

namespace Pronto.Middleware.Models.ProjectManagementModels
{
    public class PMTaskResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("standing")]
        public string? Standing { get; set; }

        [JsonProperty("against_id")]
        public string AgainstId { get; set; }

        [JsonProperty("against_type")]
        public string AgainstType { get; set; }

        [JsonProperty("milestone")]
        public string? MilestoneId { get; set; }

        [JsonProperty("date_commenced")]
        public string? DateCommenced { get; set; }

        [JsonProperty("date_due")]
        public string? DateDue { get; set; }

        [JsonProperty("date_completed")]
        public string? DateCompleted { get; set; }

        [JsonProperty("task_priority")]
        public string? TaskPriority { get; set; }

        [JsonProperty("assignee")]
        public string? Assignee { get; set; }

        [JsonProperty("task_status")]
        public string? TaskStatus { get; set; }

        [JsonProperty("task_type")]
        public string? TaskType { get; set; }

        [JsonProperty("manager")]
        public string? Manager { get; set; }
    }
}
