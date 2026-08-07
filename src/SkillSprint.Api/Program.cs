using SkillSprint.Infrastructure;

using MongoDB.Driver;

using Microsoft.Extensions.Options;

MongoConventions.Register();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddHostedService<MongoIndexInitializer>();


builder.Services
    .AddOptions<MongoOptions>()
    .Bind(builder.Configuration.GetSection("Mongo"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
.AddOptions<JwtOptions>()
.Bind(builder.Configuration.GetSection("Jwt"))
.ValidateDataAnnotations()
.ValidateOnStart();

builder.Services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

var app = builder.Build();

app.MapGet("/temp-mongo-check", (MongoContext ctx) => ctx.Database.ListCollectionNames().ToList());
app.MapHealthChecks("/health");

var jwt = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
app.Logger.LogInformation("Jwt:AccessSecret length = {Len}", jwt.AccessSecret.Length);


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
