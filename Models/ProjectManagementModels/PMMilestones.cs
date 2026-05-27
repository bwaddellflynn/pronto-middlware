namespace Pronto.Middleware.Models.ProjectManagementModels
{
    public class PMMilestone
    {
        // Existing required fields
        public int Id { get; set; }
        public string Title { get; set; }
        public int Job { get; set; }

        // Existing optional fields
        public string? Description { get; set; }
        public string? Standing { get; set; }
        public long? DateCommenced { get; set; }
        public long? DateStarted { get; set; }
        public long? DateDue { get; set; }
        public long? DateCreated { get; set; }
        public long? DateModified { get; set; }
        public long? DateCompleted { get; set; }

        // Newly (or recently) exposed fields
        public string? Status { get; set; }               // deprecated by Accelo, keep for now
        public string? MilestoneStatus { get; set; }      // preferred
        public decimal? Rate { get; set; }
        public decimal? RateCharged { get; set; }
        public PMMilestoneBudget? MilestoneObjectBudget { get; set; }
        public long? LoggedSubtotalSeconds { get; set; }
        public long? BillableSubtotalSeconds { get; set; }
        public long? NonBillableSubtotalSeconds { get; set; }
        public long? RemainingSubtotalSeconds { get; set; }
        public long? ServiceTimeSubtotalEstimateSeconds { get; set; }
        public int? Ordering { get; set; }
        public int? Manager { get; set; }
        public int? Parent { get; set; }

        // NEW: typed schedule block (epoch seconds as long?)
        public PMMilestoneSchedule? MilestoneObjectSchedule { get; set; }
    }

    public class PMMilestoneSchedule
    {
        public int Id { get; set; }
        public string? AgainstType { get; set; }
        public int? AgainstId { get; set; }

        public long? DateCommenced { get; set; }

        public long? DatePlannedStart { get; set; }
        public long? DateFixedStart { get; set; }
        public long? DatePredictedStart { get; set; }
        public long? DateUserEstimatedStart { get; set; }
        public long? DateTargetedStart { get; set; }

        public long? DatePlannedDue { get; set; }
        public long? DateFixedDue { get; set; }
        public long? DatePredictedDue { get; set; }
        public long? DateUserEstimatedDue { get; set; }
        public long? DateTargetedDue { get; set; }

        public long? DateCompleted { get; set; }
    }

    public class PMMilestoneBudget
    {
        public long? LoggedSubtotalSeconds { get; set; }
        public long? BillableSubtotalSeconds { get; set; }
        public long? NonBillableSubtotalSeconds { get; set; }
        public long? RemainingSubtotalSeconds { get; set; }
        public long? ServiceTimeSubtotalEstimateSeconds { get; set; }
    }
}
