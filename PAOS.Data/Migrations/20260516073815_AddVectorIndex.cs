using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_memory_embeddings_embedding ON \"MemoryEmbeddings\" " +
                "USING ivfflat (\"Embedding\" vector_cosine_ops) WITH (lists = 100)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_memory_embeddings_embedding");
        }
    }
}
