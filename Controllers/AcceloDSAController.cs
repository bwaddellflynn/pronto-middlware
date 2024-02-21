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
using System.Text;

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
        #region Contracts
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
            var contractsResponse = await client.GetAsync($"{_baseUrl}contracts?_filters=standing(active)&_limit=100&_fields=breadcrumbs,owner_affiliation()");
            if (!contractsResponse.IsSuccessStatusCode)
            {
                return new List<Contract>();
            }

            var contractsJsonResponse = await contractsResponse.Content.ReadAsStringAsync();
            var acceloContractsResponse = JsonConvert.DeserializeObject<AcceloApiResponse>(contractsJsonResponse);

            // Fetch affiliations separately
            var affiliations = await GetAffiliationsFromAcceloAsync(accessToken);

            var contracts = new List<Contract>();

            foreach (var contractResponse in acceloContractsResponse.Response)
            {
                var dsaAgreementCustomField = await GetCustomFieldAsync(accessToken, contractResponse.Id.ToString(), "DSA Agreement");
                if (dsaAgreementCustomField == null || dsaAgreementCustomField.Value != "Yes")
                {
                    continue;
                }

                var reportingFrequencyCustomField = await GetCustomFieldAsync(accessToken, contractResponse.Id.ToString(), "Reporting Frequency");

                var companyInfo = contractResponse.Breadcrumbs
                    .Where(b => b.Table == "company")
                    .Select(b => new Contract.CompanyInfo
                    {
                        Id = int.Parse(b.Id),
                        Name = b.Title
                    })
                    .FirstOrDefault();

                // Find the affiliation(s) for this contract's company
                var companyAffiliations = affiliations
                    .Where(a => a.Company == companyInfo?.Id.ToString())
                    .ToList(); // Assuming Company property in Affiliation class matches CompanyInfo.Id

                // For simplicity, let's assume we're only interested in the first affiliation
                var affiliation = companyAffiliations.FirstOrDefault();

                var contract = new Contract
                {
                    Id = int.Parse(contractResponse.Id),
                    Title = contractResponse.Title,
                    Company = companyInfo,
                    Frequency = reportingFrequencyCustomField,
                    DSA_Agreement = dsaAgreementCustomField,
                    Affiliation = affiliation // Set the Affiliation property
                };

                contracts.Add(contract);
            }

            return contracts;
        }


        private async Task<CustomField> GetCustomFieldAsync(string accessToken, string contractId, string fieldName)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}contracts/{contractId}/profiles/values");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to fetch custom fields for contract {contractId}. Status code: {response.StatusCode}");
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<CustomFieldsResponse>(jsonResponse);

            return data.Response.FirstOrDefault(field => field.FieldName == fieldName);
        }

        private async Task<List<Affiliation>> GetAffiliationsFromAcceloAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}affiliations?_fields=_ALL,contact()");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to fetch affiliations. Status code: {response.StatusCode}");
                return new List<Affiliation>();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var affiliationsResponse = JsonConvert.DeserializeObject<AffiliationResponse>(jsonResponse);
            return affiliationsResponse.Response;
        }

        #endregion

        #region Update Frequency

        #endregion

        #region Periods
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

        #endregion

        #region Issues
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
        private async Task<List<AcceloGeneralController.Issue>> GetIssuesFromAcceloAsync(string accessToken, string issueId, long startDate, long endDate)
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
                return new List<AcceloGeneralController.Issue>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloGeneralController.AcceloApiResponse<AcceloGeneralController.IssueResponse>>(jsonResponse);

            var issues = acceloResponse.Response.Select(issueResp => new AcceloGeneralController.Issue
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

        #endregion
    }
}