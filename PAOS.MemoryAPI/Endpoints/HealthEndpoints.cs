namespace PAOS.MemoryAPI.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new { service = "PAOS Memory API", status = "running" }));
    }
}
