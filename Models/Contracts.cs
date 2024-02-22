using Newtonsoft.Json;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Models
{
    public class Contract
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public CompanyInfo Company { get; set; }
        public OwnerAffiliation OwnerAffiliation { get; set; }

        public Affiliation Affiliation { get; set; }

        public CustomField Frequency { get; set; }
        public CustomField DSA_Agreement { get; set; }

        public class CompanyInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }

    public class AcceloApiResponse
    {
        [JsonProperty("response")]
        public List<ContractResponse> Response { get; set; }

        public class ContractResponse
        {
            public string Id { get; set; }
            public string Title { get; set; }
            [JsonProperty("breadcrumbs")]
            public List<Breadcrumb> Breadcrumbs { get; set; }
            [JsonProperty("owner_affiliation")]
            public string OwnerAffiliationId { get; set; } // Ensure this matches the JSON structure.
        }

        public class Breadcrumb
        {
            public string Table { get; set; }
            public string Id { get; set; }
            public string Title { get; set; }
        }
    }
}