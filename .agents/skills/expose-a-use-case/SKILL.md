---
name: expose-a-use-case
description: How to put an Application use case on a Lambda function - a handler method that sends the request through Mediator with a cancellation token from the remaining time, request and response records, the Mapperly mapper, the source-generated serializer context and tests. Use when adding or changing a function in this Lambda template.
metadata:
  scope: presentation
---

# Expose a use case (Lambda function)

## The rule

Every function is a public method on `Handler` (`src/Presentation/LambdaFunction/LambdaFunction.cs`) with the signature `Task<TResponse> Name(TRequest request, ILambdaContext context)`. Lambda creates one `Handler` per execution environment, so the services and the database are set up once in its constructor and reused for every invocation. Each method maps its request to the Application request, sends it through the private `Send` helper and maps the result to a presentation model. `Send` opens a DI scope per invocation and cancels the work when the invocation's remaining time runs out.

Functions contain no logic. Validation, not-found and everything else happen in Application, and their exceptions reach the caller as Lambda function errors (`api-conventions`).

## Steps

1. Add the use case first (`add-a-use-case`).
2. Add the request as a `record` in `src/Presentation/LambdaFunction/Models/` with `init` properties, defaults where the Application request has them, and XML docs on the type and each property.
3. Add or reuse a response `record` in the same folder, with a `<param>` tag for each value. Map Application entities to it with a `partial` method on `LambdaFunctionMapper` (`mapping-with-mapperly`). Lists return `Page<T>` through `ToPage`.
4. Add the method to `Handler` and send through `this.Send(..., context)`. Never resolve services or create tokens yourself.
5. Add every new request and response type to `LambdaFunctionJsonSerializerContext` with `[JsonSerializable(typeof(...))]` (`json-serialization`).
6. Document the method: `<summary>` with what it returns and in what order, `<param>` for the request and the context, and `<returns>`.
7. Test it through the handler in `tests/Presentation.Tests.Integration/HandlerTests.cs` (`presentation-integration-tests`). Cover each success and each validation or not-found failure (`test-design`).
8. A new function is deployed as its own Lambda function with the handler string `Presentation::CleanLambdaFunction.Presentation.LambdaFunction.Handler::<Method>`. Mention it in `docs/RUN.md`.

## Example

```csharp
// src/Presentation/LambdaFunction/LambdaFunction.cs
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
```

## Don't

- Return Application entities. Map them to the records in `Models/`, so the function's contract doesn't move when Application does.
- Catch exceptions or check `null` in a function.
- Build a new `ServiceProvider` per invocation. The constructor's one is reused, and `Send` scopes each call.
- Use `async void`, or block with `.Result` or `.Wait()`.
- Forget `[JsonSerializable]` for a new type. The serializer has no reflection fallback.

## Check

``` bash
dotnet test tests/Presentation.Tests.Integration
docker compose up -d --build   # then invoke it as in lambda-emulator
```
