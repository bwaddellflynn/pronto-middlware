using Newtonsoft.Json;

namespace Pronto.Middleware.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Company_Status { get; set; }
    }

    public class CompanyResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Company_Status { get; set; }
    }
}
