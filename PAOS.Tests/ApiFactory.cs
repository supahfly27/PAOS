using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PAOS.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    // Static constructor runs before the factory is instantiated, ensuring env vars
    // are set before WebApplication.CreateBuilder reads configuration.
    static ApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres",
            "Host=localhost;Port=5432;Database=agentic_os;Username=agent;Password=agent_password");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
    }
}
