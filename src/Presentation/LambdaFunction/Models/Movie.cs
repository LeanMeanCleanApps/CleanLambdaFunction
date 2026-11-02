namespace CleanLambdaFunction.Presentation.LambdaFunction.Models;

/// <summary>
/// A movie, with its review count and average stars when it comes from a list of movies.
/// </summary>
/// <param name="Id">The unique identifier of the movie.</param>
/// <param name="Title">The title of the movie.</param>
/// <param name="ReviewCount">The number of reviews of the movie. Always 0 on the movie inside a review.</param>
/// <param name="AverageStars">The average star rating, or null when the movie has no reviews or is inside a review.</param>
public record Movie(Guid Id, string Title, int ReviewCount = 0, double? AverageStars = null);
