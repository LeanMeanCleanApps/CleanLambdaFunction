namespace CleanLambdaFunction.Presentation.LambdaFunction.Models;

/// <summary>
/// An author of reviews, with their review count and average stars.
/// </summary>
/// <param name="Id">The unique identifier of the author.</param>
/// <param name="FirstName">The author's first name.</param>
/// <param name="LastName">The author's last name.</param>
/// <param name="ReviewCount">The number of reviews the author has written.</param>
/// <param name="AverageStars">The average star rating the author gives, or null when they have written no reviews.</param>
public record Author(Guid Id, string FirstName, string LastName, int ReviewCount = 0, double? AverageStars = null);
