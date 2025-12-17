using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SustainabilityCanvas.Api.Data;

public class SustainabilityCanvasContextFactory : IDesignTimeDbContextFactory<SustainabilityCanvasContext>
{
    public SustainabilityCanvasContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SustainabilityCanvasContext>();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? configuration["Values:ConnectionStrings:DefaultConnection"]
            ?? throw new InvalidOperationException("Database connection string 'DefaultConnection' is required. Please configure it in local.settings.json or Azure Connection Strings.");

        optionsBuilder.UseNpgsql(connectionString);

        return new SustainabilityCanvasContext(optionsBuilder.Options);
    }
}