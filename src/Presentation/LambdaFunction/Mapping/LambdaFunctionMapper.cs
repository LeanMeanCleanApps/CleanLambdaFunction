namespace CleanLambdaFunction.Presentation.LambdaFunction.Mapping;

using Riok.Mapperly.Abstractions;
using AppAuthor = Application.Authors.Entities.AuthorSummary;
using AppMovie = Application.Movies.Entities.MovieSummary;
using AppReview = Application.Reviews.Entities.Review;
using AppReviewAuthor = Application.Authors.Entities.ReviewAuthor;
using AppReviewedMovie = Application.Movies.Entities.ReviewedMovie;
using PresentationAuthor = Models.Author;
using PresentationMovie = Models.Movie;
using PresentationReview = Models.Review;

[Mapper]
internal sealed partial class LambdaFunctionMapper
{
    public partial PresentationAuthor ToPresentation(AppAuthor author);

    public partial PresentationMovie ToPresentation(AppMovie movie);

    [MapperIgnoreTarget(nameof(PresentationMovie.ReviewCount))]
    [MapperIgnoreTarget(nameof(PresentationMovie.AverageStars))]
    private partial PresentationMovie ToPresentation(AppReviewedMovie movie);

    [MapperIgnoreTarget(nameof(PresentationAuthor.ReviewCount))]
    [MapperIgnoreTarget(nameof(PresentationAuthor.AverageStars))]
    private partial PresentationAuthor ToPresentation(AppReviewAuthor author);

    public partial PresentationReview ToPresentation(AppReview review);
}
