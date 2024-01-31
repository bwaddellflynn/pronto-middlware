namespace Pronto.Middleware.Models
{
    public class Contract
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public CompanyInfo Company { get; set; }
        public string Frequency { get; set; } 

        public class CompanyInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}