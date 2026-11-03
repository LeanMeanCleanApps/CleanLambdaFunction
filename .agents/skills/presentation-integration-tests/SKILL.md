---
name: presentation-integration-tests
description: How this Lambda function is tested end to end - calling Handler methods with a TestLambdaContext, services from Startup with a private in-memory database per test class, the generated handler classes, and asserting exceptions. Use when writing or fixing a test in tests/Presentation.Tests.Integration.
metadata:
  scope: presentation
---

# Presentation integration tests (Lambda function)

## The rule

`tests/Presentation.Tests.Integration` tests the whole function in memory by calling `Handler` methods the way Lambda does. Each test class creates one `Handler` from the services `Startup` registers, with its own shared-cache in-memory SQLite database and the sample data on, and a `TestLambdaContext` (from `Amazon.Lambda.TestUtilities`) with time remaining.

| File | Tests |
| --- | --- |
| `HandlerTests.cs` | Every function's results, paging, search and filters, the validation failures, and how the handler reads its settings |

The serializer is tested on its own in `tests/Presentation.Tests.Unit` (`unit-tests`, `json-serialization`).

There are two ways in, and tests use whichever the case needs:

| Way in | Use it to |
| --- | --- |
| `CreateHandler(settings)` | Most tests. It builds `new Startup(settings)` with scope validation on and resolves `Handler`, so settings such as `ConnectionStrings:MovieReviews` and `Database:SeedSampleData` override the environment |
| `new Handler_<Method>_Generated()` | The environment variable test, which calls each generated class exactly as Lambda does, then clears the variables |

## Steps

1. Use the class's `handler` and `context` fields. Create a second handler with `CreateHandler` only when the test needs other settings.
2. Call the function, then assert the result: counts, `NextOffset`, and that every item matches the filter.
3. Failures: `await Should.ThrowAsync<ValidationException>(...)` and check the property name in the errors, or `NotFoundException` and its message.
4. Find the rows you need through another function (list, then pick one), not by hard-coded ids. Sample data ids are random.
5. A test that sets environment variables clears them in `finally`.
6. Write one test per success and per failure the function can return (`test-design`).

## Example

```csharp
// tests/Presentation.Tests.Integration/HandlerTests.cs
private readonly Handler handler = CreateHandler(new()
{
    ["ConnectionStrings:MovieReviews"] = InMemoryDatabase(),
    ["Database:SeedSampleData"] = "true"
});
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
