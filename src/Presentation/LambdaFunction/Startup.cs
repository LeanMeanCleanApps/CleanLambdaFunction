namespace CleanLambdaFunction.Presentation.LambdaFunction;

using System.Reflection;
using Amazon.Lambda.Annotations;
using Application;
using Application.Common.Telemetry;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Telemetry;

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
    /// Traces, metrics and logs also leave over OTLP when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
    /// </summary>
    /// <returns>The host builder the generated classes build their services from.</returns>
    public HostApplicationBuilder ConfigureHostBuilder()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        _ = builder.Configuration
            .AddEnvironmentVariables()
            .AddInMemoryCollection(settings);

        #region Telemetry

        var assembly = typeof(Startup).Assembly;
        var telemetry = TelemetrySettings.FromConfiguration(builder.Configuration, builder.Environment.EnvironmentName, assembly);

        // The generated classes build two service providers from these services, one for the functions and one for
        // the host. The providers are built here, once, so both share them and nothing is exported twice.
        var tracing = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(builder.Configuration))
            .ConfigureResource(resource => resource.AddAttributes(telemetry.ResourceAttributes))
            .AddSource(ApplicationTelemetry.Name)
            // Telemetry goes over OTLP, not X-Ray, so a "not sampled" X-Ray header from Lambda mustn't switch tracing off.
            .AddAWSLambdaConfigurations(options => options.DisableAwsXRayContextExtraction = true);
        var metrics = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(builder.Configuration))
            .ConfigureResource(resource => resource.AddAttributes(telemetry.ResourceAttributes))
            .AddMeter("Microsoft.EntityFrameworkCore")
            .AddRuntimeInstrumentation()
            .SetExemplarFilter(ExemplarFilterType.TraceBased);

        // The exporters read OTEL_EXPORTER_OTLP_ENDPOINT and the other OTEL_* settings themselves.
        if (telemetry.Enabled)
        {
            _ = tracing.AddOtlpExporter();
            _ = metrics.AddOtlpExporter();
        }

        _ = builder.Services.AddSingleton(tracing.Build());
        _ = builder.Services.AddSingleton(metrics.Build());

        #endregion Telemetry

        #region Logging

        _ = builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();

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
                .ReadFrom.Configuration(configuration);

            // Logs leave over OTLP too, carrying the trace and span IDs of whatever was running when they were written.
            if (telemetry.Enabled)
            {
                _ = loggerConfiguration.WriteTo.OpenTelemetry(
                    options =>
                    {
                        options.ResourceAttributes = telemetry.ResourceAttributes;
                        options.OnBeginSuppressInstrumentation = SuppressInstrumentationScope.Begin;
                    },
                    key => configuration[key]);
            }
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
