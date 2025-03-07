using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class Milestone
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Job { get; set; }
        public string Description { get; set; }
        public string Standing { get; set; }
        public long Date_Commenced { get; set; }
    }

    public class MilestoneResponse
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Job { get; set; }
        public string Description { get; set; }
        public string Standing { get; set; }
        public string Date_Commenced { get; set; }
    }
}
