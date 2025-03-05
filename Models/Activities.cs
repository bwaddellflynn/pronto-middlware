using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class Activity
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("against_type")]
        public string AgainstType { get; set; }

        [JsonProperty("against_id")]
        public string AgainstId { get; set; }

        [JsonProperty("task")]
        public string Task { get; set; }

        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("billable")]
        public string Billable { get; set; }

        [JsonProperty("nonbillable")]
        public string Nonbillable { get; set; }

        [JsonProperty("time_allocation")]
        public string TimeAllocation { get; set; }

        [JsonProperty("subject")]
        public string Subject { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }

        [JsonProperty("date_created")]
        public long DateCreated { get; set; }

        [JsonProperty("date_modified")]
        public long DateModified { get; set; }
    }
}
