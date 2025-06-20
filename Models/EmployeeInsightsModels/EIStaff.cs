// src/Pronto.Middleware/Models/EmployeeInsights/EIStaff.cs
namespace Pronto.Middleware.Models.EmployeeInsights
{
    public class EIStaff
    {
        public int Id { get; set; }
        public string Firstname { get; set; } = default!;
        public string Surname { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string AccessLevel { get; set; } = default!;
    }
}
