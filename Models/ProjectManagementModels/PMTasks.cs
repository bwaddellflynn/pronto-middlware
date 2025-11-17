// Pronto.Middleware.Models.ProjectManagementModels.PMTask.cs
namespace Pronto.Middleware.Models.ProjectManagementModels
{
    public class PMTask
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Standing { get; set; }
        public int AgainstId { get; set; }
        public string AgainstType { get; set; }
        public int? MilestoneId { get; set; }
        public long? DateCommenced { get; set; }
        public long? DateDue { get; set; }
        public long? DateCompleted { get; set; }
        public string? TaskPriority { get; set; }
        public TaskAssignee? Assignee { get; set; }
        public string? TaskStatus { get; set; }
        public string? TaskType { get; set; }
        public string? Manager { get; set; }

        // New properties for billing and budgeting
        public long Billable { get; set; }
        public long NonBillable { get; set; }
        public long Remaining { get; set; }
        public long Logged { get; set; }
        public long ObjectBudget { get; set; }
    }

    public class TaskAssignee
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? Surname { get; set; }
    }
}