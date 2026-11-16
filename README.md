# cfsalerts-mastodon

Post toots to Mastodon from the CFS current incidents JSON feed

## Orchestration

The function-key-protected `StartOrchestration` endpoint starts the monitor if
it is not already active. Deployments call this endpoint and fail if it cannot
reach Durable Functions. `GetOrchestrationStatus` lists instances and
`StopOrchestrations` terminates them.

## Infrastructure

The Bicep template targets Linux Flex Consumption (`FC1`) with .NET 8 isolated,
512 MB instances, a maximum of 40 instances and no always-ready instances.
Code is deployed through the Functions action using OneDeploy and a private
blob container, rather than Azure Files or `WEBSITE_RUN_FROM_PACKAGE`.
Existing storage connection-string authentication is retained.
The application stays on .NET 8 by choice for this migration; its published
support end date was 10 November 2026, so plan a separate runtime upgrade.

The 512 MB size reduces memory-based compute charges by 75% compared with
2048 MB if execution duration is unchanged. It also provides less CPU and
memory, so validate cold starts, execution duration and memory usage after
deployment. Increase the size if the app experiences memory pressure or
unacceptable latency. Actual bill savings depend on usage and free allowances;
storage and telemetry charges are separate. The 40-instance limit is a scaling
ceiling, not reserved capacity.

Flex requires a new Function App and plan; it cannot convert the deployed
Consumption app in place. The template uses:

- App: `func-cfsalerts-prod-australiasoutheast-flex`
- Plan: `plan-cfsalerts-prod-australiasoutheast-flex`
- Deployment container: `app-packages-cfsalerts-flex` in the existing storage
  account

The existing storage account, Application Insights and Log Analytics workspace
are retained. Deploy in **incremental** mode; do not use complete mode or delete
the old app/plan as part of this deployment.

### Safe cutover

The new app shares the original monitor's Durable storage so that it can resume
the existing singleton and its incident history, instead of reposting current
incidents. `durableTaskHubName` explicitly preserves the hub name derived from
the original app name. **Verify this value against the source app's Durable
configuration/storage before enabling the new app.** Do not change the hub,
purge its history or terminate the singleton during cutover.
The Flex app has a separate host ID (`cfsalerts-prod-flex`) to avoid collisions
with the original app's automatically truncated host ID in the shared storage.

`monitorEnabled` defaults to `false`. The orchestration, activity, start and stop
functions are disabled on the Flex app until cutover; the status endpoint
remains available to inspect the shared state. Disabled functions can still be
invoked with a host master key; do not use Test/Run to bypass this safeguard.
The deployment workflow passes the `dev` environment variable
`FLEX_MONITOR_ENABLED` (unset means `false`) to Bicep and skips startup while
disabled.

When ready to deploy and cut over manually:

1. Leave `FLEX_MONITOR_ENABLED` unset or `false`, deploy the infrastructure and
   code, and verify that the Flex status endpoint sees the existing singleton.
   A Running status here is shared storage state, not proof Flex is executing.
2. Wait until `CheckAlerts` has completed and the original orchestration is
   waiting on its 15-minute timer. Stop the original Function App, **not** the
   orchestration, and verify it has stopped. Never run both apps against this
   task hub simultaneously.
3. Set `FLEX_MONITOR_ENABLED=true` in the `dev` GitHub environment and rerun the
   workflow. The triggers resume the existing Durable instance; the startup
   endpoint preserves it. Keep this variable true for subsequent deployments.
4. Verify new `CheckAlerts` executions from the Flex app in Application Insights,
   absence of exceptions, and actual Mastodon delivery when incidents change.
   Allow for the timer and telemetry delay. A successful invocation or a
   `Tooting` trace alone does not prove a post was delivered.

For rollback, stop the Flex app and verify it has stopped **before** restarting
the original app. Keep the Durable state intact and set
`FLEX_MONITOR_ENABLED=false` before any further workflow deployment. Retain the
original app and plan until cutover is validated; cleanup is a separate manual
decision.

Note: You will need to adjust the names if you are trying to provision your own resources.

Create a new resource group in Azure

```bash
az group create --location australiasoutheast --resource-group rg-cfsalerts-prod-australiasoutheast
```

Create a service principal that has contributor access to the resource group

```bash
az ad sp create-for-rbac --name sp-cfsalerts-prod-australiasoutheast --role Contributor --scopes /subscriptions/<yoursubscription>/resourceGroups/rg-cfsalerts-prod-australiasoutheast --sdk-auth
```