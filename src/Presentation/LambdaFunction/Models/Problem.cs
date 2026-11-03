namespace CleanLambdaFunction.Presentation.LambdaFunction.Models;

/// <summary>
/// Why an HTTP request failed, as RFC 9457 problem details.
/// </summary>
/// <param name="Title">A short summary of the problem.</param>
/// <param name="Status">The HTTP status code.</param>
/// <param name="Detail">What went wrong with this request, when there's more to say than the title.</param>
/// <param name="Errors">The messages for each request field that broke a rule, when the request was invalid.</param>
public record Problem(
    string Title,
    int Status,
    string? Detail = null,
    IDictionary<string, string[]>? Errors = null);
