using Newtonsoft.Json;
using System.Collections.Generic;

namespace Pronto.Middleware.Models
{
    public class AffiliationResponse
    {
        [JsonProperty("response")]
        public List<Affiliation> Response { get; set; }
    }

    public class Affiliation
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("company")]
        public string Company { get; set; }

        [JsonProperty("contact")]
        public Contact Contact { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("mobile")]
        public string Mobile { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; }

        [JsonProperty("standing")]
        public string Standing { get; set; }

    }

    public class Contact
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("firstname")]
        public string Firstname { get; set; }

        [JsonProperty("surname")]
        public string Surname { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("mobile")]
        public string Mobile { get; set; }
    }
}