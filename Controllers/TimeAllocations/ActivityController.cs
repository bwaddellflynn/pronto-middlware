using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
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
    public class TasksController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TasksController> _logger;
        private readonly string _baseUrl;

        public TasksController(IHttpClientFactory httpClientFactory, ILogger<TasksController> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrl = configuration["Startup:AcceloConfig:BaseUrl"];
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks(string againstType, string againstId, long date_modified_after, long date_modified_before)
        {
            if (string.IsNullOrEmpty(againstType) || (againstType != "job" && againstType != "issue" && againstType != "task"))
            {
                return BadRequest("Invalid againstType. Must be 'job', 'issue', or 'task'.");
            }

            if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return Unauthorized("Authorization header is missing.");
            }

            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
            {
                return Unauthorized("Invalid Authorization header.");
            }

            var accessToken = headerValue.Parameter;

            if (againstType == "task")
            {
                var taskActivities = await GetTaskFromAcceloAsync(accessToken, againstId, date_modified_after, date_modified_before);
                return Ok(taskActivities);
            }
            else
            {
                var tasks = await GetTasksFromAcceloAsync(accessToken, againstType, againstId, date_modified_after, date_modified_before);
                return Ok(tasks);
            }
        }

        /// <summary>
        /// Fetches activities related to jobs/issues (batch call).
        /// </summary>
        private async Task<List<Activity>> GetTasksFromAcceloAsync(string accessToken, string againstType, string againstId, long date_modified_after, long date_modified_before)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var requestUrl = $"{_baseUrl}activities/?_limit=100&_fields=against_id,against_type,task,date_created,date_modified,billable" +
                $"&_filters=activity_class(),task(0),against_id({againstId}),against_type({againstType}),date_logged_after({date_modified_after}),date_logged_before({date_modified_before}),billable_greater_than(0)";

            _logger.LogInformation("Accelo API Request: {RequestUrl}", requestUrl);
            var response = await client.GetAsync(requestUrl);

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<Activity>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<Activity>>(jsonResponse);
            return acceloResponse.Response;
        }

        /// <summary>
        /// Fetches activities related to individual tasks (one call per task).
        /// </summary>
        private async Task<List<Activity>> GetTaskFromAcceloAsync(string accessToken, string taskId, long date_modified_after, long date_modified_before)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var requestUrl = $"{_baseUrl}activities/?_limit=100&_fields=_ALL" +
                $"&_filters=activity_class(),task({taskId}),date_logged_after({date_modified_after}),date_logged_before({date_modified_before}),billable_greater_than(0)";

            _logger.LogInformation("Accelo API Request: {RequestUrl}", requestUrl);
            var response = await client.GetAsync(requestUrl);

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<Activity>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<Activity>>(jsonResponse);
            return acceloResponse.Response;
        }
    }
}
