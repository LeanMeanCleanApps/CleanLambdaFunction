[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.SourceGeneratorLambdaJsonSerializer<CleanLambdaFunction.Presentation.LambdaFunction.Serialization.LambdaFunctionJsonSerializerContext>))]

namespace CleanLambdaFunction.Presentation.LambdaFunction;

using Amazon.Lambda.Core;
using Application;
using Application.Authors.Queries.GetAuthors;
using Application.Movies.Queries.GetMovies;
using Application.Reviews.Queries.GetReviews;
using Infrastructure;
using Mapping;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models;
using AppPaging = Application.Common.Paging;

/// <summary>
/// The Lambda functions. Lambda creates one handler per execution environment and reuses it for every invocation,
/// so the services and the database are set up once, when the environment starts.
/// </summary>
public class Handler
{
    /// <summary>
    /// Lambda can only write to /tmp, so the SQLite database lives there unless the settings say otherwise.
    /// </summary>
    public const string DefaultConnectionString = "Data Source=/tmp/movie-reviews.db";

    private readonly ServiceProvider provider;
    private readonly LambdaFunctionMapper mapper = new();

    /// <summary>
    /// Creates the handler Lambda uses, with settings from environment variables such as
    /// <c>ConnectionStrings__MovieReviews</c> and <c>Database__SeedSampleData</c>.
    /// </summary>
    public Handler()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    /// <summary>
    /// Creates a handler from configuration, using the same keys as the other templates.
    /// </summary>
    /// <param name="configuration">The settings, read from <c>ConnectionStrings:MovieReviews</c> and <c>Database:SeedSampleData</c>.</param>
    public Handler(IConfiguration configuration)
        : this(ConnectionStringFrom(configuration), configuration.GetValue<bool>("Database:SeedSampleData"))
    {
    }

    /// <summary>
    /// Creates a handler for a database, seeding sample data into it when it's empty and asked to.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="seedSampleData">True to seed sample data into an empty database.</param>
    public Handler(string connectionString, bool seedSampleData)
    {
        var services = new ServiceCollection();

        _ = services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        _ = services.AddInfrastructure(connectionString);
        _ = services.AddApplication();

        this.provider = services.BuildServiceProvider(validateScopes: true);
        _ = this.provider.InitialiseInfrastructure(seedSampleData);
    }

    /// <summary>
    /// Lists movies ordered by title, one page at a time, optionally only those whose title contains a search term.
    /// This is the function the image runs by default.
    /// </summary>
    /// <param name="request">The search term, offset and limit.</param>
    /// <param name="context">The Lambda invocation context.</param>
    /// <returns>One page of movies, each with its review count and average stars.</returns>
    public async Task<Page<Movie>> GetMovies(ListRequest request, ILambdaContext context)
    {
        var page = await this.Send(new GetMoviesQuery
        {
            Query = request.Query,
            Offset = request.Offset,
            Limit = request.Limit
        }, context);

        return ToPage(page, this.mapper.ToPresentation);
    }

    /// <summary>
    /// Lists authors ordered by last name then first name, one page at a time, optionally only those whose name contains a search term.
    /// </summary>
    /// <param name="request">The search term, offset and limit.</param>
    /// <param name="context">The Lambda invocation context.</param>
    /// <returns>One page of authors, each with their review count and average stars.</returns>
    public async Task<Page<Author>> GetAuthors(ListRequest request, ILambdaContext context)
    {
        var page = await this.Send(new GetAuthorsQuery
        {
            Query = request.Query,
            Offset = request.Offset,
            Limit = request.Limit
        }, context);

        return ToPage(page, this.mapper.ToPresentation);
    }

    /// <summary>
    /// Lists reviews ordered by movie then author, one page at a time, optionally only those of one movie, by one author, or both.
    /// </summary>
    /// <param name="request">The movie, author, offset and limit.</param>
    /// <param name="context">The Lambda invocation context.</param>
    /// <returns>One page of reviews.</returns>
    public async Task<Page<Review>> GetReviews(ListReviewsRequest request, ILambdaContext context)
    {
        var page = await this.Send(new GetReviewsQuery
        {
            MovieId = request.MovieId,
            AuthorId = request.AuthorId,
            Offset = request.Offset,
            Limit = request.Limit
        }, context);

        return ToPage(page, this.mapper.ToPresentation);
    }

    /// <summary>
    /// The connection string from configuration, or the default in /tmp when it isn't set.
    /// </summary>
    /// <param name="configuration">The settings to read <c>ConnectionStrings:MovieReviews</c> from.</param>
    /// <returns>The connection string the handler uses.</returns>
    public static string ConnectionStringFrom(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetConnectionString("MovieReviews") ?? DefaultConnectionString;
    }

    private static Page<TResult> ToPage<T, TResult>(AppPaging.Page<T> page, Func<T, TResult> map)
    {
        var nextOffset = page.Offset + page.Items.Count;

        return new Page<TResult>([.. page.Items.Select(map)], page.TotalCount, nextOffset < page.TotalCount ? nextOffset : null);
    }

    private async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, ILambdaContext context)
    {
        // Stop the work when the invocation runs out of time, rather than letting Lambda kill it part way through.
        using var cancellation = context.RemainingTime > TimeSpan.Zero
            ? new CancellationTokenSource(context.RemainingTime)
            : new CancellationTokenSource();

        await using var scope = this.provider.CreateAsyncScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(request, cancellation.Token);
    }
}
