using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{

    public class Issue
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Against_Id { get; set; }
        public string Resolution_Detail { get; set; }
        public string Standing { get; set; }
        public long Date_Opened { get; set; }
        public int Billable_Seconds { get; set; }
        public string Class { get; set; }
    }

    public class ClassResponse
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }

    public class IssueResponse
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Against_Id { get; set; }
        public string Resolution_Detail { get; set; }
        public string Standing { get; set; }
        public string Date_Opened { get; set; }
        public string Billable_Seconds { get; set; }
        public ClassResponse Class { get; set; }
    }
}
