using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class OwnerAffiliation
    {
        [JsonProperty("owner_affiliation")]
        public string Id { get; set; }
    }
}
