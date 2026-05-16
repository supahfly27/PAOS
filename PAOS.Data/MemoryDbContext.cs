using Microsoft.EntityFrameworkCore;

namespace PAOS.Data;

public class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        base.OnModelCreating(modelBuilder);
    }
}
