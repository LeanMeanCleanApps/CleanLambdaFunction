---
name: json-serialization
description: How this Lambda function reads and writes JSON - the assembly-level SourceGeneratorLambdaJsonSerializer, the source-generated LambdaFunctionJsonSerializerContext, which types it must list, and how JSON is shaped. Use when adding a request or response type, changing how JSON looks, or when a value serialises differently than expected.
metadata:
  scope: presentation
---

# JSON serialization (Lambda function)

## The rule

Lambda hands the function a JSON payload and expects JSON back. The `[assembly: LambdaSerializer(...)]` attribute at the top of `LambdaFunction.cs` sets the serializer for every function: `SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>` from `Amazon.Lambda.Serialization.SystemTextJson`. It uses the source-generated metadata in `src/Presentation/LambdaFunction/Serialization/LambdaFunctionJsonSerializerContext.cs`, so nothing is serialised with reflection, which keeps cold starts short.

| Behaviour | Effect |
| --- | --- |
| Property names read in any case | `{"query":"star"}` and `{"Query":"star"}` both fill `Query` |
| Property names written as declared | Responses read `Items`, `TotalCount`, `NextOffset` |
| Nulls left out | `NextOffset` disappears on the last page, `AverageStars` when there are no reviews |
| Only listed types | A type missing from the context can't be serialised |

Responses are trees: a list returns flat records, and a review holds a flat `Movie`. There's no cycle handling.

## Steps

1. Add every new request and response type to `LambdaFunctionJsonSerializerContext` with `[JsonSerializable(typeof(...))]`, including each `Page<T>` you return.
2. Change how JSON looks on the serializer, not on single properties, and keep the request and response tests in `SerializerTests.cs` in step.
3. Add a serializer test for a new request type (it reads the JSON a caller sends) and for any response whose shape matters to callers.

## Example

```csharp
// src/Presentation/LambdaFunction/Serialization/LambdaFunctionJsonSerializerContext.cs
[JsonSerializable(typeof(ListRequest))]
[JsonSerializable(typeof(ListReviewsRequest))]
[JsonSerializable(typeof(Page<Movie>))]
[JsonSerializable(typeof(Page<Author>))]
[JsonSerializable(typeof(Page<Review>))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}
```

## Don't

- Switch to `DefaultLambdaJsonSerializer`. It uses reflection and slows every cold start.
- Use `[JsonPropertyName]` to rename one property.
- Use Newtonsoft.Json or `Amazon.Lambda.Serialization.Json`.
- Let a response type refer back to its parent.

## Check

``` bash
dotnet test tests/Presentation.Tests.Integration --filter "FullyQualifiedName~Serializer"
```
