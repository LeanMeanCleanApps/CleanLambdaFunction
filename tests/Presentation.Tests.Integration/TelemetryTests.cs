namespace CleanLambdaFunction.Presentation.Tests.Integration;

using System.Diagnostics;
using Amazon.Lambda.TestUtilities;
using Application.Common.Telemetry;
using CleanLambdaFunction.Presentation.LambdaFunction;
using CleanLambdaFunction.Presentation.LambdaFunction.Models;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using Xunit;

public sealed class TelemetryTests : IDisposable
{
    private const string TestSourceName = "CleanLambdaFunction.Presentation.Tests";
    private const string LambdaSourceName = "OpenTelemetry.Instrumentation.AWSLambda";

    private static readonly ActivitySource TestSource = new(TestSourceName);

    private readonly ActivityListener listener;
    private readonly List<Activity> spans = [];
    private readonly TestLambdaContext context = new()
    {
        FunctionName = "GetMovies",
        InvokedFunctionArn = "arn:aws:lambda:us-east-1:123456789012:function:GetMovies",
        AwsRequestId = Guid.NewGuid().ToString(),
        RemainingTime = TimeSpan.FromSeconds(30)
    };

    public TelemetryTests()
    {
        // Other tests invoke functions at the same time, so only spans from this test's own trace are kept.
        this.listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is TestSourceName or LambdaSourceName or ApplicationTelemetry.Name,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (this.spans)
                {
                    this.spans.Add(activity);
                }
            }
        };

        ActivitySource.AddActivityListener(this.listener);
    }

    [Fact]
    public async Task Invocation_ShouldTrace_FromFunctionThroughUseCase()
    {
        // Arrange
        var handler = CreateHandler([]);

        using var parent = TestSource.StartActivity("Test")!;

        // Act
        _ = await handler.GetMovies(new ListRequest { Limit = 5 }, this.context);

        // Assert
        var trace = this.SpansIn(parent);
        var function = trace.Single(s => s.Source.Name == LambdaSourceName);
        var useCase = trace.Single(s => s.Source.Name == ApplicationTelemetry.Name);

        function.Kind.ShouldBe(ActivityKind.Server);
        function.GetTagItem("faas.name").ShouldBe("GetMovies");
        useCase.DisplayName.ShouldBe("GetMoviesQuery");
        useCase.ParentSpanId.ShouldBe(function.SpanId);
        useCase.GetTagItem(ApplicationTelemetry.RequestOutcomeTag).ShouldBe("handled");
    }

    [Fact]
    public async Task HttpInvocation_ShouldTrace_OneSpanPerInvocation()
    {
        // Arrange
        var handler = CreateHandler([]);

        using var parent = TestSource.StartActivity("Test")!;

        // Act
        _ = await handler.HttpGetReviews("not-a-guid", string.Empty, null, null, this.context);

        // Assert
        var trace = this.SpansIn(parent);
        trace.Count(s => s.Source.Name == LambdaSourceName).ShouldBe(1);
        trace.ShouldNotContain(s => s.Source.Name == ApplicationTelemetry.Name);
    }

    [Fact]
    public async Task Startup_ShouldRegister_TracerAndMeterProviders_WhenExporting()
    {
        // Arrange
        var handler = CreateHandler(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
            ["OTEL_SERVICE_NAME"] = "TelemetryTests"
        }, out var services);

        // Act
        var result = await handler.GetMovies(new ListRequest { Limit = 5 }, this.context);

        // Assert
        result.Items.Count.ShouldBe(5);
        _ = services.GetRequiredService<TracerProvider>();
        _ = services.GetRequiredService<MeterProvider>();
    }

    public void Dispose()
    {
        this.listener.Dispose();
    }

    private static Handler CreateHandler(Dictionary<string, string?> settings)
    {
        return CreateHandler(settings, out _);
    }

    private static Handler CreateHandler(Dictionary<string, string?> settings, out IServiceProvider services)
    {
        settings["ConnectionStrings:MovieReviews"] = $"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        settings["Database:SeedSampleData"] = "true";

        var builder = new Startup(settings).ConfigureHostBuilder();

        _ = builder.Services.AddSingleton<Handler>();

        services = builder.Services.BuildServiceProvider(validateScopes: true);

        return services.GetRequiredService<Handler>();
    }

    private List<Activity> SpansIn(Activity parent)
    {
        lock (this.spans)
        {
            return [.. this.spans.Where(s => s.TraceId == parent.TraceId && s.Source.Name != TestSourceName)];
        }
    }
}
