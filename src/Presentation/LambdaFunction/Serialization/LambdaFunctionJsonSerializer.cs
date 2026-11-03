namespace CleanLambdaFunction.Presentation.LambdaFunction.Serialization;

using System.Text.Json;
using Amazon.Lambda.Serialization.SystemTextJson;

/// <summary>
/// Reads and writes every request and response with the source-generated metadata, in camelCase like the other templates.
/// Property names are read without regard to case, and null values are left out of responses.
/// </summary>
public sealed class LambdaFunctionJsonSerializer()
    : SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>(options => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
