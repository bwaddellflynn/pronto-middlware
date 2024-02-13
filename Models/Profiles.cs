using Newtonsoft.Json;
using System.Collections.Generic; 

namespace Pronto.Middleware.Models
{
    public class CustomFieldsResponse
    {
        [JsonProperty("response")]
        public List<CustomField> Response { get; set; }
    }

    public class CustomField
    {
        [JsonProperty("field_name")]
        public string FieldName { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("field_type")]
        public string FieldType { get; set; }

        [JsonProperty("link_type")]
        public string LinkType { get; set; }

        [JsonProperty("value_type")]
        public string ValueType { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class UpdateFrequencyRequest
    {
        public string ContractId { get; set; }
        public string Frequency { get; set; }
    }
}