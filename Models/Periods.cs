using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class PeriodsApiResponse
    {
        [JsonProperty("response")]
        public PeriodsResponse Response { get; set; }
    }

    public class PeriodsResponse
    {
        [JsonProperty("periods")]
        public List<ContractPeriod> Periods { get; set; }
    }
}
