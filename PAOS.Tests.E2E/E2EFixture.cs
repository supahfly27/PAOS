using Amazon.S3;
using Amazon.S3.Model;
using Npgsql;
using StackExchange.Redis;

namespace PAOS.Tests.E2E;

public class E2EFixture : IAsyncLifetime
{
    public const string ApiBase = "http://localhost:8000";
    public const string PostgresConnString = "Host=localhost;Port=5432;Database=agentic_os;Username=agent;Password=agent_password";
    public const string RedisConnString = "localhost:6379";
    public const string MinioServiceUrl = "http://localhost:9000";
    public const string MinioAccessKey = "minio";
    public const string MinioSecretKey = "minio_password";
    public const string MinioBucket = "paos-e2e-test";

    public HttpClient Http { get; } = new() { BaseAddress = new Uri(ApiBase) };
    public NpgsqlDataSource Db { get; } = NpgsqlDataSource.Create(PostgresConnString);
    public IDatabase Redis { get; private set; } = null!;
    public AmazonS3Client S3 { get; } = new(
        MinioAccessKey,
        MinioSecretKey,
        new AmazonS3Config { ServiceURL = MinioServiceUrl, ForcePathStyle = true });

    public async Task InitializeAsync()
    {
        var mux = await ConnectionMultiplexer.ConnectAsync(RedisConnString);
        Redis = mux.GetDatabase();

        try { await S3.PutBucketAsync(MinioBucket); }
        catch { /* already exists */ }
    }

    public Task DisposeAsync()
    {
        Http.Dispose();
        Db.Dispose();
        return Task.CompletedTask;
    }
}
