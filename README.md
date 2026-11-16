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

The template and deployment workflow manage only the Flex Function App and
plan. The template uses:

- App: `func-cfsalerts-prod-australiasoutheast-flex`
- Plan: `plan-cfsalerts-prod-australiasoutheast-flex`
- Deployment container: `app-packages-cfsalerts-flex` in the existing storage
  account

The existing storage account, Application Insights and Log Analytics workspace
are retained. Deploy in **incremental** mode; do not use complete mode or delete
the old app/plan as part of this deployment.

### Durable state and operation

The Flex app retains the original monitor's Durable storage and verified hub
name, `funccfsalertsprodaustraliasoutheast`, so it can resume the existing
singleton and incident history instead of reposting current incidents.
The hub name is declared in `src/CfsAlerts/host.json` so the function host and
trigger synchronization use the same value when scaling from zero. Do not
replace it with an `AzureFunctionsJobHost__extensions__durableTask__hubName`
app-setting override: trigger synchronization reads the deployed `host.json`
directly. Preserve the hub name, the storage account and the host ID
(`cfsalerts-prod-flex`) on subsequent deployments. Local execution also uses
this hub name; use local development storage, not the production account.
Do not purge the history or terminate the singleton as part of deployment.

The monitor functions are enabled, and each successful code deployment invokes
`StartOrchestration`, which preserves an active singleton. No cutover environment
variable is required.

Verify new `CheckAlerts` executions from the Flex app in Application Insights,
absence of exceptions, and actual Mastodon delivery when incidents change.
Allow for the 15-minute timer and telemetry delay. A successful invocation or a
`Tooting` trace alone does not prove a post was delivered.

Any retained Consumption app must remain stopped; never run both apps against
this task hub simultaneously. If rollback is needed, prevent further workflow
deployments, then stop the Flex app and verify it has stopped before restarting
the original app. Keep the Durable state intact. Deleting the old Azure app and
plan is a separate manual action; do not delete the shared storage or monitoring
resources.

Note: You will need to adjust the names if you are trying to provision your own resources.

Create a new resource group in Azure

```bash
az group create --location australiasoutheast --resource-group rg-cfsalerts-prod-australiasoutheast
```

Create a service principal that has contributor access to the resource group

```bash
az ad sp create-for-rbac --name sp-cfsalerts-prod-australiasoutheast --role Contributor --scopes /subscriptions/<yoursubscription>/resourceGroups/rg-cfsalerts-prod-australiasoutheast --sdk-auth
```