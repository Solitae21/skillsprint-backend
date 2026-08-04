using SkillSprint.Infrastructure;

using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

var app = builder.Build();

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
