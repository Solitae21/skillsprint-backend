namespace SkillSprint.Infrastructure;

using MongoDB.Driver;

using Microsoft.Extensions.Options;

public class MongoContext
{
    public IMongoDatabase Database { get; }

    public MongoContext(IMongoClient client, IOptions<MongoOptions> options)
    {
        Database = client.GetDatabase(options.Value.Database);
    }
}