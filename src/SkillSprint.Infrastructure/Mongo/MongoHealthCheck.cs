namespace SkillSprint.Infrastructure;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using MongoDB.Bson;

public class MongoHealthCheck : IHealthCheck
{
    private readonly MongoContext _mongoContext;

    public MongoHealthCheck(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            await _mongoContext.Database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cts.Token
            );

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {

            return HealthCheckResult.Unhealthy("Mongo ping failed", ex);
        }
    }
}