using Newtonsoft.Json;

namespace Pronto.Middleware.Models.ProjectManagementModels
{
    public class PMMilestoneResponse
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("job")] public string Job { get; set; }

        [JsonProperty("description")] public string? Description { get; set; }
        [JsonProperty("standing")] public string? Standing { get; set; }

        // Prefer milestone_status over deprecated status, but keep both for now
        [JsonProperty("status")] public string? Status { get; set; }
        [JsonProperty("milestone_status")] public string? MilestoneStatus { get; set; }

        [JsonProperty("rate")] public string? Rate { get; set; }
        [JsonProperty("rate_charged")] public string? RateCharged { get; set; }

        [JsonProperty("date_created")] public string? DateCreated { get; set; }
        [JsonProperty("date_modified")] public string? DateModified { get; set; }
        [JsonProperty("date_commenced")] public string? DateCommenced { get; set; }
        [JsonProperty("date_started")] public string? DateStarted { get; set; }
        [JsonProperty("date_due")] public string? DateDue { get; set; }
        [JsonProperty("date_completed")] public string? DateCompleted { get; set; }

        [JsonProperty("milestone_object_budget")] public string? MilestoneObjectBudget { get; set; }

        // CHANGED: was string?; now a nested object
        [JsonProperty("milestone_object_schedule")]
        public MilestoneObjectScheduleResponse? MilestoneObjectSchedule { get; set; }

        [JsonProperty("ordering")] public string? Ordering { get; set; }
        [JsonProperty("manager")] public string? Manager { get; set; }
        [JsonProperty("parent")] public string? Parent { get; set; }

        // Deprecated but still present in payload; keep so we can ignore/sunset later
        [JsonProperty("object_budget")] public string? ObjectBudget_Deprecated { get; set; }
    }

    public class MilestoneObjectScheduleResponse
    {
        [JsonProperty("id")] public string? Id { get; set; }
        [JsonProperty("against_type")] public string? AgainstType { get; set; }
        [JsonProperty("against_id")] public string? AgainstId { get; set; }

        [JsonProperty("date_commenced")] public string? DateCommenced { get; set; }

        [JsonProperty("date_planned_start")] public string? DatePlannedStart { get; set; }
        [JsonProperty("date_fixed_start")] public string? DateFixedStart { get; set; }
        [JsonProperty("date_predicted_start")] public string? DatePredictedStart { get; set; }
        [JsonProperty("date_user_estimated_start")] public string? DateUserEstimatedStart { get; set; }
        [JsonProperty("date_targeted_start")] public string? DateTargetedStart { get; set; }

        [JsonProperty("date_planned_due")] public string? DatePlannedDue { get; set; }
        [JsonProperty("date_fixed_due")] public string? DateFixedDue { get; set; }
        [JsonProperty("date_predicted_due")] public string? DatePredictedDue { get; set; }
        [JsonProperty("date_user_estimated_due")] public string? DateUserEstimatedDue { get; set; }
        [JsonProperty("date_targeted_due")] public string? DateTargetedDue { get; set; }

        [JsonProperty("date_completed")] public string? DateCompleted { get; set; }
    }
}
