using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.MemoryAPI.Endpoints;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();                   // /openapi/v1.json
app.MapScalarApiReference();        // /scalar/v1
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/openapi/v1.json", "PAOS Memory API v1");
    c.RoutePrefix = "swagger";      // /swagger
});

app.MapHealthChecks("/health");
app.MapHealthEndpoints();
app.MapSourceEndpoints();
app.MapIdentityEndpoints();
app.MapPeopleEndpoints();
app.MapProjectEndpoints();
app.MapCommitmentEndpoints();
app.MapSearchEndpoints();
app.MapEpisodicEndpoints();
app.MapSemanticEndpoints();
app.MapProceduralEndpoints();
app.MapDecisionEndpoints();

app.Run();

public partial class Program { }
