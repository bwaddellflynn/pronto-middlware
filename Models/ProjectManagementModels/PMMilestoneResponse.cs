using Newtonsoft.Json;

namespace Pronto.Middleware.Models.ProjectManagementModels
{
    public class PMMilestoneResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("job")]
        public string Job { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("standing")]
        public string? Standing { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("milestone_status")]
        public string? MilestoneStatus { get; set; }

        [JsonProperty("rate")]
        public string? Rate { get; set; }

        [JsonProperty("rate_charged")]
        public string? RateCharged { get; set; }

        [JsonProperty("date_created")]
        public string? DateCreated { get; set; }

        [JsonProperty("date_modified")]
        public string? DateModified { get; set; }

        [JsonProperty("date_commenced")]
        public string? DateCommenced { get; set; }

        [JsonProperty("date_started")]
        public string? DateStarted { get; set; }

        [JsonProperty("date_due")]
        public string? DateDue { get; set; }

        [JsonProperty("date_completed")]
        public string? DateCompleted { get; set; }

        [JsonProperty("milestone_object_budget")]
        public string? MilestoneObjectBudget { get; set; }

        [JsonProperty("milestone_object_schedule")]
        public string? MilestoneObjectSchedule { get; set; }

        [JsonProperty("ordering")]
        public string? Ordering { get; set; }

        [JsonProperty("manager")]
        public string? Manager { get; set; }

        [JsonProperty("parent")]
        public string? Parent { get; set; }
    }
}
