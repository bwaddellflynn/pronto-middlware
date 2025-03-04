using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Pronto.Middleware.Models;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PeriodsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PeriodsController> _logger;
        private readonly string _baseUrl;

        public PeriodsController(IHttpClientFactory httpClientFactory, ILogger<PeriodsController> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrl = configuration["Startup:AcceloConfig:BaseUrl"];
        }

        [HttpGet]
        public async Task<IActionResult> GetContractPeriods(int contractId, long startDate, long endDate)
        {
            if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return Unauthorized("Authorization header is missing.");
            }

            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
            {
                return Unauthorized("Invalid Authorization header.");
            }

            var accessToken = headerValue.Parameter;
            var periods = await GetContractPeriodsFromAcceloAsync(accessToken, contractId, startDate, endDate);
            return Ok(periods);
        }

        private async Task<List<ContractPeriod>> GetContractPeriodsFromAcceloAsync(string accessToken, int contractId, long startDate, long endDate)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}contracts/{contractId}/periods?_fields=usage,id,total,date_commenced,date_closed,budget,budget_used,time_allocations&_filters=date_expires_after({startDate}),date_commenced_before({endDate})");

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<ContractPeriod>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<PeriodsApiResponse>(jsonResponse);

            var periods = acceloResponse.Response.Periods.Select(periodResp => new ContractPeriod
            {
                Id = periodResp.Id,
                Usage = periodResp.Usage,
                Total = periodResp.Total,
                Date_Commenced = periodResp.Date_Commenced,
                Date_Closed = periodResp.Date_Closed,
                Budget = periodResp.Budget,
                Budget_Used = periodResp.Budget_Used,
                TimeAllocations = periodResp.TimeAllocations?.Select(ta => new TimeAllocation
                {
                    Against_Type = ta.Against_Type,
                    Against_Title = ta.Against_Title,
                    Against_Id = ta.Against_Id,
                    Billable = ta.Billable,
                    Nonbillable = ta.Nonbillable,
                    Period_Id = ta.Period_Id
                }).ToList()
            }).ToList();

            // Logging for debugging
            foreach (var period in periods)
            {
                _logger.LogInformation("Period ID: {Id}, Budget: {Budget}, Budget Used: {BudgetUsed}",
                    period.Id,
                    period.Budget?.Value,
                    period.Budget_Used?.Value);
            }

            return periods;
        }
    }
}
