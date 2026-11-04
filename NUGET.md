![Clean Lambda Function](https://raw.githubusercontent.com/LeanMeanCleanApps/brand/main/assets/banners/png/lambda.png)

A `dotnet new` template for an [AWS Lambda function](https://docs.aws.amazon.com/lambda/latest/dg/lambda-csharp.html) in .NET 10, built with a streamlined Clean Architecture and shipped as a container image. All the layers you need, none of the ceremony you don't.

It's one of five [Lean Mean Clean Apps](https://leanmeancleanapps.github.io/) templates, for Minimal APIs, Model Context Protocol, GraphQL, Web API controllers and AWS Lambda. They share the same Application and Infrastructure layers and differ only in how they face the world. The full docs live at [leanmeancleanapps.github.io/lambda](https://leanmeancleanapps.github.io/lambda/).

## Get started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). [Docker](https://www.docker.com/products/docker-desktop) is optional, but it's how you run the function locally.

```bash
# Install the template
dotnet new install CleanLambdaFunction.Template

# Create a solution
dotnet new cleanlambdafunction --name Acme.Reviews --authorName "Your Name"
cd Acme.Reviews
```

| Option | What it does |
| --- | --- |
| `--name`, `-n` | Names the solution and its root namespace |
| `--authorName`, `-an` | Puts your name on the assembly instead of ours |
| `--no-restore` | Skips the package restore that normally runs once the solution is created |

Rather click than type? Visual Studio and Rider show the same options in their new project dialogs, and `dotnet new cleanlambdafunction --help` lists them in the terminal.

## Run it

The image ships with the Lambda runtime emulator, so it answers exactly like Lambda would. No AWS account needed until you're ready.

```bash
# Build the image and start it with sample data, plus a collector and the Aspire dashboard for its telemetry
docker compose up -d --build

# Ask it for a page of movies
curl -X POST http://localhost:9000/2015-03-31/functions/function/invocations \
  -d '{ "query": "global", "limit": 2 }'

# Run the tests while you're here
dotnet test
```

When you're ready for the real thing, [deploy it to AWS](https://leanmeancleanapps.github.io/lambda/guides/deploy-to-aws/). The function is invoked directly, so AWS IAM decides who gets to call it.

## What's inside

- **Presentation**: the Lambda handler, using the source-generated serializer so cold starts don't pay for reflection
- **Application**: commands and queries through [Mediator](https://github.com/martinothamar/Mediator), validated by [FluentValidation](https://github.com/FluentValidation/FluentValidation) and mapped by [Mapperly](https://mapperly.riok.app)
- **Infrastructure**: [Entity Framework Core](https://learn.microsoft.com/ef/core) on SQLite, with optional sample data
- **Tests**: [xUnit](https://xunit.net), [Shouldly](https://github.com/shouldly/shouldly) and [NSubstitute](https://github.com/nsubstitute/NSubstitute), each on its own throwaway database
- **Extras**: [OpenTelemetry](https://opentelemetry.io) traces, metrics and [Serilog](https://github.com/serilog/serilog) logs over OTLP, a Dockerfile that runs as a non-root user, Docker Compose, and a GitHub Actions pipeline that won't let your coverage slip

## Releases

A new version lands on the 1st of each month, as long as something changed. No changes, no release. We're not here to pad the download stats.

```bash
dotnet new update
```

## More help

- [Docs](https://leanmeancleanapps.github.io/lambda/) for guides, a reference and why it's built this way
- [GitHub](https://github.com/LeanMeanCleanApps/CleanLambdaFunction) for the code, [issues](https://github.com/LeanMeanCleanApps/CleanLambdaFunction/issues) and [release notes](https://github.com/LeanMeanCleanApps/CleanLambdaFunction/releases)
- [Security policy](https://github.com/LeanMeanCleanApps/CleanLambdaFunction/security/policy) to report a vulnerability privately

## Who built this

Built by [Stephen Walsh](https://github.com/stphnwlsh) in spare time, with strong coffee and a healthy disregard for pomp and ceremony. If this template saves you some time, [sponsoring the work](https://github.com/sponsors/stphnwlsh) keeps all five templates current and the coffee flowing.
