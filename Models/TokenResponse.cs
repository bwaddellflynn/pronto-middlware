using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;

public class TokenResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}
