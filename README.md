[![Clean Lambda Function](https://raw.githubusercontent.com/LeanMeanCleanApps/brand/main/assets/banners/png/lambda.png)](https://leanmeancleanapps.github.io/lambda/)

[![Build Pipeline](https://img.shields.io/github/actions/workflow/status/LeanMeanCleanApps/CleanLambdaFunction/build-pipeline.yml?branch=main&label=Build%20Pipeline&logo=github&style=for-the-badge)](https://github.com/LeanMeanCleanApps/CleanLambdaFunction/actions/workflows/build-pipeline.yml)
[![NuGet](https://img.shields.io/nuget/v/CleanLambdaFunction.Template?label=NuGet&logo=nuget&logoColor=white&style=for-the-badge)](https://www.nuget.org/packages/CleanLambdaFunction.Template)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CleanLambdaFunction.Template?label=Downloads&logo=nuget&logoColor=white&style=for-the-badge)](https://www.nuget.org/packages/CleanLambdaFunction.Template)
[![License](https://img.shields.io/github/license/LeanMeanCleanApps/CleanLambdaFunction?label=License&style=for-the-badge)](LICENSE.md)

A `dotnet new` template for an [AWS Lambda function](https://docs.aws.amazon.com/lambda/latest/dg/lambda-csharp.html) in .NET 10, built with a streamlined Clean Architecture and shipped as a container image. All the layers you need, none of the ceremony you don't.

It's one of five [Lean Mean Clean Apps](https://leanmeancleanapps.github.io/) templates. They share the same Application and Infrastructure layers and differ only in how they face the world. Same genes, different haircuts.

The guides, the reference and the reasoning behind every opinion live at **[leanmeancleanapps.github.io/lambda](https://leanmeancleanapps.github.io/lambda/)**. This is the version for people who skim. We see you.

## Get started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). [Docker](https://www.docker.com/products/docker-desktop) is optional, but it's how you run the function locally.

```bash
# Install the template
dotnet new install CleanLambdaFunction.Template

# Create a solution
dotnet new cleanlambdafunction --name WorksOnMyMachine.Reviews --authorName "Your Name"
cd WorksOnMyMachine.Reviews
```

| Option | Default | What it does |
| --- | --- | --- |
| `--name`, `-n` | The folder's name | Names the solution and its root namespace |
| `--authorName`, `-an` | `Stephen Walsh` | Puts your name on the assembly instead of ours |
| `--no-restore` | Off | Skips the package restore that normally runs once the solution is created |

Rather click than type? Visual Studio and Rider show the same options in their new project dialogs, and `dotnet new cleanlambdafunction --help` lists them in the terminal.

That's three layers, four test projects, a Dockerfile and a CI pipeline. Go and tell your team you've been working on it all week.

## Run it

The image ships with the Lambda runtime emulator, so it answers exactly like Lambda would. No AWS account needed until you're ready.

```bash
# Build the image and start it with sample data, plus a collector and the Aspire dashboard for its telemetry
docker compose up -d --build

# Ask it for a page of movies
curl -X POST http://localhost:9000/2015-03-31/functions/function/invocations \
  -d '{ "query": "global", "limit": 2 }'
```

```json
{
  "items": [
    {
      "id": "30fca414-247a-779e-105a-d81aa2506f58",
      "title": "Assimilated global infrastructure",
      "reviewCount": 1,
      "averageStars": 1
    },
    {
      "id": "c27d6600-79a6-9346-2595-a52b7f07dc3e",
      "title": "Automated global alliance",
      "reviewCount": 3,
      "averageStars": 3.3
    }
  ],
  "totalCount": 2
}
```

Yes, the sample movies are named by a corporate buzzword generator, and the critics are harsh.

Every function returns one page at a time, with a `totalCount` and a `nextOffset` when there's more to come. Send the `nextOffset` back as `offset` to get the next page. Movies and authors come with their review count and average stars, worked out by the database rather than in a loop somewhere. Each invocation's logs, trace and metrics land in the [Aspire dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone) at <http://localhost:18888>.

The image runs `GetMovies` by default. The same image also has `GetAuthors` and `GetReviews`, and you pick one by passing its handler string when the container starts. [Lambda Annotations](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Annotations) writes the class behind each one, so you only write the method.

```bash
# Free up port 9000 and build the image
docker compose down
docker build . --target run --tag acme-reviews:local

# Start the reviews function instead of movies
docker run --rm -p 9000:8080 \
  -e Database__SeedSampleData=true \
  acme-reviews:local \
  "Presentation::WorksOnMyMachine.Reviews.Presentation.LambdaFunction.Handler_GetReviews_Generated::GetReviews"

# Reviews take a movie or an author instead of a search term
curl -X POST http://localhost:9000/2015-03-31/functions/function/invocations \
  -d '{ "movieId": "30fca414-247a-779e-105a-d81aa2506f58", "limit": 5 }'
```

The database is a SQLite file in `/tmp`, the only place Lambda lets you write, and the function stops any work that would outrun the invocation's remaining time. Lambda bills by the millisecond, so it knows when to call it a day.

When you're ready for the real thing, fill in your profile, region and stack name in `src/Presentation/aws-lambda-tools-defaults.json` and [deploy it to AWS](https://leanmeancleanapps.github.io/lambda/guides/deploy-to-aws/). Every other way to run it, from plain `docker run` to stamping a version into the image, is in [docs/RUN.md](docs/RUN.md).

The lists also come over HTTP. `HttpGetMovies`, `HttpGetAuthors` and `HttpGetReviews` sit behind an API Gateway HTTP API at `GET /movies`, `GET /authors` and `GET /reviews`, taking `query`, `offset` and `limit` (or `movieId` and `authorId`) from the query string. They answer `200` with the page, or `400` with problem details when the request doesn't add up. The deployed stack prints the URL as `ApiUrl`.

Direct invocations are guarded by AWS IAM, and the HTTP routes are open until you say otherwise. Want bearer tokens? Add a JWT authorizer to the HTTP API. Either way, the bouncer stands outside the door, not inside your code.

## What's inside

Three layers, loosely based on Clean Architecture. Fewer layers than an onion, more than a sandwich.

- **Presentation** is the Lambda handler. It sends each request through [Mediator](https://github.com/martinothamar/Mediator) and uses the source-generated Lambda serializer, so cold starts don't pay for reflection.
- **Application** owns the rules. Commands and queries are validated by [FluentValidation](https://github.com/FluentValidation/FluentValidation) and mapped by [Mapperly](https://mapperly.riok.app).
- **Infrastructure** owns the data, with [Entity Framework Core](https://learn.microsoft.com/ef/core) on SQLite. Everything in it is `internal`, so Presentation can wire it up but can't move in.

There's a little CQRS in how commands and queries are shaped, but reads and writes share one database underneath. CQRS-curious, not CQRS-committed.

And around the edges:

- **Tests** use [xUnit](https://xunit.net), [Shouldly](https://github.com/shouldly/shouldly) and [NSubstitute](https://github.com/nsubstitute/NSubstitute), with coverage from [Coverlet](https://github.com/coverlet-coverage/coverlet). Each test gets its own throwaway in-memory database, so no test can spoil another one's data.
- **Sample data** comes from [Bogus](https://github.com/bchavez/Bogus), and only when `Database:SeedSampleData` is on. A deployed function never starts with fake reviews.
- **Telemetry** is [OpenTelemetry](https://opentelemetry.io) all the way: a span for every invocation and use case, metrics from EF Core and the .NET runtime, and [Serilog](https://github.com/serilog/serilog) logs that know which trace they belong to. Logs always go to CloudWatch as compact JSON, and everything leaves over OTLP too when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Docker Compose runs an [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/) that forwards it all to the Aspire dashboard; in AWS, build the [ADOT](https://aws-otel.github.io/docs/getting-started/lambda) collector extension into the image or point the function at a collector gateway.
- **Build rules** live in `Directory.Build.props`. Code style and the recommended analyzers run on every build, warnings fail it, and package versions sit in one place with [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management).
- **The pipeline** builds, checks formatting, tests with a 95% coverage bar, makes sure the packed template still works, runs [CodeQL](https://codeql.github.com), reviews new dependencies and their licences, and scans the container and Dockerfile with [Trivy](https://trivy.dev). Then it sums the lot up in one comment on your pull request, so you know exactly who to blame. It needs no accounts, tokens or secrets, so your copy works the moment you push it. Trust is earned.
- **Dependabot** bundles the month's dependency updates into one pull request on the 15th.

The [architecture page](https://leanmeancleanapps.github.io/lambda/explanation/architecture/) has the long version, with full credit to [Jason Taylor's template](https://github.com/jasontaylordev/CleanArchitecture) and [Uncle Bob](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html) for the ideas.

## Releases

A new version lands on the 1st of each month, as long as something changed, with notes for every pull request in the [GitHub release](https://github.com/LeanMeanCleanApps/CleanLambdaFunction/releases). No changes, no release. We're not here to pad the download stats. Each release also carries an SPDX bill of materials listing every package the function ships with, for anyone whose security team asks nicely (or not so nicely).

```bash
dotnet new update
```

## The family

| Template | Presentation |
| --- | --- |
| [Clean Minimal API](https://github.com/LeanMeanCleanApps/CleanMinimalApi) | ASP.NET Core Minimal APIs |
| [Clean Model Context Protocol](https://github.com/LeanMeanCleanApps/CleanModelContextProtocol) | Model Context Protocol server |
| [Clean GraphQL API](https://github.com/LeanMeanCleanApps/CleanGraphQLApi) | GraphQL with Hot Chocolate |
| [Clean Web API](https://github.com/LeanMeanCleanApps/CleanWebApi) | ASP.NET Core controllers |
| **Clean Lambda Function** | AWS Lambda, you are here |

## Contributing

Bug reports, ideas and pull requests are all welcome, and typo fixes doubly so. Start with the [contributing guide](CONTRIBUTING.md) and the [code of conduct](CODE_OF_CONDUCT.md). Security problems go through the [security policy](SECURITY.md), not an issue.

## Who built this

[<img src="https://github.com/stphnwlsh.png?size=96" width="48" align="left" alt="">](https://github.com/stphnwlsh)
Built by **Stephen Walsh** in spare time, with strong coffee and a healthy disregard for pomp and ceremony. Say hello on [GitHub](https://github.com/stphnwlsh), [LinkedIn](https://www.linkedin.com/in/stphnwlsh), [Medium](https://stphnwlsh.medium.com) or at [stphnwlsh.github.io](https://stphnwlsh.github.io). 

If this template saves you some time, [sponsoring the work](https://github.com/sponsors/stphnwlsh) keeps all five templates current and the coffee flowing, which is the real dependency here.

## License

[MIT](LICENSE.md). Take it, change it, ship it. Just don't blame us for your cold starts.
