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
        public string? Assignee { get; set; }
        public string? TaskStatus { get; set; }
        public string? TaskType { get; set; }
        public string? Manager { get; set; }
    }
}
