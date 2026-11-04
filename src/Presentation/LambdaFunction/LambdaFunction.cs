[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(CleanLambdaFunction.Presentation.LambdaFunction.Serialization.LambdaFunctionJsonSerializer))]

namespace CleanLambdaFunction.Presentation.LambdaFunction;

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Application.Authors.Queries.GetAuthors;
using Application.Movies.Queries.GetMovies;
using Application.Reviews.Queries.GetReviews;
using FluentValidation;
using Infrastructure;
using Mapping;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using AppPaging = Application.Common.Paging;

/// <summary>
/// The Lambda functions. Lambda Annotations generates a class for each one, such as <c>Handler_GetMovies_Generated</c>,
/// and that class is what Lambda calls. It creates one handler per execution environment and reuses it for every
/// invocation, so the database is set up once, when the environment starts. Each invocation is one trace, started by
/// the OpenTelemetry Lambda wrapper and flushed before the function returns, because Lambda freezes the environment
/// as soon as it does.
/// </summary>
public class Handler
{
    private readonly IServiceProvider services;
    private readonly TracerProvider tracerProvider;
    private readonly MeterProvider meterProvider;
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
        this.tracerProvider = services.GetRequiredService<TracerProvider>();
        this.meterProvider = services.GetRequiredService<MeterProvider>();
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
        return await this.Invoke(request, context, () => this.ListMovies(request, context));
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
        return await this.Invoke(request, context, () => this.ListAuthors(request, context));
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
        return await this.Invoke(request, context, () => this.ListReviews(request, context));
    }

    private async Task<Page<Movie>> ListMovies(ListRequest request, ILambdaContext context)
    {
        var page = await this.Send(new GetMoviesQuery
        {
            Query = request.Query,
            Offset = request.Offset,
            Limit = request.Limit
        }, context);

        return ToPage(page, this.mapper.ToPresentation);
    }

    private async Task<Page<Author>> ListAuthors(ListRequest request, ILambdaContext context)
    {
        var page = await this.Send(new GetAuthorsQuery
        {
            Query = request.Query,
            Offset = request.Offset,
            Limit = request.Limit
        }, context);

        return ToPage(page, this.mapper.ToPresentation);
    }

    private async Task<Page<Review>> ListReviews(ListReviewsRequest request, ILambdaContext context)
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

    #region HTTP API

    /// <summary>
    /// The same list as <see cref="GetMovies"/>, for API Gateway: <c>GET /movies?query=&amp;offset=&amp;limit=</c>.
    /// </summary>
    /// <param name="query">The words to look for in the title, ignoring case. Leave empty to list every movie.</param>
    /// <param name="offset">The number of movies to skip.</param>
    /// <param name="limit">The most movies to return, from 1 to 50.</param>
    /// <param name="context">The Lambda invocation context.</param>
    /// <returns><c>200</c> with one page of movies, or <c>400</c> with problem details when the request is invalid.</returns>
    [LambdaFunction(ResourceName = "HttpGetMovies", PackageType = LambdaPackageType.Image, MemorySize = 256, Timeout = 30)]
    [HttpApi(LambdaHttpMethod.Get, "/movies")]
    public async Task<IHttpResult> HttpGetMovies(
        [FromQuery] string query,
        [FromQuery] int? offset,
        [FromQuery] int? limit,
        ILambdaContext context)
    {
        var request = new ListRequest
        {
            Query = EmptyAsNull(query),
            Offset = offset ?? 0,
            Limit = limit ?? AppPaging.Paging.DefaultLimit
        };

        return await this.Invoke(request, context, () => Http(() => this.ListMovies(request, context)));
    }

    /// <summary>
    /// The same list as <see cref="GetAuthors"/>, for API Gateway: <c>GET /authors?query=&amp;offset=&amp;limit=</c>.
    /// </summary>
    /// <param name="query">The words to look for in the author's name, ignoring case. Leave empty to list every author.</param>
    /// <param name="offset">The number of authors to skip.</param>
    /// <param name="limit">The most authors to return, from 1 to 50.</param>
    /// <param name="context">The Lambda invocation context.</param>
    /// <returns><c>200</c> with one page of authors, or <c>400</c> with problem details when the request is invalid.</returns>
    [LambdaFunction(ResourceName = "HttpGetAuthors", PackageType = LambdaPackageType.Image, MemorySize = 256, Timeout = 30)]
    [HttpApi(LambdaHttpMethod.Get, "/authors")]
    public async Task<IHttpResult> HttpGetAuthors(
        [FromQuery] string query,
        [FromQuery] int? offset,
        [FromQuery] int? limit,
        ILambdaContext context)
    {
        var request = new ListRequest
        {
            Query = EmptyAsNull(query),
            Offset = offset ?? 0,
            Limit = limit ?? AppPaging.Paging.DefaultLimit
        };

        return await this.Invoke(request, context, () => Http(() => this.ListAuthors(request, context)));
    }

    /// <summary>
    /// The same list as <see cref="GetReviews"/>, for API Gateway: <c>GET /reviews?movieId=&amp;authorId=&amp;offset=&amp;limit=</c>.
    /// </summary>
    /// <param name="movieId">Only reviews of this movie. Leave empty for every movie.</param>
    /// <param name="authorId">Only reviews by this author. Leave empty for every author.</param>
    /// <param name="offset">The number of reviews to skip.</param>
    /// <param name="limit">The most reviews to return, from 1 to 50.</param>
    /// <param name="context">The Lambda invocation context.</param>
    /// <returns>
    /// <c>200</c> with one page of reviews, or <c>400</c> with problem details when an id isn't a GUID or the request is invalid.
    /// </returns>
    [LambdaFunction(ResourceName = "HttpGetReviews", PackageType = LambdaPackageType.Image, MemorySize = 256, Timeout = 30)]
    [HttpApi(LambdaHttpMethod.Get, "/reviews")]
    public async Task<IHttpResult> HttpGetReviews(
        [FromQuery] string movieId,
        [FromQuery] string authorId,
        [FromQuery] int? offset,
        [FromQuery] int? limit,
        ILambdaContext context)
    {
        return await this.Invoke((movieId, authorId, offset, limit), context, async () =>
        {
            // The generator only binds primitive types from the query string, so the ids arrive as text.
            var errors = new Dictionary<string, string[]>();
            var movie = ParseId(movieId, nameof(movieId), errors);
            var author = ParseId(authorId, nameof(authorId), errors);

            if (errors.Count > 0)
            {
                return BadRequest(errors);
            }

            var request = new ListReviewsRequest
            {
                MovieId = movie,
                AuthorId = author,
                Offset = offset ?? 0,
                Limit = limit ?? AppPaging.Paging.DefaultLimit
            };

            return await Http(() => this.ListReviews(request, context));
        });
    }

    /// <summary>
    /// Turns the result into <c>200 OK</c>, and a validation failure into <c>400</c> problem details. Anything else is
    /// left to fail the invocation, so Lambda logs it and API Gateway answers <c>500</c>. A function that can raise
    /// <c>NotFoundException</c> adds a catch for it here that returns <c>404</c>.
    /// </summary>
    private static async Task<IHttpResult> Http<T>(Func<Task<T>> function)
    {
        try
        {
            return HttpResults.Ok(await function());
        }
        catch (ValidationException exception)
        {
            return BadRequest(exception.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));
        }
    }

    private static IHttpResult BadRequest(IDictionary<string, string[]> errors)
    {
        return ProblemResult(HttpResults.BadRequest(new Problem("One or more validation errors occurred.", StatusCodes.BadRequest, Errors: errors)));
    }

    private static Guid? ParseId(string value, string name, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Guid.TryParse(value, out var id))
        {
            return id;
        }

        errors[name] = [$"'{value}' is not a valid id."];

        return null;
    }

    private static string? EmptyAsNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IHttpResult ProblemResult(IHttpResult result)
    {
        return result.AddHeader("Content-Type", "application/problem+json");
    }

    private static class StatusCodes
    {
        public const int BadRequest = 400;
    }

    #endregion HTTP API

    private static Page<TResult> ToPage<T, TResult>(AppPaging.Page<T> page, Func<T, TResult> map)
    {
        var nextOffset = page.Offset + page.Items.Count;

        return new Page<TResult>([.. page.Items.Select(map)], page.TotalCount, nextOffset < page.TotalCount ? nextOffset : null);
    }

    /// <summary>
    /// Runs one invocation inside the span the OpenTelemetry Lambda wrapper starts, which picks up the caller's X-Ray
    /// trace when there is one. The wrapper flushes the traces; the metrics are flushed here, because Lambda can freeze
    /// the environment for good as soon as the function returns.
    /// </summary>
    private async Task<TResult> Invoke<TInput, TResult>(TInput input, ILambdaContext context, Func<Task<TResult>> function)
    {
        try
        {
            return await AWSLambdaWrapper.TraceAsync(this.tracerProvider, (_, _) => function(), input, context);
        }
        finally
        {
            _ = this.meterProvider.ForceFlush();
        }
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
