using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class OwnerAffiliation
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("email")]
        public string Email { get; set; }
    }
}
