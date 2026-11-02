namespace CleanLambdaFunction.Presentation.Tests.Integration;

using Amazon.Lambda.TestUtilities;
using CleanLambdaFunction.Presentation.LambdaFunction;
using CleanLambdaFunction.Presentation.LambdaFunction.Models;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

public class HandlerTests
{
    // Every test class gets its own in-memory database, so tests never share or keep data.
    private readonly Handler handler = new($"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared", seedSampleData: true);
    private readonly TestLambdaContext context = new() { RemainingTime = TimeSpan.FromSeconds(30) };

    [Fact]
    public async Task GetMovies_ShouldReturn_FirstPage()
    {
        // Act
        var result = await this.handler.GetMovies(new ListRequest(), this.context);

        // Assert
        result.Items.Count.ShouldBe(10);
        result.TotalCount.ShouldBe(50);
        result.NextOffset.ShouldBe(10);
        result.Items.ShouldAllBe(m => m.Id != Guid.Empty && !string.IsNullOrWhiteSpace(m.Title));
        result.Items.ShouldAllBe(m => m.ReviewCount == 0 ? m.AverageStars == null : m.AverageStars != null);
    }

    [Fact]
    public async Task GetMovies_ShouldReturn_NextPage()
    {
        // Arrange
        var first = await this.handler.GetMovies(new ListRequest { Limit = 20 }, this.context);

        // Act
        var result = await this.handler.GetMovies(new ListRequest { Offset = first.NextOffset.ShouldNotBeNull(), Limit = 40 }, this.context);

        // Assert
        result.Items.Count.ShouldBe(30);
        result.TotalCount.ShouldBe(50);
        result.NextOffset.ShouldBeNull();
        result.Items.Select(m => m.Id).Intersect(first.Items.Select(m => m.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMovies_ShouldReturn_Matches()
    {
        // Arrange
        var title = (await this.handler.GetMovies(new ListRequest(), this.context)).Items[0].Title;

        // Act
        var result = await this.handler.GetMovies(new ListRequest { Query = title.ToUpperInvariant() }, this.context);

        // Assert
        result.Items.ShouldNotBeEmpty();
        result.Items.ShouldAllBe(m => m.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handler_ShouldRead_SettingsFromEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ConnectionStrings__MovieReviews", $"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        Environment.SetEnvironmentVariable("Database__SeedSampleData", "true");

        try
        {
            // Act
            var result = await new Handler().GetMovies(new ListRequest(), this.context);

            // Assert
            result.TotalCount.ShouldBe(50);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__MovieReviews", null);
            Environment.SetEnvironmentVariable("Database__SeedSampleData", null);
        }
    }

    [Fact]
    public async Task Handler_ShouldRead_SettingsFromConfiguration()
    {
        // Arrange
        var configuration = Configuration(new()
        {
            ["ConnectionStrings:MovieReviews"] = $"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared",
            ["Database:SeedSampleData"] = "true"
        });

        // Act
        var result = await new Handler(configuration).GetMovies(new ListRequest(), this.context);

        // Assert
        result.TotalCount.ShouldBe(50);
    }

    [Fact]
    public async Task Handler_ShouldNotSeed_WhenSettingIsMissing()
    {
        // Arrange
        var configuration = Configuration(new()
        {
            ["ConnectionStrings:MovieReviews"] = $"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared"
        });

        // Act
        var result = await new Handler(configuration).GetMovies(new ListRequest(), this.context);

        // Assert
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public void Handler_ShouldThrow_WhenSeedSettingIsNotABoolean()
    {
        // Arrange
        var configuration = Configuration(new()
        {
            ["ConnectionStrings:MovieReviews"] = $"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared",
            ["Database:SeedSampleData"] = "yes"
        });

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => new Handler(configuration));

        // Assert
        exception.Message.ShouldContain("Database:SeedSampleData");
    }

    [Fact]
    public void ConnectionStringFrom_ShouldReturn_DefaultWhenUnset()
    {
        // Act
        var connectionString = Handler.ConnectionStringFrom(Configuration([]));

        // Assert
        connectionString.ShouldBe(Handler.DefaultConnectionString);
    }

    [Fact]
    public void ConnectionStringFrom_ShouldThrow_WhenConfigurationIsNull()
    {
        // Act
        var exception = Should.Throw<ArgumentNullException>(() => Handler.ConnectionStringFrom(null!));

        // Assert
        exception.ParamName.ShouldBe("configuration");
    }

    [Fact]
    public async Task Handler_ShouldNotSeed_WhenSampleDataIsOff()
    {
        // Arrange
        var empty = new Handler($"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared", seedSampleData: false);

        // Act
        var result = await empty.GetMovies(new ListRequest(), this.context);

        // Assert
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMovies_ShouldRun_WithoutRemainingTime()
    {
        // Act
        var result = await this.handler.GetMovies(new ListRequest(), new TestLambdaContext());

        // Assert
        result.Items.Count.ShouldBe(10);
    }

    [Fact]
    public async Task GetMovies_ShouldThrow_WhenLimitIsNotValid()
    {
        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() => this.handler.GetMovies(new ListRequest { Limit = 0 }, this.context));

        // Assert
        exception.Errors.ShouldContain(e => e.PropertyName == "Limit" && e.ErrorMessage == "A limit must be between 1 and 50.");
    }

    [Fact]
    public async Task GetMovies_ShouldThrow_WhenOffsetIsNegative()
    {
        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() => this.handler.GetMovies(new ListRequest { Offset = -1 }, this.context));

        // Assert
        exception.Errors.ShouldContain(e => e.PropertyName == "Offset");
    }

    [Fact]
    public async Task GetAuthors_ShouldReturn_FirstPage()
    {
        // Act
        var result = await this.handler.GetAuthors(new ListRequest { Limit = 5 }, this.context);

        // Assert
        result.Items.Count.ShouldBe(5);
        result.TotalCount.ShouldBe(15);
        result.NextOffset.ShouldBe(5);
        result.Items.ShouldAllBe(a => !string.IsNullOrEmpty(a.FirstName) && !string.IsNullOrEmpty(a.LastName));
    }

    [Fact]
    public async Task GetAuthors_ShouldReturn_Matches()
    {
        // Arrange
        var lastName = (await this.handler.GetAuthors(new ListRequest(), this.context)).Items[0].LastName;

        // Act
        var result = await this.handler.GetAuthors(new ListRequest { Query = lastName.ToLowerInvariant() }, this.context);

        // Assert
        result.Items.ShouldNotBeEmpty();
        result.Items.ShouldAllBe(a => $"{a.FirstName} {a.LastName}".Contains(lastName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetReviews_ShouldReturn_FirstPage()
    {
        // Act
        var result = await this.handler.GetReviews(new ListReviewsRequest(), this.context);

        // Assert
        result.Items.Count.ShouldBe(10);
        result.TotalCount.ShouldBe(150);
        result.NextOffset.ShouldBe(10);
        result.Items.ShouldAllBe(r => r.Stars >= 1 && r.Stars <= 5 && r.ReviewedMovie != null);
    }

    [Fact]
    public async Task GetReviews_ShouldReturn_ReviewsOfMovie()
    {
        // Arrange
        var movieId = (await this.handler.GetReviews(new ListReviewsRequest(), this.context)).Items[0].ReviewedMovie.ShouldNotBeNull().Id;

        // Act
        var result = await this.handler.GetReviews(new ListReviewsRequest { MovieId = movieId, Limit = 50 }, this.context);

        // Assert
        result.Items.ShouldNotBeEmpty();
        result.Items.ShouldAllBe(r => r.ReviewedMovie!.Id == movieId);
    }

    [Fact]
    public async Task GetReviews_ShouldReturn_ReviewsByAuthor()
    {
        // Arrange
        var author = (await this.handler.GetAuthors(new ListRequest { Limit = 50 }, this.context)).Items.First(a => a.ReviewCount > 0);

        // Act
        var result = await this.handler.GetReviews(new ListReviewsRequest { AuthorId = author.Id, Limit = 50 }, this.context);

        // Assert
        result.TotalCount.ShouldBe(author.ReviewCount);
        result.Items.Average(r => r.Stars).ShouldBe(author.AverageStars.ShouldNotBeNull(), 0.05);
    }

    [Fact]
    public async Task GetReviews_ShouldThrow_WhenMovieIdIsEmpty()
    {
        // Act
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            this.handler.GetReviews(new ListReviewsRequest { MovieId = Guid.Empty }, this.context));

        // Assert
        exception.Errors.ShouldContain(e => e.PropertyName == "MovieId");
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
