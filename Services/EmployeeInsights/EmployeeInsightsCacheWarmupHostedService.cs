using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Pronto.Middleware.Services.EmployeeInsights
{
    public class EmployeeInsightsCacheWarmupHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmployeeInsightsCacheWarmupHostedService> _logger;
        private readonly EmployeeInsightsCacheOptions _options;

        public EmployeeInsightsCacheWarmupHostedService(
            IServiceProvider serviceProvider,
            ILogger<EmployeeInsightsCacheWarmupHostedService> logger,
            IOptions<EmployeeInsightsCacheOptions> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.WarmupEnabled)
            {
                _logger.LogInformation("Employee Insights cache warm-up hosted service disabled.");
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                using var scope = _serviceProvider.CreateScope();
                var summaryService = scope.ServiceProvider.GetRequiredService<IEmployeeInsightsActivitySummaryService>();
                if (!string.IsNullOrWhiteSpace(_options.WarmupAccessToken))
                {
                    await summaryService.WarmCurrentYearAsync(_options.WarmupAccessToken, stoppingToken);
                }
                else
                {
                    _logger.LogInformation("Employee Insights cache warm-up hosted service has no configured token. Warm-up will start after the first authenticated summary request.");
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(5, _options.CurrentWeekRefreshMinutes)), stoppingToken);
                    var token = string.IsNullOrWhiteSpace(_options.WarmupAccessToken)
                        ? summaryService.GetLatestWarmupToken()
                        : _options.WarmupAccessToken;

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        _logger.LogInformation("Employee Insights recent-week refresh skipped because no warm-up token is available yet.");
                        continue;
                    }

                    await summaryService.RefreshRecentWeeksAsync(token, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Employee Insights cache warm-up hosted service stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Employee Insights cache warm-up hosted service failed.");
            }
        }
    }
}
