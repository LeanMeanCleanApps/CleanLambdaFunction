namespace CleanLambdaFunction.Presentation.LambdaFunction;

using Amazon.Lambda.Annotations;
using Application;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    /// Creates the host builder, with the settings and every service registered.
    /// </summary>
    /// <returns>The host builder the generated classes build their services from.</returns>
    public HostApplicationBuilder ConfigureHostBuilder()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        _ = builder.Configuration
            .AddEnvironmentVariables()
            .AddInMemoryCollection(settings);

        _ = builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        _ = builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("MovieReviews") ?? DefaultConnectionString);
        _ = builder.Services.AddApplication();

        return builder;
    }
}
