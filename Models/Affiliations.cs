using Newtonsoft.Json;
using System.Collections.Generic;

namespace Pronto.Middleware.Models
{
    public class Affiliation
    {
        public string Id { get; set; }
        public string Company { get; set; }
        public Contact Contact { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Position { get; set; }
        public string InvoiceMethod { get; set; }
    }

    public class Contact
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
    }

    public class AcceloAffiliationResponse
    {
        [JsonProperty("response")]
        public AffiliationResponse Data { get; set; }
    }

    public class AffiliationResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("company")]
        public string Company { get; set; }

        [JsonProperty("contact")]
        public ContactResponse Contact { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("mobile")]
        public string Mobile { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; }

        [JsonProperty("invoice_method")]
        public string InvoiceMethod { get; set; }
    }

    public class ContactResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("firstname")]
        public string Firstname { get; set; }

        [JsonProperty("surname")]
        public string Surname { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }
}