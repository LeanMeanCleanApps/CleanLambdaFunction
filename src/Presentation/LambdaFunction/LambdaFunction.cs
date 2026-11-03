[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(CleanLambdaFunction.Presentation.LambdaFunction.Serialization.LambdaFunctionJsonSerializer))]

namespace CleanLambdaFunction.Presentation.LambdaFunction;

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
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
/// The Lambda functions. Lambda Annotations generates a class for each one, such as <c>Handler_GetMovies_Generated</c>,
/// and that class is what Lambda calls. It creates one handler per execution environment and reuses it for every
/// invocation, so the database is set up once, when the environment starts.
/// </summary>
public class Handler
{
    private readonly IServiceProvider services;
    private readonly LambdaFunctionMapper mapper = new();

    /// <summary>
    /// Creates or upgrades the database, and seeds sample data into it when it's empty and
    /// <c>Database:SeedSampleData</c> is true.
    /// </summary>
    /// <param name="services">The services <see cref="Startup"/> registered.</param>
    /// <param name="configuration">The settings to read <c>Database:SeedSampleData</c> from.</param>
    public Handler(IServiceProvider services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        this.services = services.InitialiseInfrastructure(configuration.GetValue<bool>("Database:SeedSampleData"));
    }

    /// <summary>
    /// Lists movies ordered by title, one page at a time, optionally only those whose title contains a search term.
    /// This is the function the image runs by default.
    /// </summary>
    /// <param name="request">The search term, offset and limit.</param>
    /// <param name="context">The Lambda invocation context.</param>
    /// <returns>One page of movies, each with its review count and average stars.</returns>
    [LambdaFunction(ResourceName = "GetMovies", PackageType = LambdaPackageType.Image, MemorySize = 256, Timeout = 30)]
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
    [LambdaFunction(ResourceName = "GetAuthors", PackageType = LambdaPackageType.Image, MemorySize = 256, Timeout = 30)]
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
    [LambdaFunction(ResourceName = "GetReviews", PackageType = LambdaPackageType.Image, MemorySize = 256, Timeout = 30)]
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

        // The generated classes create a scope too, but only [FromServices] parameters use it, and the 2.4 generator
        // writes a documentation link for those that doesn't resolve, which fails the build. So each invocation makes its own.
        await using var scope = this.services.CreateAsyncScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(request, cancellation.Token);
    }
}
