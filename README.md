# cfsalerts-mastodon

Post toots to Mastodon from CFS RSS feed

## Infrastructure

Note: You will need to adjust the names if you are trying to provision your own resources.

Create a new resource group in Azure

```bash
az group create --location australiasoutheast --resource-group rg-cfsalerts-prod-australiasoutheast
```

Create a service principal that has contributor access to the resource group

```bash
az ad sp create-for-rbac --name sp-cfsalerts-prod-australiasoutheast --role Contributor --scopes /subscriptions/<yoursubscription>/resourceGroups/rg-cfsalerts-prod-australiasoutheast --sdk-auth
```