# Repository instructions

Keep repository-wide agent instructions in this `AGENTS.md` file. Do not
create a separate `.github/copilot-instructions.md`.

## Build and test

- The solution is `src/CfsAlerts.sln`; the application targets .NET 8 and Azure
  Functions v4 with the isolated worker. From the repository root, build with
  `dotnet build src/CfsAlerts.sln`. The deployment workflow builds the
  application in Release: `dotnet build src/CfsAlerts/CfsAlerts.csproj --configuration Release`.
- Tests use xUnit, NSubstitute and Verify. Run tests in Debug: Release builds
  execute real Mastodon publishing calls. The test project currently lacks an
  xUnit VSTest adapter, so `dotnet test` can report success while discovering
  zero tests. Verify the executed test count, including for filtered runs.
- With `xunit.runner.visualstudio` 2.8.2 already in the local NuGet cache, the
  following runs the suite from an isolated output directory. Change the
  adapter path to a suitable installed version if necessary:

  ```sh
  dotnet build src/CfsAlerts.Tests/CfsAlerts.Tests.csproj
  TEST_DIR=$(mktemp -d)
  cp -R src/CfsAlerts.Tests/bin/Debug/net8.0/. "$TEST_DIR/"
  cp "$HOME/.nuget/packages/xunit.runner.visualstudio/2.8.2/build/net6.0/"*.dll "$TEST_DIR/"
  dotnet vstest "$TEST_DIR/CfsAlerts.Tests.dll"
  ```

  To run one test instead of the full suite:

  ```sh
  dotnet vstest "$TEST_DIR/CfsAlerts.Tests.dll" --TestCaseFilter:'FullyQualifiedName=CfsAlerts.Tests.StartOrchestrationTests.Run_WhenClientFails_PropagatesFailure'
  ```

  `CfsFunctionTests.Test1` uses the embedded `newList.json` fixture and a
  checked-in Verify snapshot; the snapshot currently differs from the
  description format produced by the parser.
- The GitHub Actions workflow builds and performs a Bicep what-if on pull
  requests; it does not run the test suite. On `main` it deploys the Bicep and
  Release app, then invokes `StartOrchestration`.

## Architecture

- `StartOrchestration` starts or preserves a fixed singleton Durable Functions
  instance. `MonitorJobStatus` calls `CheckAlerts`, waits on a durable timer,
  and uses `ContinueAsNew` to carry the latest incident list into the next
  iteration. The interval is 15 minutes in Release and 20 seconds in Debug.
- `CheckAlerts` reads the CFS current-incidents JSON feed, maps its nested
  fields (with legacy flat-field fallbacks), and publishes changed items via
  Mastonet as unlisted posts. Debug logs intended posts without publishing.
  The feed is not filtered by agency.
- `CfsFeedItem` equality uses only `Id`, which is built from incident number,
  title, description and link. This makes changes to incident details new
  posts; preserve that behavior when changing parsing or identity.
- The three function-key-protected HTTP endpoints start, list and stop
  orchestrations. Use the Functions `[DurableClient]` parameter binding for
  client operations rather than registering a standalone gRPC client.
  `StartOrchestration` avoids replacing active instances and propagates
  scheduling failures.

## Configuration and deployment

- `Program.cs` binds `MastodonSettings` from configuration and loads .NET user
  secrets. Azure uses `MastodonSettings__instance` and
  `MastodonSettings__token`; the token is supplied through GitHub Actions
  secrets. Local Functions execution also needs `AzureWebJobsStorage` and
  `FUNCTIONS_WORKER_RUNTIME` configured.
- `bicep/main.bicep` provisions the Linux Function App, storage, hosting plan
  and Application Insights. Resource names are specific to this deployment;
  adjust them before provisioning another environment.
- When diagnosing missing posts, compare the last `CheckAlerts` run and the
  15-minute Release timer with the deployment time before concluding the
  orchestration stopped; check its status and allow for telemetry delay.
- Inspect application traces, warnings and exceptions as well as request
  status. `CheckAlerts` can return successfully after skipping incidents or
  catching an error. Its `Tooting` message is logged before publishing and
  does not by itself confirm delivery. Previous versions of
  `StartOrchestration` could also return HTTP 200 after a gRPC failure.
