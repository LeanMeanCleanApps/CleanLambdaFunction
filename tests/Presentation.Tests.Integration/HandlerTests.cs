namespace CleanLambdaFunction.Presentation.Tests.Integration;

using Amazon.Lambda.TestUtilities;
using CleanLambdaFunction.Presentation.LambdaFunction;
using CleanLambdaFunction.Presentation.LambdaFunction.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class HandlerTests
{
    // Every test class gets its own in-memory database, so tests never share or keep data.
    private readonly Handler handler = CreateHandler(new()
    {
        ["ConnectionStrings:MovieReviews"] = InMemoryDatabase(),
        ["Database:SeedSampleData"] = "true"
    });
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
    public async Task GeneratedHandlers_ShouldRead_SettingsFromEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ConnectionStrings__MovieReviews", InMemoryDatabase());
        Environment.SetEnvironmentVariable("Database__SeedSampleData", "true");

        try
        {
            // Act
            var movies = await new Handler_GetMovies_Generated().GetMovies(new ListRequest(), this.context);
            var authors = await new Handler_GetAuthors_Generated().GetAuthors(new ListRequest(), this.context);
            var reviews = await new Handler_GetReviews_Generated().GetReviews(new ListReviewsRequest(), this.context);

            // Assert
            movies.TotalCount.ShouldBe(50);
            authors.TotalCount.ShouldBe(15);
            reviews.TotalCount.ShouldBe(150);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__MovieReviews", null);
            Environment.SetEnvironmentVariable("Database__SeedSampleData", null);
        }
    }

    [Fact]
    public async Task Handler_ShouldNotSeed_WhenSettingIsMissing()
    {
        // Arrange
        var empty = CreateHandler(new() { ["ConnectionStrings:MovieReviews"] = InMemoryDatabase() });

        // Act
        var result = await empty.GetMovies(new ListRequest(), this.context);

        // Assert
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Handler_ShouldThrow_WhenSeedSettingIsNotABoolean()
    {
        // Act
        var exception = Should.Throw<InvalidOperationException>(() => CreateHandler(new()
        {
            ["ConnectionStrings:MovieReviews"] = InMemoryDatabase(),
            ["Database:SeedSampleData"] = "yes"
        }));

        // Assert
        exception.Message.ShouldContain("Database:SeedSampleData");
    }

    [Fact]
    public void Handler_ShouldThrow_WhenServicesAreNull()
    {
        // Act
        var exception = Should.Throw<ArgumentNullException>(() => new Handler(null!, new ConfigurationBuilder().Build()));

        // Assert
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Handler_ShouldThrow_WhenConfigurationIsNull()
    {
        // Act
        var exception = Should.Throw<ArgumentNullException>(() => new Handler(new ServiceCollection().BuildServiceProvider(), null!));

        // Assert
        exception.ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void Startup_ShouldUse_DefaultDatabase_WhenUnset()
    {
        // Arrange
        using var provider = new Startup().ConfigureHostBuilder().Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions>();
        var connectionString = options.Extensions.OfType<RelationalOptionsExtension>().Single().ConnectionString;

        // Assert
        connectionString.ShouldBe(Startup.DefaultConnectionString);
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
        result.Items.ShouldAllBe(r => r.ReviewAuthor != null && !string.IsNullOrEmpty(r.ReviewAuthor.FirstName) && !string.IsNullOrEmpty(r.ReviewAuthor.LastName));
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
        result.Items.ShouldAllBe(r => r.ReviewAuthor!.Id == author.Id);
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

    private static Handler CreateHandler(Dictionary<string, string?> settings)
    {
        // The same services the generated classes build, with scope validation on to catch a scoped service in a singleton.
        var builder = new Startup(settings).ConfigureHostBuilder();

        _ = builder.Services.AddSingleton<Handler>();

        return builder.Services.BuildServiceProvider(validateScopes: true).GetRequiredService<Handler>();
    }

    private static string InMemoryDatabase()
    {
        return $"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
    }
}
