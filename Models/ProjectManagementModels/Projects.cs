namespace Pronto.Middleware.Models.ProjectManagementModels
{
    public class Project
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? JobType { get; set; }
        public string? TypeTitle { get; set; }
        public string? CompanyName { get; set; }
        public string? Status { get; set; }
        public string? Standing { get; set; }
        public string? Comments { get; set; }
        public long? DateCommenced { get; set; }
        public long? DateModified { get; set; }
        public long? DateCreated { get; set; }
        public long? DateStarted { get; set; }
        public long? DateDue { get; set; }
    }
}
