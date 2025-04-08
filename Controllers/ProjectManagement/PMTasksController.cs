using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Pronto.Middleware.Models;
using Pronto.Middleware.Models.ProjectManagementModels;
using System.Net.Http.Headers;

namespace Pronto.Middleware.Controllers.ProjectManagement
{
    [ApiController]
    [Route("projectmanagement/tasks")]
    public class PMTasksController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PMTasksController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public PMTasksController(IHttpClientFactory httpClientFactory, ILogger<PMTasksController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks(
            [FromQuery] int limit = 100,
            [FromQuery(Name = "fields")] string? fields = "_ALL",
            [FromQuery(Name = "filters")] string? filters = null)
        {
            if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader) ||
                !AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
            {
                return Unauthorized("Missing or invalid authorization header.");
            }

            var token = headerValue.Parameter;
            var tasks = await FetchTasksAsync(token, limit, fields, filters);
            return Ok(tasks);
        }

        private async Task<List<PMTask>> FetchTasksAsync(string token, int limit, string? fields, string? filters)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var query = $"{_baseUrl}tasks?_limit={limit}&_fields={fields}";

            if (!string.IsNullOrWhiteSpace(filters))
            {
                query += $"&_filters={filters}";
            }

            _logger.LogInformation("[FetchTasks] Requesting: {Query}", query);

            var response = await client.GetAsync(query);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[FetchTasks] Response Code: {StatusCode}", response.StatusCode);
            _logger.LogDebug("[FetchTasks] Raw JSON: {Json}", json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[FetchTasks] Failed to fetch tasks from Accelo: {StatusCode} - {Json}", response.StatusCode, json);
                return new List<PMTask>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<PMTaskResponse>>(json);

            return acceloResponse?.Response.Select(task => new PMTask
            {
                Id = int.Parse(task.Id),
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Standing = task.Standing,
                AgainstId = int.TryParse(task.AgainstId, out var aid) ? aid : 0,
                AgainstType = task.AgainstType,
                MilestoneId = int.TryParse(task.MilestoneId, out var mid) ? mid : null,
                DateCommenced = long.TryParse(task.DateCommenced, out var d1) ? d1 : null,
                DateDue = long.TryParse(task.DateDue, out var d2) ? d2 : null,
                DateCompleted = long.TryParse(task.DateCompleted, out var d3) ? d3 : null,
                TaskPriority = task.TaskPriority,
                Assignee = task.Assignee,
                TaskStatus = task.TaskStatus,
                TaskType = task.TaskType,
                Manager = task.Manager
            }).ToList() ?? new List<PMTask>();
        }
    }
}
