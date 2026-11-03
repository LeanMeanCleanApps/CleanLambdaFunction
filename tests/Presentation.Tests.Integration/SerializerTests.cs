namespace CleanLambdaFunction.Presentation.Tests.Integration;

using System.Text;
using CleanLambdaFunction.Presentation.LambdaFunction.Models;
using CleanLambdaFunction.Presentation.LambdaFunction.Serialization;
using Shouldly;
using Xunit;

public class SerializerTests
{
    private readonly LambdaFunctionJsonSerializer serializer = new();

    [Fact]
    public void Deserialize_ShouldRead_ListRequest()
    {
        // Arrange
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(/*lang=json,strict*/ """{"query":"star","offset":20,"limit":5}"""));

        // Act
        var request = this.serializer.Deserialize<ListRequest>(input);

        // Assert
        request.Query.ShouldBe("star");
        request.Offset.ShouldBe(20);
        request.Limit.ShouldBe(5);
    }

    [Fact]
    public void Deserialize_ShouldRead_ListRequest_InAnyCase()
    {
        // Arrange
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(/*lang=json,strict*/ """{"Query":"star","OFFSET":20}"""));

        // Act
        var request = this.serializer.Deserialize<ListRequest>(input);

        // Assert
        request.Query.ShouldBe("star");
        request.Offset.ShouldBe(20);
    }

    [Fact]
    public void Serialize_ShouldWrite_Review_WithMovieAndAuthor()
    {
        // Arrange
        using var output = new MemoryStream();
        var review = new Review(Guid.Empty, 4, new Movie(Guid.Empty, "Lorem Ipsum"), new Author(Guid.Empty, "Dolor", "Sit"));
        var page = new Page<Review>([review], 1, null);

        // Act
        this.serializer.Serialize(page, output);

        // Assert
        Encoding.UTF8.GetString(output.ToArray()).ShouldBe(/*lang=json,strict*/ """{"items":[{"id":"00000000-0000-0000-0000-000000000000","stars":4,"reviewedMovie":{"id":"00000000-0000-0000-0000-000000000000","title":"Lorem Ipsum","reviewCount":0},"reviewAuthor":{"id":"00000000-0000-0000-0000-000000000000","firstName":"Dolor","lastName":"Sit","reviewCount":0}}],"totalCount":1}""");
    }

    [Fact]
    public void Serialize_ShouldWrite_Page_WithoutNulls()
    {
        // Arrange
        using var output = new MemoryStream();
        var page = new Page<Movie>([new Movie(Guid.Empty, "Lorem Ipsum")], 1, null);

        // Act
        this.serializer.Serialize(page, output);

        // Assert
        Encoding.UTF8.GetString(output.ToArray()).ShouldBe(/*lang=json,strict*/ """{"items":[{"id":"00000000-0000-0000-0000-000000000000","title":"Lorem Ipsum","reviewCount":0}],"totalCount":1}""");
    }
}
