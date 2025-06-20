// src/Pronto.Middleware/Models/EmployeeInsights/StaffResponse.cs
using Newtonsoft.Json;

namespace Pronto.Middleware.Models.EmployeeInsights
{
    public class StaffResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; } = default!;

        [JsonProperty("firstname")]
        public string Firstname { get; set; } = default!;

        [JsonProperty("surname")]
        public string Surname { get; set; } = default!;

        [JsonProperty("email")]
        public string Email { get; set; } = default!;

        [JsonProperty("access_level")]
        public string AccessLevel { get; set; } = default!;

        // expand  with additional fields as needed, e.g.:
        // [JsonProperty("position")] public string Position { get; set; }
        // [JsonProperty("standing")] public string Standing { get; set; }
    }
}
