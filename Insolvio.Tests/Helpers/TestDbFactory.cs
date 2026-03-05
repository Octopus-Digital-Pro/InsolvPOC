using Microsoft.EntityFrameworkCore;
using Insolvio.Data;

namespace Insolvio.Tests.Helpers;

/// <summary>Spins up an in-memory ApplicationDbContext for unit tests.</summary>
public static class TestDbFactory
{
  public static ApplicationDbContext Create(string? dbName = null)
  {
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
  .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
        .Options;

    var db = new ApplicationDbContext(options);
    db.Database.EnsureCreated();
    return db;
  }
}
