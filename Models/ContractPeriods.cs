using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class ContractPeriod
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("usage")]
        public double Usage { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("date_commenced")]
        public long Date_Commenced { get; set; }

        [JsonProperty("date_closed")]
        public long? Date_Closed { get; set; }

        // Other properties as per JSON structure

        [JsonProperty("time_allocations")]
        public List<TimeAllocation> TimeAllocations { get; set; }
    }
}
