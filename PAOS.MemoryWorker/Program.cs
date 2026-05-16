using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.MemoryWorker.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddHostedService<EmbeddingWorker>();

var host = builder.Build();
host.Run();
