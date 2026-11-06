# Running Clean Lambda Function

The function is packaged as a container image based on the [AWS Lambda .NET base image](https://gallery.ecr.aws/lambda/dotnet), which includes the Lambda runtime interface emulator. That lets you invoke the function locally exactly as Lambda would, without an AWS bill to show for it.

## Database

The function stores its data in a SQLite file, `/tmp/movie-reviews.db`, because `/tmp` is the only place Lambda can write. When a container starts it applies any pending Entity Framework Core migrations. When the `Database__SeedSampleData` environment variable is `true`, it also seeds the same sample data into an empty database. `docker-compose.yml` turns it on, and it's off everywhere else, so a deployed function never starts with fake data. Nobody wants reviews from Bogus in production. Set the `ConnectionStrings__MovieReviews` environment variable to point somewhere else, or to `Data Source=MovieReviews;Mode=Memory;Cache=Shared` to keep everything in memory.

To add a migration after changing the data model:

```bash
# Once per machine
dotnet tool install --global dotnet-ef

# Then for each change
dotnet ef migrations add <Name> \
  --project ./src/Infrastructure \
  --output-dir Databases/MovieReviews/Migrations
```

## Docker

Build the image and start it.

```bash
docker build . --target run --tag cleanlambdafunction:local

docker run --rm -p 7350:8080 \
  -e Database__SeedSampleData=true \
  cleanlambdafunction:local
```

Invoke the function from another terminal.

```bash
curl -X POST http://localhost:7350/2015-03-31/functions/function/invocations \
  -d '{ "query": "global", "offset": 0, "limit": 5 }'
```

To try one of the HTTP API functions, run the image with its handler and post an API Gateway HTTP API (payload 2.0) event. The emulator doesn't pretend to be API Gateway, so you send the event it would have sent.

```bash
docker run --rm -p 7350:8080 \
  -e Database__SeedSampleData=true \
  cleanlambdafunction:local \
  "Presentation::CleanLambdaFunction.Presentation.LambdaFunction.Handler_HttpGetMovies_Generated::HttpGetMovies"

curl -X POST http://localhost:7350/2015-03-31/functions/function/invocations \
  -d '{ "version": "2.0", "routeKey": "GET /movies", "rawPath": "/movies", "queryStringParameters": { "query": "global", "limit": "5" }, "requestContext": { "http": { "method": "GET", "path": "/movies" } } }'
```

The answer is the response API Gateway turns back into HTTP: a `statusCode`, the headers, and the page as a JSON string in `body`.

## Docker Compose

`docker-compose.yml` does the same build and run in one step.

```bash
docker compose up -d --build
```

To shut it down:

```bash
docker compose down
```

## Ports

Every Lean Mean Clean template has its own block of ten ports on your machine, so this solution runs beside one made from another template without a clash. This one uses 7350 to 7359:

| Port | What listens |
| --- | --- |
| 7350 | The function, through the runtime interface emulator |
| 7352 | The Aspire dashboard |
| 7353 | The OpenTelemetry Collector, OTLP over gRPC |
| 7354 | The OpenTelemetry Collector, OTLP over HTTP |

Inside its container the emulator still listens on 8080, as Lambda's base image expects, and the dashboard and collector keep their usual ports. To move a port, change the left-hand side of its mapping in `docker-compose.yml`. 7355 to 7359 are spare for anything you add.

## Telemetry

The function always collects [OpenTelemetry](https://opentelemetry.io) traces and metrics, and exports them, along with its logs, only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Every other standard `OTEL_*` variable works too, such as `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_PROTOCOL` and `OTEL_EXPORTER_OTLP_HEADERS`. Logs always go to the console as compact JSON as well, so CloudWatch Logs keeps them whatever else you set.

Each invocation is one trace: a span for the function from the [OpenTelemetry Lambda wrapper](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AWSLambda), and one for the use case. Traces and metrics are flushed before the function returns, because Lambda can freeze the environment the moment it does.

`docker-compose.yml` also starts an [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/) and the [Aspire dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone). The function sends everything to the collector, and the collector forwards it to the dashboard at <http://localhost:7352>. The dashboard is only a container that receives OTLP; nothing in the solution depends on Aspire. The dashboard has no login and the collector accepts OTLP from anyone on ports 7353 and 7354. That's on purpose for a developer's machine, so don't run this compose file anywhere other people can reach. `otel-collector.yaml` is the collector's configuration: an OTLP receiver, the memory limiter and batch processors, and one exporter to swap for your backend.

In AWS, send the function's telemetry to a collector rather than straight to a backend. Lambda container images can't use layers, so either build the [AWS Distro for OpenTelemetry](https://aws-otel.github.io/docs/getting-started/lambda) collector extension into the image, or run a collector as a gateway and point `OTEL_EXPORTER_OTLP_ENDPOINT` at it. Either way, the collector owns batching, retries and the choice of backend, X-Ray included, and changing any of that never touches the function.

## Tests

```bash
dotnet test
```

## Deploying to AWS

`src/Presentation/serverless.template` is a CloudFormation template with a Lambda function for each handler method, all running the same container image. Lambda Annotations writes each function into it when the project builds, so a new function appears there by itself. The three `Http*` functions also get a route on the stack's API Gateway HTTP API, and the stack's `ApiUrl` output is its address. Add any other resources you need beside them.

`src/Presentation/aws-lambda-tools-defaults.json` holds the defaults for the [Amazon.Lambda.Tools](https://github.com/aws/aws-extensions-for-dotnet-cli) CLI. Fill in your profile, region and stack name, then deploy the stack. The CLI builds the image from the Dockerfile, pushes it to Amazon ECR and creates or updates every function in one go.

```bash
# Once per machine
dotnet tool install --global Amazon.Lambda.Tools

# Then each time you ship
dotnet lambda deploy-serverless --project-location ./src/Presentation
```

The tests never touch `movie-reviews.db`. Each test builds its own in-memory SQLite database from the migrations and the same fixed seed, so no test can change what another one sees and the results are the same every time.
