using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pronto.Middleware.Models.EmployeeInsights;
using Pronto.Middleware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Pronto.Middleware.Controllers.EmployeeInsights
{
    [ApiController]
    [Route("employeeinsights/staff")]
    public class EIStaffController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EIStaffController> _logger;
        private const string BaseUrl = "https://perbyte.api.accelo.com/api/v0/";

        public EIStaffController(
            IHttpClientFactory httpClientFactory,
            ILogger<EIStaffController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// GET /employeeinsights/staff
        /// Returns a list of staff members.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EIStaff>>> GetStaffAsync(
            [FromQuery] int limit = 100,
            [FromQuery(Name = "fields")] string? fields = "id,firstname,surname,email,access_level,standing",
            [FromQuery(Name = "filters")] string? filters = "standing(active)")
        {
            try
            {
                if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                    return Unauthorized("Missing Authorization header.");
                if (!AuthenticationHeaderValue.TryParse(authHeader, out var headerValue))
                    return Unauthorized("Invalid Authorization header.");

                var token = headerValue.Parameter!;
                var staffList = await FetchStaffListAsync(token, limit, fields, filters);
                return Ok(staffList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in GetStaffAsync");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET /employeeinsights/staff/{staffId}
        /// Returns a single staff member by ID.
        /// </summary>
        [HttpGet("{staffId}")]
        public async Task<ActionResult<EIStaff>> GetStaffByIdAsync(
            int staffId,
            [FromQuery(Name = "fields")] string? fields = "id,firstname,surname,email,access_level,standing")
        {
            try
            {
                if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                    return Unauthorized("Missing Authorization header.");
                if (!AuthenticationHeaderValue.TryParse(authHeader, out var headerValue))
                    return Unauthorized("Invalid Authorization header.");

                var token = headerValue.Parameter!;
                var staff = await FetchStaffByIdAsync(token, staffId, fields);
                if (staff == null)
                    return NotFound();

                return Ok(staff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in GetStaffByIdAsync for ID {StaffId}", staffId);
                return StatusCode(500, "Internal server error while fetching staff");
            }
        }

        private async Task<List<EIStaff>> FetchStaffListAsync(
            string token, int limit, string? fields, string? filters)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var url = $"{BaseUrl}staff?_limit={limit}&_fields={fields}";
            if (!string.IsNullOrWhiteSpace(filters))
                url += $"&_filters={filters}";

            _logger.LogInformation("Fetching Accelo Staff List: {Url}", url);
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Accelo Response [{Status}]: {Json}", response.StatusCode, json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error fetching staff list: {Status} {Json}", response.StatusCode, json);
                return new List<EIStaff>();
            }

            var parsed = JsonConvert.DeserializeObject<AcceloApiResponse<StaffResponse>>(json);
            var items = parsed?.Response ?? new List<StaffResponse>();

            return items.Select(r => new EIStaff
            {
                Id = int.Parse(r.Id),
                Firstname = r.Firstname,
                Surname = r.Surname,
                Email = r.Email,
                AccessLevel = r.AccessLevel
            }).ToList();
        }

        private async Task<EIStaff?> FetchStaffByIdAsync(
            string token, int staffId, string? fields)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var url = $"{BaseUrl}staff/{staffId}?_fields={fields}";
            _logger.LogInformation("Fetching Accelo Staff by ID: {Url}", url);

            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Accelo Response [{Status}]: {Json}", response.StatusCode, json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                  "Error fetching staff by ID {StaffId}: {Status} {Json}",
                  staffId, response.StatusCode, json
                );
                return null;
            }

            // Handle “response” being either an object or an array
            var jo = JObject.Parse(json);
            var tokenResp = jo["response"];
            List<StaffResponse> list;
            if (tokenResp == null)
            {
                return null;
            }
            else if (tokenResp.Type == JTokenType.Array)
            {
                list = tokenResp.ToObject<List<StaffResponse>>()!;
            }
            else
            {
                var single = tokenResp.ToObject<StaffResponse>()!;
                list = new List<StaffResponse> { single };
            }

            var r = list.FirstOrDefault();
            if (r == null) return null;

            return new EIStaff
            {
                Id = int.Parse(r.Id),
                Firstname = r.Firstname,
                Surname = r.Surname,
                Email = r.Email,
                AccessLevel = r.AccessLevel
            };
        }
    }
}
