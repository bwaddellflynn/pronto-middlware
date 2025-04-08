using Newtonsoft.Json;

namespace Pronto.Middleware.Models.ProjectManagementModels
{
    public class ProjectsResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("job_type")]
        public string? JobType { get; set; }

        [JsonProperty("type")]
        public ProjectType? Type { get; set; }

        [JsonProperty("breadcrumbs")]
        public List<Breadcrumb>? Breadcrumbs { get; set; }

        [JsonProperty("against_type")]
        public string AgainstType { get; set; }

        [JsonProperty("against_id")]
        public string AgainstId { get; set; }

        [JsonProperty("against")]
        public string Against { get; set; }

        [JsonProperty("company")]
        public string? Company { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("standing")]
        public string? Standing { get; set; }

        [JsonProperty("comments")]
        public string? Comments { get; set; }

        [JsonProperty("date_commenced")]
        public string? DateCommenced { get; set; }

        [JsonProperty("date_modified")]
        public string? DateModified { get; set; }

        [JsonProperty("date_created")]
        public string? DateCreated { get; set; }

        [JsonProperty("date_started")]
        public string? DateStarted { get; set; }

        [JsonProperty("date_due")]
        public string? DateDue { get; set; }

        [JsonProperty("rate")]
        public string? Rate { get; set; }

        [JsonProperty("rate_charged")]
        public string? RateCharged { get; set; }

        [JsonProperty("job_contract")]
        public string? JobContract { get; set; }

        [JsonProperty("manager")]
        public string? Manager { get; set; }

        [JsonProperty("paused")]
        public string? Paused { get; set; }

        [JsonProperty("staff_bookmarked")]
        public string? StaffBookmarked { get; set; }

        [JsonProperty("modified_by")]
        public string? ModifiedBy { get; set; }

        [JsonProperty("job_object_schedule")]
        public string? JobObjectSchedule { get; set; }

        [JsonProperty("job_object_budget")]
        public string? JobObjectBudget { get; set; }

        [JsonProperty("affiliation")]
        public string? Affiliation { get; set; }

        [JsonProperty("custom_id")]
        public string? CustomId { get; set; }

        [JsonProperty("date_completed")]
        public string? DateCompleted { get; set; }
    }

    public class ProjectType
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }

    public class Breadcrumb
    {
        [JsonProperty("table")]
        public string Table { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }
}
