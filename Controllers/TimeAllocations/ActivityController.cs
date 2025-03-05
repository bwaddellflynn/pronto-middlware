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
        public async Task<IActionResult> GetTasks(string againstType, int againstId)
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
                var task = await GetTaskFromAcceloAsync(accessToken, againstId);
                return Ok(task);
            }
            else
            {
                var tasks = await GetTasksFromAcceloAsync(accessToken, againstType, againstId);
                return Ok(tasks);
            }
        }

        private async Task<List<Activity>> GetTasksFromAcceloAsync(string accessToken, string againstType, int againstId)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}activities?_filters=against_id({againstId}),against_type({againstType})&_fields=_ALL");

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

        private async Task<List<Activity>> GetTaskFromAcceloAsync(string accessToken, int taskId)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}activities/?_fields=_ALL&_filters=task({taskId})");

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
