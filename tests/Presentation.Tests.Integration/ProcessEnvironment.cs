namespace CleanLambdaFunction.Presentation.Tests.Integration;

using Xunit;

/// <summary>
/// Environment variables belong to the whole process, and the handlers read them. Tests that set them, and tests that
/// rely on a setting being missing, run here, away from everything else, so one can't leak into another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironment
{
    public const string Name = "Process environment";
}
