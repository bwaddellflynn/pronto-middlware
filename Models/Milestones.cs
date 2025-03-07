using Newtonsoft.Json;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Models
{

    public class Milestone
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Job { get; set; }
        public string Description { get; set; }
        public string Standing { get; set; }
        public long Date_Opened { get; set; }
        public int Billable_Seconds { get; set; }
        public string Class { get; set; }
    }

    public class MilestoneResponse
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Job { get; set; }
        public string Description { get; set; }
        public string Standing { get; set; }
        public string Date_Opened { get; set; }
        public string Billable_Seconds { get; set; }
        public string Class { get; set; }
    }
}
