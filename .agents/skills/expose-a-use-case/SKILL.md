---
name: expose-a-use-case
description: How to put an Application use case on a Lambda function - a [LambdaFunction] method on Handler that sends the request through Mediator with a cancellation token from the remaining time, request and response records, the Mapperly mapper, the source-generated serializer context, the generated CloudFormation template and tests. Use when adding or changing a function in this Lambda template.
metadata:
  scope: presentation
---

# Expose a use case (Lambda function)

## The rule

Every function is a public method on `Handler` (`src/Presentation/LambdaFunction/LambdaFunction.cs`) with a `[LambdaFunction]` attribute and the signature `Task<TResponse> Name(TRequest request, ILambdaContext context)`. [Lambda Annotations](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Annotations) generates a class for each one, such as `Handler_GetMovies_Generated`, and that class is what Lambda calls. It builds the services from `Startup` once per execution environment, creates the `Handler` as a singleton, and writes the function into `src/Presentation/serverless.template` when the project builds.

The `Handler` constructor creates or upgrades the database once. Each method wraps its body in the private `Invoke` helper, which opens the function's trace span and flushes the metrics when the invocation ends, and inside it maps its request to the Application request, sends it through the private `Send` helper and maps the result to a presentation model. `Send` opens a DI scope per invocation and cancels the work when the invocation's remaining time runs out.

A list that callers also want over HTTP gets a second method in the `#region HTTP API` block, named with an `Http` prefix (`HttpGetMovies`). It has `[HttpApi(LambdaHttpMethod.Get, "/movies")]` beside `[LambdaFunction]`, takes its inputs as `[FromQuery]` parameters and returns `IHttpResult`. It builds the same request record and calls the direct method through the `Http` helper, which answers `200` with the result and `400` problem details for a `ValidationException`. The build adds the route to the stack's API Gateway HTTP API in `serverless.template`.

Functions contain no logic. Validation, not-found and everything else happen in Application, and their exceptions reach the caller as Lambda function errors (`api-conventions`).

## Steps

1. Add the use case first (`add-a-use-case`).
2. Add the request as a `record` in `src/Presentation/LambdaFunction/Models/` with `init` properties, defaults where the Application request has them, and XML docs on the type and each property.
3. Add or reuse a response `record` in the same folder, with a `<param>` tag for each value. Map Application entities to it with a `partial` method on `LambdaFunctionMapper` (`mapping-with-mapperly`). Lists return `Page<T>` through `ToPage`.
4. Add the method to `Handler` with `[LambdaFunction(ResourceName = "<Method>", PackageType = LambdaPackageType.Image, MemorySize = 256, Timeout = 30)]`. Wrap the body in `this.Invoke(request, context, ...)`, so the invocation is traced and its metrics are flushed, and send through `this.Send(..., context)`. Never resolve services or create tokens yourself.
5. Add every new request and response type to `LambdaFunctionJsonSerializerContext` with `[JsonSerializable(typeof(...))]` (`json-serialization`).
6. Document the method: `<summary>` with what it returns and in what order, `<param>` for the request and the context, and `<returns>`.
7. Test it through the handler in `tests/Presentation.Tests.Integration/HandlerTests.cs` (`presentation-integration-tests`). Cover each success and each validation or not-found failure (`test-design`).
8. For an HTTP route, add the `Http<Method>` method as described above, and test it in `tests/Presentation.Tests.Integration/HttpApiTests.cs` for the success, each `400`, and once through the generated class with an `APIGatewayHttpApiV2ProxyRequest`.
9. Build, and commit the function the build adds to `src/Presentation/serverless.template`. It deploys as its own Lambda function from the same image, with the handler string `Presentation::CleanLambdaFunction.Presentation.LambdaFunction.Handler_<Method>_Generated::<Method>`. Mention it in `docs/RUN.md`.

## Example

```csharp
// src/Presentation/LambdaFunction/LambdaFunction.cs
[LambdaFunction(ResourceName = "GetAuthors", PackageType = LambdaPackageType.Image, MemorySize = 256, Timeout = 30)]
public async Task<Page<Author>> GetAuthors(ListRequest request, ILambdaContext context)
{
    return await this.Invoke(request, context, async () =>
    {
        var page = await this.Send(new GetAuthorsQuery
        {
            Query = request.Query,
            Offset = request.Offset,
            Limit = request.Limit
        }, context);

        return ToPage(page, this.mapper.ToPresentation);
    });
}
```

## Don't

- Return Application entities. Map them to the records in `Models/`, so the function's contract doesn't move when Application does.
- Catch exceptions or check `null` in a function.
- Skip `Invoke`. A function that calls `Send` on its own still works, but it leaves no trace and its metrics wait for the next invocation to be sent.
- Build a new `ServiceProvider` per invocation, or register services anywhere but `Startup`. The generated class builds them once, and `Send` scopes each call.
- Take services as `[FromServices]` parameters. Lambda Annotations 2.4 writes a documentation link for them that doesn't resolve, and the build fails on the warning.
- Edit a function's entry in `serverless.template` by hand. The build rewrites it from the attribute. Add other resources and settings beside it.
- Give an `[HttpApi]` method a `Guid` or `string?` parameter. Lambda Annotations 2.4 only binds primitive types from the query string (`AWSLambda0109`), and its generated code has nullable turned off (`CS8669`). Take a `string`, treat empty as missing with `EmptyAsNull`, and parse ids with `ParseId`, which adds a `400` error for text that isn't a GUID.
- Remove the `CS1574` entry from `NoWarn` in `Presentation.csproj`. It's our one suppression here: the classes generated for `[HttpApi]` methods link their docs to our method with a parameter list they don't share, the link can't resolve, and the generated code isn't ours to fix. A nested `.editorconfig` doesn't reach generated files. Don't add any other code to it.
- Use `async void`, or block with `.Result` or `.Wait()`.
- Forget `[JsonSerializable]` for a new type. The serializer has no reflection fallback.

## Check

``` bash
dotnet test tests/Presentation.Tests.Integration
docker compose up -d --build   # then invoke it as in lambda-emulator
```
