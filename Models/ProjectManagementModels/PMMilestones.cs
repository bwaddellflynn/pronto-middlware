namespace Pronto.Middleware.Models.ProjectManagementModels
{
    public class PMMilestone
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Job { get; set; }
        public string? Description { get; set; }
        public string? Standing { get; set; }
        public long? DateCommenced { get; set; }
        public long? DateStarted { get; set; }
        public long? DateDue { get; set; }
        public long? DateCreated { get; set; }
        public long? DateModified { get; set; }
        public long? DateCompleted { get; set; }
    }
}
