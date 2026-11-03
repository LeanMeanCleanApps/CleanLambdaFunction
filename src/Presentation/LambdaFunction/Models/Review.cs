namespace CleanLambdaFunction.Presentation.LambdaFunction.Models;

/// <summary>
/// A review of a movie.
/// </summary>
/// <param name="Id">The unique identifier of the review.</param>
/// <param name="Stars">The star rating, from 1 to 5.</param>
/// <param name="ReviewedMovie">The movie the review is about.</param>
/// <param name="ReviewAuthor">The author who wrote the review.</param>
public record Review(Guid Id, int Stars, Movie? ReviewedMovie, Author? ReviewAuthor);
