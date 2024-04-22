namespace Pronto.Middleware.Models
{
    public class AuthStatusResponse
    {
        public bool IsAuthenticated { get; set; }
        public string Username { get; set; }
    }
}
