using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class TimeAllocation
    {
        public string Against_Type { get; set; }
        public string Against_Title { get; set; }
        public int Against_Id { get; set; }
        public int Billable { get; set; }
        public int Nonbillable { get; set; }
        public int Period_Id { get; set; }
    }

    public class TimeAllocationResponse
    {
        [JsonProperty("against_type")]
        public string Against_Type { get; set; }

        [JsonProperty("against_title")]
        public string Against_Title { get; set; }

        [JsonProperty("against_id")]
        public string Against_Id { get; set; }

        [JsonProperty("billable")]
        public string Billable { get; set; }

        [JsonProperty("nonbillable")]
        public string Nonbillable { get; set; }

        [JsonProperty("period_id")]
        public string Period_Id { get; set; }
    }
}
