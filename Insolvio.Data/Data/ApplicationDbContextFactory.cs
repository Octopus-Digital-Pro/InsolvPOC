using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Insolvio.Data;

/// <summary>
/// Used only by EF Core design-time tools (dotnet ef migrations / efbundle).
/// The connection string here is never used at runtime — it just needs to be
/// a valid SQL Server connection string so the tooling can scaffold the context.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=localhost\\SQLEXPRESS;Database=InsolvioDb_Design;Trusted_Connection=True;TrustServerCertificate=True;",
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options);
    }
}
