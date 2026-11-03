namespace CleanLambdaFunction.Presentation.Tests.Integration;

using System.Net;
using System.Text.Json;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using CleanLambdaFunction.Presentation.LambdaFunction;
using CleanLambdaFunction.Presentation.LambdaFunction.Models;
using CleanLambdaFunction.Presentation.LambdaFunction.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class HttpApiTests
{
    private static readonly LambdaFunctionJsonSerializer Serializer = new();

    // Every test class gets its own in-memory database, so tests never share or keep data.
    private readonly Handler handler = CreateHandler();
    private readonly TestLambdaContext context = new() { RemainingTime = TimeSpan.FromSeconds(30) };

    [Fact]
    public async Task HttpGetMovies_ShouldReturn_Ok()
    {
        // Act
        var response = Read(await this.handler.HttpGetMovies(string.Empty, null, 5, this.context));

        // Assert
        response.StatusCode.ShouldBe((int)HttpStatusCode.OK);

        var page = Body<Page<Movie>>(response);
        page.Items.Count.ShouldBe(5);
        page.TotalCount.ShouldBe(50);
        page.NextOffset.ShouldBe(5);
    }

    [Fact]
    public async Task HttpGetMovies_ShouldReturn_Matches()
    {
        // Arrange
        var title = Body<Page<Movie>>(Read(await this.handler.HttpGetMovies(string.Empty, null, null, this.context))).Items[0].Title;

        // Act
        var response = Read(await this.handler.HttpGetMovies(title.ToUpperInvariant(), null, null, this.context));

        // Assert
        Body<Page<Movie>>(response).Items.ShouldAllBe(m => m.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HttpGetMovies_ShouldReturn_BadRequest_WhenLimitIsNotValid()
    {
        // Act
        var result = await this.handler.HttpGetMovies(string.Empty, 0, 0, this.context);
        var response = Read(result);

        // Assert
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        ContentType(result).ShouldBe("application/problem+json");

        var problem = Body<Problem>(response);
        problem.Status.ShouldBe(400);
        problem.Title.ShouldBe("One or more validation errors occurred.");
        problem.Errors.ShouldNotBeNull()["Limit"].ShouldBe(["A limit must be between 1 and 50."]);
    }

    [Fact]
    public async Task HttpGetAuthors_ShouldReturn_Ok()
    {
        // Act
        var response = Read(await this.handler.HttpGetAuthors(string.Empty, 10, null, this.context));

        // Assert
        response.StatusCode.ShouldBe((int)HttpStatusCode.OK);

        var page = Body<Page<Author>>(response);
        page.Items.Count.ShouldBe(5);
        page.TotalCount.ShouldBe(15);
        page.NextOffset.ShouldBeNull();
    }

    [Fact]
    public async Task HttpGetReviews_ShouldReturn_ReviewsOfMovie()
    {
        // Arrange
        var movieId = (await this.handler.GetReviews(new ListReviewsRequest(), this.context)).Items[0].ReviewedMovie.ShouldNotBeNull().Id;

        // Act
        var response = Read(await this.handler.HttpGetReviews(movieId.ToString(), string.Empty, null, 50, this.context));

        // Assert
        response.StatusCode.ShouldBe((int)HttpStatusCode.OK);

        var page = Body<Page<Review>>(response);
        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(r => r.ReviewedMovie!.Id == movieId);
    }

    [Fact]
    public async Task HttpGetReviews_ShouldReturn_BadRequest_WhenIdsAreNotGuids()
    {
        // Act
        var response = Read(await this.handler.HttpGetReviews("not-a-movie", "not-an-author", null, null, this.context));

        // Assert
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);

        var errors = Body<Problem>(response).Errors.ShouldNotBeNull();
        errors["movieId"].ShouldBe(["'not-a-movie' is not a valid id."]);
        errors["authorId"].ShouldBe(["'not-an-author' is not a valid id."]);
    }

    [Fact]
    public async Task HttpGetReviews_ShouldReturn_BadRequest_WhenMovieIdIsEmpty()
    {
        // Act
        var response = Read(await this.handler.HttpGetReviews(Guid.Empty.ToString(), string.Empty, null, null, this.context));

        // Assert
        response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        Body<Problem>(response).Errors.ShouldNotBeNull().ShouldContainKey("MovieId");
    }

    [Fact]
    public async Task GeneratedHttpHandlers_ShouldBind_QueryString()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ConnectionStrings__MovieReviews", InMemoryDatabase());
        Environment.SetEnvironmentVariable("Database__SeedSampleData", "true");

        try
        {
            var request = new APIGatewayHttpApiV2ProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> { ["limit"] = "3" }
            };

            // Act
            var movies = await ReadAsync(await new Handler_HttpGetMovies_Generated().HttpGetMovies(request, this.context));
            var authors = await ReadAsync(await new Handler_HttpGetAuthors_Generated().HttpGetAuthors(request, this.context));
            var reviews = await ReadAsync(await new Handler_HttpGetReviews_Generated().HttpGetReviews(request, this.context));

            // Assert
            Body<Page<Movie>>(movies).Items.Count.ShouldBe(3);
            Body<Page<Author>>(authors).Items.Count.ShouldBe(3);
            Body<Page<Review>>(reviews).Items.Count.ShouldBe(3);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__MovieReviews", null);
            Environment.SetEnvironmentVariable("Database__SeedSampleData", null);
        }
    }

    private static APIGatewayHttpApiV2ProxyResponse Read(IHttpResult result)
    {
        using var stream = Serialize(result);

        return Serializer.Deserialize<APIGatewayHttpApiV2ProxyResponse>(stream);
    }

    /// <summary>
    /// Reads the content type from the JSON that goes back to API Gateway. Lambda Annotations writes header names
    /// in lower case, and the SDK's response type doesn't read them back, so the raw JSON is the one to check.
    /// </summary>
    private static string? ContentType(IHttpResult result)
    {
        using var stream = Serialize(result);
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.GetProperty("headers").GetProperty("content-type").GetString();
    }

    private static Stream Serialize(IHttpResult result)
    {
        return result.Serialize(new HttpResultSerializationOptions
        {
            Format = HttpResultSerializationOptions.ProtocolFormat.HttpApi,
            Version = HttpResultSerializationOptions.ProtocolVersion.V2,
            Serializer = Serializer
        });
    }

    private static async Task<APIGatewayHttpApiV2ProxyResponse> ReadAsync(Stream stream)
    {
        await using (stream)
        {
            return Serializer.Deserialize<APIGatewayHttpApiV2ProxyResponse>(stream);
        }
    }

    private static T Body<T>(APIGatewayHttpApiV2ProxyResponse response)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(response.Body));

        return Serializer.Deserialize<T>(stream);
    }

    private static Handler CreateHandler()
    {
        var builder = new Startup(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MovieReviews"] = InMemoryDatabase(),
            ["Database:SeedSampleData"] = "true"
        }).ConfigureHostBuilder();

        _ = builder.Services.AddSingleton<Handler>();

        return builder.Services.BuildServiceProvider(validateScopes: true).GetRequiredService<Handler>();
    }

    private static string InMemoryDatabase()
    {
        return $"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
    }
}
