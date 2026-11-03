---
name: api-conventions
description: The conventions for this Lambda function's contract - function names, one request record in and one response record out, offset paging with NextOffset, errors as Lambda function errors, and what is left to API Gateway or IAM. Use when naming a function, shaping its request or response, or deciding how a failure reaches the caller.
metadata:
  scope: presentation
---

# API conventions (Lambda function)

## The rule

| Topic | Convention |
| --- | --- |
| Names | A verb and a plural noun for lists (`GetMovies`), a verb and a singular noun for one item (`GetMovieById`) |
| Input | One request record, deserialised from the invocation payload. Optional values have defaults, so `{}` is a valid request |
| Output | One response record. Lists return `Page<T>(Items, TotalCount, NextOffset)`, written as `items`, `totalCount` and `nextOffset` |
| Paging | `offset` and `limit` on the request (`paging`). `nextOffset` is the offset of the next page, or left out on the last page |
| Search | A `query` property on the list request, not a separate function |
| Errors | Application exceptions are not caught. Lambda returns them as a function error whose `errorType` is the exception name (`ValidationException`, `NotFoundException`) and whose `errorMessage` is its message |
| Auth | None in the function. API Gateway or the caller's IAM permissions decide who can invoke it (`optional-token-auth`) |
| JSON | Property names read in any case, written in camelCase, nulls left out (`json-serialization`) |
| Nested items | A review holds its movie and its author, each without counts or averages. Callers get those from `GetMovies` and `GetAuthors` |

Callers, such as Step Functions or another service, match on `errorType` to tell bad input from a missing item and from a fault.

## Steps

1. Name the function from the table and give it its own request record unless an existing one fits exactly (`ListRequest` serves movies and authors).
2. Put every optional input on the request with a default, and document the default.
3. Return a record designed for the caller, not the Application entity.
4. Add a test for each success and for each exception the use case can throw.

## Don't

- Return an error inside a successful response, or `null` for not found. Let the exception reach Lambda.
- Wrap exceptions in a generic one. The exception name is part of the contract.
- Return an unbounded list.
- Add authentication checks to the function.

## Check

``` bash
dotnet test tests/Presentation.Tests.Integration
```
