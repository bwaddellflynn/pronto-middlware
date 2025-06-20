using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Pronto.Middleware.Models;
using Pronto.Middleware.Models.EmployeeInsights;
using System.Net.Http.Headers;

namespace Pronto.Middleware.Controllers.EmployeeInsights
{
    [ApiController]
    [Route("employeeinsights/tasks")]
    public class EITasksController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EITasksController> _logger;
        private const string BaseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public EITasksController(
            IHttpClientFactory httpClientFactory,
            ILogger<EITasksController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EITask>>> GetTasksAsync(
            [FromQuery] int limit = 100,
            [FromQuery(Name = "fields")] string? fields = "id,title,description,status,standing,assignee,date_created,date_due,date_completed,billable,nonbillable,against_type,against_id",
            [FromQuery] string? filters = null)

        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                return Unauthorized("Missing Authorization header.");
            if (!AuthenticationHeaderValue.TryParse(authHeader, out var headerValue))
                return Unauthorized("Invalid Authorization header.");

            var token = headerValue.Parameter!;

            // Build filter string
            var filterString = filters;

            var tasks = await FetchTasksAsync(token, limit, fields, filterString);
            return Ok(tasks);
        }

        private async Task<List<EITask>> FetchTasksAsync(
            string token,
            int limit,
            string? fields,
            string? filters)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var allResponses = new List<EITaskResponse>();
            int offset = 0;

            while (true)
            {
                var url = $"{BaseUrl}tasks?_limit={limit}&_offset={offset}&_fields={fields}";
                if (!string.IsNullOrWhiteSpace(filters))
                    url += $"&_filters={filters}";

                _logger.LogInformation("Fetching Accelo Tasks: {Url}", url);
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("Accelo Task Response [{Status}]: {Json}", response.StatusCode, json);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error fetching tasks: {Status} {Json}", response.StatusCode, json);
                    break;
                }

                var parsed = JsonConvert.DeserializeObject<AcceloApiResponse<EITaskResponse>>(json);
                var batch = parsed?.Response ?? new List<EITaskResponse>();
                if (batch.Count == 0)
                    break;

                allResponses.AddRange(batch);
                offset += batch.Count;
            }

            return allResponses.Select(r => new EITask
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Status = r.Status,
                Standing = r.Standing,
                Assignee = r.Assignee,
                DateCreated = r.DateCreated,
                DateDue = r.DateDue,
                DateCompleted = r.DateCompleted,
                Billable = r.Billable,
                NonBillable = r.NonBillable,
                AgainstType = r.AgainstType,
                AgainstId = r.AgainstId
            }).ToList();
        }
    }
}
