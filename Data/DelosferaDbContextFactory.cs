using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace delosfera_server.Data;

public class DelosferaDbContextFactory : IDesignTimeDbContextFactory<DelosferaDbContext>
{
    public DelosferaDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<DelosferaDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        optionsBuilder.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new DelosferaDbContext(optionsBuilder.Options);
    }
}