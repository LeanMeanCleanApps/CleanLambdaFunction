---
name: lambda-emulator
description: How to try the function by hand with the Lambda runtime interface emulator in the AWS base image - docker compose, invoking with curl, choosing another function, sending an API Gateway HTTP API event, and reading errors. Use when you want to call the function as Lambda would, without deploying it.
metadata:
  scope: presentation
---

# Lambda runtime interface emulator

## The rule

The runtime image is built on the AWS Lambda .NET base image, which includes the runtime interface emulator. Running the image locally gives an endpoint that invokes the function exactly as Lambda would. `docker-compose.yml` maps it to port 9000 and turns sample data on. The image runs `GetMovies` unless you name another handler. Each function's handler string names the class Lambda Annotations generates for it, and `src/Presentation/serverless.template` lists them all.

## Steps

1. Start it: `docker compose up -d --build`.
2. Invoke it by posting the request JSON to the emulator:

   ```bash
   curl -X POST http://localhost:9000/2015-03-31/functions/function/invocations \
     -d '{ "query": "global", "offset": 0, "limit": 5 }'
   ```

3. To try another function, run the image with its handler string as the command:

   ```bash
   docker build . --target run --tag cleanlambdafunction:local
   docker run --rm -p 9000:8080 -e Database__SeedSampleData=true cleanlambdafunction:local \
     "Presentation::CleanLambdaFunction.Presentation.LambdaFunction.Handler_GetAuthors_Generated::GetAuthors"
   ```

4. An HTTP API function (`HttpGetMovies`, `HttpGetAuthors`, `HttpGetReviews`) takes the API Gateway HTTP API payload 2.0 event, not the request record. Run it by its handler string as in step 3 and post the event API Gateway would send. The query string values are strings, as they are on the wire:

   ```bash
   curl -X POST http://localhost:9000/2015-03-31/functions/function/invocations \
     -d '{ "version": "2.0", "routeKey": "GET /movies", "rawPath": "/movies", "queryStringParameters": { "query": "global", "limit": "5" }, "requestContext": { "http": { "method": "GET", "path": "/movies" } } }'
   ```

   The answer has `statusCode`, `headers` and the JSON `body` as a string. Header names come back in lower case.

5. Send a request that fails validation, such as `{ "limit": 0 }`, and check the error's `errorType` and `errorMessage`. An HTTP API function answers a `400` with `content-type: application/problem+json` instead, and `"limit": "0"` in the query string shows it.
6. Read the container's output with `docker compose logs function`. The runtime's start, invoke and error lines sit between our compact JSON events, one per line, at `Debug` because compose sets `Serilog__MinimumLevel__Default` (`serilog`).
   Each invocation's trace, metrics and logs also land in the Aspire dashboard at <http://localhost:18888>, through the collector compose starts beside the function (`telemetry`).
7. Stop it with `docker compose down`.

## Don't

- Use the emulator in place of tests. Anything you check by hand gets a test (`presentation-integration-tests`).
- Expose port 9000 beyond your machine. The emulator has no authentication.
- Deploy to try a change. The emulator runs the same image Lambda would.

## Check

``` bash
docker compose up -d --build
curl -fsS -X POST http://localhost:9000/2015-03-31/functions/function/invocations -d '{}'
docker compose down
```
