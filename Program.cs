using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SustainabilityCanvas.Api.Data;
using SustainabilityCanvas.Api.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        // Add Entity Framework with PostgreSQL
        services.AddDbContext<SustainabilityCanvasContext>(options =>
        {
            var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString);
        });
        
        // Add JWT Service
        services.AddSingleton<JwtService>();
        
        // Add Registration Code Service
        services.AddScoped<RegistrationCodeService>();
        
        // Add Master Password Service
        services.AddScoped<MasterPasswordService>();
        
        // Configure shared JSON serialization options
        services.AddSingleton<JsonSerializerOptions>(provider =>
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                WriteIndented = false // Set to true for debugging if needed
            };
        });
    })
    .Build();

host.Run();
