using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Pronto.Middleware.Models;
using System.Linq;
using static Pronto.Middleware.Controllers.AcceloGeneralController;
using Microsoft.Extensions.Logging;

namespace Pronto.Middleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AcceloDSAController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AcceloDSAController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public AcceloDSAController(IHttpClientFactory httpClientFactory, ILogger<AcceloDSAController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("contracts")]
        public async Task<IActionResult> GetContracts()
        {
            // Extract the token from the Authorization header
            if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return Unauthorized("Authorization header is missing.");
            }

            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
            {
                return Unauthorized("Invalid Authorization header.");
            }

            var accessToken = headerValue.Parameter;
            var contracts = await GetContractsFromAcceloAsync(accessToken);
            return Ok(contracts);
        }

        private async Task<List<Contract>> GetContractsFromAcceloAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Fetch contracts
            var response = await client.GetAsync($"{_baseUrl}contracts?_filters=standing(active)&_limit=100&_fields=breadcrumbs");

            if (!response.IsSuccessStatusCode)
            {
                return new List<Contract>();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse>(jsonResponse);

            // Filter contracts where title starts with "DSA"
            var dsaContracts = acceloResponse.Response
                .Where(contractResp => contractResp.Title.StartsWith(""))
                .ToList();

            var contracts = new List<Contract>();

            foreach (var contractResponse in dsaContracts)
            {
                var contract = new Contract
                {
                    Id = int.Parse(contractResponse.Id),
                    Title = contractResponse.Title,
                    Company = contractResponse.Breadcrumbs
                                .Where(b => b.Table == "company")
                                .Select(b => new Contract.CompanyInfo
                                {
                                    Id = int.Parse(b.Id),
                                    Name = b.Title
                                })
                                .FirstOrDefault()
                };

                // Fetch and add frequency information
                contract.Frequency = await GetFrequencyAsync(accessToken, contract.Id);
                contracts.Add(contract);
            }

            return contracts;
        }

        public class AcceloApiResponse
        {
            [JsonProperty("response")]
            public List<ContractResponse> Response { get; set; }

            public class ContractResponse
            {
                public string Id { get; set; }
                public string Title { get; set; }
                [JsonProperty("breadcrumbs")]
                public List<Breadcrumb> Breadcrumbs { get; set; }
            }

            public class Breadcrumb
            {
                public string Table { get; set; }
                public string Id { get; set; }
                public string Title { get; set; }
            }
        }

        private class ProfileResponse
        {
            [JsonProperty("response")]
            public List<ProfileFieldValue> Response { get; set; }
        }

        private class ProfileFieldValue
        {
            [JsonProperty("field_name")]
            public string FieldName { get; set; }
            public string Value { get; set; }
        }

        private async Task<string> GetFrequencyAsync(string accessToken, int contractId)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}contracts/{contractId}/profiles/values");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<ProfileResponse>(jsonResponse);

            return data.Response.FirstOrDefault(item => item.FieldName == "Reporting Frequency")?.Value;
        }

        [HttpGet("periods")]
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

            var response = await client.GetAsync($"{_baseUrl}contracts/{contractId}/periods?_fields=usage,id,total,date_commenced,date_closed,time_allocations&_filters=date_expires_after({startDate}),date_commenced_before({endDate})");

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
                TimeAllocations = periodResp.TimeAllocations.Select(ta => new TimeAllocation
                {
                    Against_Type = ta.Against_Type,
                    Against_Title = ta.Against_Title,
                    Against_Id = ta.Against_Id,
                    Billable = ta.Billable,
                    Nonbillable = ta.Nonbillable,
                    Period_Id = ta.Period_Id
                }).ToList()
            }).ToList();

            return periods;
        }


        public class PeriodsApiResponse
        {
            [JsonProperty("response")]
            public PeriodsResponse Response { get; set; }
        }

        public class PeriodsResponse
        {
            [JsonProperty("periods")]
            public List<ContractPeriod> Periods { get; set; }
        }

        public class ContractPeriod
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("usage")]
            public double Usage { get; set; }

            [JsonProperty("total")]
            public int Total { get; set; }

            [JsonProperty("date_commenced")]
            public long Date_Commenced { get; set; }

            [JsonProperty("date_closed")]
            public long? Date_Closed { get; set; }

            // Other properties as per JSON structure

            [JsonProperty("time_allocations")]
            public List<TimeAllocation> TimeAllocations { get; set; }
        }

        public class TimeAllocation
        {
            public string Against_Type { get; set; }
            public string Against_Title { get; set; }
            public int Against_Id { get; set; }
            public int Billable { get; set; }
            public int Nonbillable { get; set; }
            public int Period_Id { get; set; }
        }


        public class TimeAllocationResponse
        {
            [JsonProperty("against_type")]
            public string Against_Type { get; set; }

            [JsonProperty("against_title")]
            public string Against_Title { get; set; }

            [JsonProperty("against_id")]
            public string Against_Id { get; set; }

            [JsonProperty("billable")]
            public string Billable { get; set; }

            [JsonProperty("nonbillable")]
            public string Nonbillable { get; set; }

            [JsonProperty("period_id")]
            public string Period_Id { get; set; }
        }

        [HttpGet("issues")]
        public async Task<IActionResult> GetIssues(string issueId, long startDate, long endDate)
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
            var issues = await GetIssuesFromAcceloAsync(accessToken, issueId, startDate, endDate);
            return Ok(issues);
        }
        private async Task<List<Issue>> GetIssuesFromAcceloAsync(string accessToken, string  issueId, long startDate, long endDate)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);


            var response = await client.GetAsync($"{_baseUrl}issues?_limit=100&_filters=id({issueId}),billable_seconds_greater_than(0),date_modified_after({startDate}),date_modified_before({endDate})&_fields=against_id,resolution_detail,date_modified,standing,date_opened,billable_seconds,class(title)");

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<Issue>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<IssueResponse>>(jsonResponse);

            var issues = acceloResponse.Response.Select(issueResp => new Issue
            {
                Id = int.Parse(issueResp.Id),
                Title = issueResp.Title,
                Against_Id = int.Parse(issueResp.Against_Id),
                Resolution_Detail = issueResp.Resolution_Detail,
                Standing = issueResp.Standing,
                Date_Opened = long.Parse(issueResp.Date_Opened),
                Billable_Seconds = int.Parse(issueResp.Billable_Seconds),
                Class = issueResp.Class.Title,
            }).ToList();

            return issues;
        }

        public class AcceloApiResponse<T>
        {
            public List<T> Response { get; set; }
        }

        public class Issue
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public int Against_Id { get; set; }
            public string Resolution_Detail { get; set; }
            public string Standing { get; set; }
            public long Date_Opened { get; set; }
            public int Billable_Seconds { get; set; }
            public string Class { get; set; }
        }

        public class ClassResponse
        {
            public string Id { get; set; }
            public string Title { get; set; }
        }

        public class IssueResponse
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Against_Id { get; set; }
            public string Resolution_Detail { get; set; }
            public string Standing { get; set; }
            public string Date_Opened { get; set; }
            public string Billable_Seconds { get; set; }
            public ClassResponse Class { get; set; }
        }
    }
}