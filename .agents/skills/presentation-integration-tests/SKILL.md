---
name: presentation-integration-tests
description: How this Lambda function is tested end to end - calling Handler methods directly with a TestLambdaContext, a private in-memory database per test class, settings through configuration, asserting exceptions, and the serializer tests. Use when writing or fixing a test in tests/Presentation.Tests.Integration.
metadata:
  scope: presentation
---

# Presentation integration tests (Lambda function)

## The rule

`tests/Presentation.Tests.Integration` tests the whole function in memory by calling `Handler` methods the way Lambda does. Each test class creates one `Handler` with its own shared-cache in-memory SQLite database and the sample data on, and a `TestLambdaContext` (from `Amazon.Lambda.TestUtilities`) with time remaining.

| File | Tests |
| --- | --- |
| `HandlerTests.cs` | Every function's results, paging, search and filters, the validation failures, and how the handler reads its settings |
| `SerializerTests.cs` | The JSON a caller sends and gets back, through the real Lambda serializer |

The handler takes its settings in three ways, and tests use whichever the case needs:

| Constructor | Use it to |
| --- | --- |
| `new Handler(connectionString, seedSampleData)` | Most tests: a database and sample data on or off |
| `new Handler(IConfiguration)` | Settings tests, with an in-memory configuration |
| `new Handler()` | The environment variable test, which sets and then clears the variables |

## Steps

1. Use the class's `handler` and `context` fields. Create a second `Handler` only when the test needs other settings.
2. Call the function, then assert the result: counts, `NextOffset`, and that every item matches the filter.
3. Failures: `await Should.ThrowAsync<ValidationException>(...)` and check the property name in the errors, or `NotFoundException` and its message.
4. Find the rows you need through another function (list, then pick one), not by hard-coded ids. Sample data ids are random.
5. A test that sets environment variables clears them in `finally`.
6. Write one test per success and per failure the function can return (`test-design`).

## Example

```csharp
// tests/Presentation.Tests.Integration/HandlerTests.cs
private readonly Handler handler = new($"Data Source=Tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared", seedSampleData: true);
private readonly TestLambdaContext context = new() { RemainingTime = TimeSpan.FromSeconds(30) };

[Fact]
public async Task GetMovies_ShouldThrow_WhenLimitIsNotValid()
{
    // Act
    var exception = await Should.ThrowAsync<ValidationException>(() => this.handler.GetMovies(new ListRequest { Limit = 0 }, this.context));

    // Assert
    exception.Errors.ShouldContain(e => e.PropertyName == "Limit" && e.ErrorMessage == "A limit must be between 1 and 50.");
}
```

## Don't

- Share a `Handler` across test classes or in a `static` field. Each class gets its own database.
- Point a test at `/tmp/movie-reviews.db` or any file.
- Leave an environment variable set after a test.
- Depend on the order rows come back in unless the function promises one.

## Check

``` bash
dotnet test tests/Presentation.Tests.Integration
```
