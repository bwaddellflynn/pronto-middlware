namespace Pronto.Middleware.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Company { get; set; }
        public int AgainstId { get; set; }
        public int Contract { get; set; }
        public int? BillableSeconds { get; set; }
        public DateTime? DateClosed { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateLastInteracted { get; set; }
    }
}