using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class UserData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("firstname")]
        public string FirstName { get; set; }

        [JsonProperty("surname")]
        public string Surname { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }

    public class AcceloUserResponse<T>
    {
        [JsonProperty("response")]
        public T Response { get; set; }
    }
}
