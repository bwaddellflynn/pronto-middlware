using Newtonsoft.Json;
using System.Collections.Generic;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Models
{
    public class AcceloApiResponse<T>
    {
        public List<T> Response { get; set; }
    }
}
