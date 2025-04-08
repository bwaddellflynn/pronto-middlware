using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Pronto.Middleware.Models.ProjectManagementModels;

namespace Pronto.Middleware.Controllers.ProjectManagement
{
    [ApiController]
    [Route("projectmanagement/projects")]
    public class ProjectsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProjectsController> _logger;
        private readonly string _baseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public ProjectsController(IHttpClientFactory httpClientFactory, ILogger<ProjectsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetProjects(
            [FromQuery] int limit = 100,
            [FromQuery(Name = "fields")] string? fields = null,
            [FromQuery(Name = "filters")] string? filters = null)
        {
            try
            {
                if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
                {
                    return Unauthorized("Authorization header is missing.");
                }

                if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
                {
                    return Unauthorized("Invalid Authorization header.");
                }

                fields ??= "_ALL,type(),breadcrumbs";
                filters ??= "job_status(14,15,16,17,18,19,22)";

                var accessToken = headerValue.Parameter;
                var projects = await GetProjectsFromAcceloAsync(accessToken, limit, fields, filters);
                return Ok(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching projects.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private async Task<List<Project>> GetProjectsFromAcceloAsync(string accessToken, int limit, string fields, string filters)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var query = $"{_baseUrl}jobs?_limit={limit}&_fields={fields}&_filters={filters}";

            var response = await client.GetAsync(query);

            _logger.LogInformation("Accelo API response status: {StatusCode}", response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Accelo API raw response: {Response}", jsonResponse);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Accelo API request failed: {StatusCode} - {Response}", response.StatusCode, jsonResponse);
                return new List<Project>();
            }

            var acceloResponse = JsonConvert.DeserializeObject<AcceloApiResponse<ProjectsResponse>>(jsonResponse);

            var projects = acceloResponse?.Response?.Select(r => new Project
            {
                Id = int.Parse(r.Id),
                Title = r.Title,
                JobType = r.JobType,
                TypeTitle = r.Type?.Title,
                CompanyName = r.Breadcrumbs?.FirstOrDefault(b => b.Table == "company")?.Title,
                Status = r.Status,
                Standing = r.Standing,
                Comments = r.Comments,
                DateCommenced = long.TryParse(r.DateCommenced, out var dc) ? dc : (long?)null,
                DateModified = long.TryParse(r.DateModified, out var dm) ? dm : (long?)null,
                DateCreated = long.TryParse(r.DateCreated, out var dcrt) ? dcrt : (long?)null,
                DateStarted = long.TryParse(r.DateStarted, out var ds) ? ds : (long?)null,
                DateDue = long.TryParse(r.DateDue, out var dd) ? dd : (long?)null
            }).ToList() ?? new List<Project>();

            return projects;
        }

        private class AcceloApiResponse<T>
        {
            [JsonProperty("response")]
            public List<T> Response { get; set; }
        }
    }
}
