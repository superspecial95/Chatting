using ChatServer.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChatServer.Tests;

public sealed class MiniDiscordApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"MiniDiscordApiTests-{Guid.NewGuid():N}";

    // Program validates configuration before WebApplicationFactory applies its
    // ConfigureAppConfiguration callback. These process-local test values are
    // read during that early phase; they never change the user's User Secrets.
    static MiniDiscordApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=unused;Database=unused;");
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-key-32-characters-minimum");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "MiniDiscord");
        Environment.SetEnvironmentVariable("Jwt__Audience", "MiniDiscordClient");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;",
                ["Jwt:Key"] = "integration-test-key-32-characters-minimum",
                ["Jwt:Issuer"] = "MiniDiscord",
                ["Jwt:Audience"] = "MiniDiscordClient"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
