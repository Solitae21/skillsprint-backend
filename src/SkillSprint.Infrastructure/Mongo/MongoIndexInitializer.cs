namespace SkillSprint.Infrastructure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class MongoIndexInitializer : IHostedService
{
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(ILogger<MongoIndexInitializer> logger)
    {
        _logger = logger;
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("start async log");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}