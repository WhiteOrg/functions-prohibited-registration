using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<string>(sp =>
        {
            var connectionString = context.Configuration.GetConnectionString("NEON_DB_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("NEON_DB_CONNECTION_STRING not found in configuration");
            return connectionString;
        });
    })
    .Build();

host.Run();