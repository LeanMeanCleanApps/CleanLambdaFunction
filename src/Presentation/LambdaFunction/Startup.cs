namespace CleanLambdaFunction.Presentation.LambdaFunction;

using System.Reflection;
using Amazon.Lambda.Annotations;
using Application;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

/// <summary>
/// Registers the services every function uses. The classes Lambda Annotations generates call it once per
/// execution environment, before the first invocation.
/// </summary>
/// <param name="settings">
/// Settings that override the environment variables, such as <c>ConnectionStrings:MovieReviews</c> and
/// <c>Database:SeedSampleData</c>. Tests use them; Lambda doesn't pass any.
/// </param>
[LambdaStartup]
public class Startup(IEnumerable<KeyValuePair<string, string?>> settings)
{
    /// <summary>
    /// Lambda can only write to /tmp, so the SQLite database lives there unless the settings say otherwise.
    /// </summary>
    public const string DefaultConnectionString = "Data Source=/tmp/movie-reviews.db";

    /// <summary>
    /// Creates the startup Lambda uses, with settings from environment variables such as
    /// <c>ConnectionStrings__MovieReviews</c> and <c>Database__SeedSampleData</c>.
    /// </summary>
    public Startup()
        : this([])
    {
    }

    /// <summary>
    /// The name every log event carries, so CloudWatch Logs Insights can tell this function's events apart.
    /// </summary>
    public const string ApplicationName = "CleanLambdaFunction";

    /// <summary>
    /// Creates the host builder, with the settings and every service registered. Logs go to the console as compact
    /// JSON, which CloudWatch Logs keeps. Settings such as <c>Serilog__MinimumLevel__Default</c> change the levels.
    /// </summary>
    /// <returns>The host builder the generated classes build their services from.</returns>
    public HostApplicationBuilder ConfigureHostBuilder()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        _ = builder.Configuration
            .AddEnvironmentVariables()
            .AddInMemoryCollection(settings);

        #region Logging

        _ = builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            var assembly = typeof(Startup).Assembly;

            _ = loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Error)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationName", ApplicationName)
                .Enrich.WithProperty(
                    "AssemblyVersion",
                    assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version)
                .Enrich.WithProperty(
                    "AssemblyInformationalVersion",
                    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .ReadFrom.Configuration(services.GetRequiredService<IConfiguration>());
        });

        #endregion Logging

        #region Services

        _ = builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        _ = builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("MovieReviews") ?? DefaultConnectionString);
        _ = builder.Services.AddApplication();

        #endregion Services

        return builder;
    }
}
