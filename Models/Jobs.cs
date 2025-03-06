using Newtonsoft.Json;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Models
{

    public class Job
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Against_Id { get; set; }
        public string Standing { get; set; }
        public long Date_Commenced { get; set; }
        public long Date_Modified { get; set; }
        public string Class { get; set; }
    }

    public class JobResponse
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Against_Id { get; set; }
        public string Standing { get; set; }
        public string Date_Commenced { get; set; }
        public string Date_Modified { get; set; }
        public ClassResponse Class { get; set; }
    }
}
