using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.MemoryWorker.Workers;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddHostedService<EmbeddingWorker>();

var host = builder.Build();
host.Run();
