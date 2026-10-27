# Infrastructure & deployment

Octocat Supply runs on **Azure Container Apps**. Everything is defined in Bicep and
deployed by one GitHub Actions workflow
([`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml) at the repo root).

```
 GitHub Actions ──OIDC──> Azure
   test → provision (phase 1) → build/push images → deploy apps (phase 2) → smoke test

 ACR (no admin user) ──AcrPull via user-assigned managed identity──┐
                                                                    ▼
   ┌──────────── Container Apps environment (+ Log Analytics) ───────────┐
   │ frontend  nginx :80, public, 1-3 replicas (HTTP concurrency scale)   │
   │ api       .NET  :3000, public, exactly 1 replica, SQLite on local fs │
   └──────────────────────────────────────────────────────────────────────┘
```

## Design decisions

| Topic | Decision | Why |
| --- | --- | --- |
| Compute | Container Apps (Consumption) | Two small containers, scale-to-demand, no cluster to run. |
| API ingress | **Public** | The browser calls the API directly: the frontend's `entrypoint.sh` writes `runtime-config.js` with `API_URL` (`API_HOST`/`API_PORT`/`API_PROTOCOL`). nginx does not proxy `/api`. |
| CORS | `API_CORS_ORIGINS` = frontend origin | Locks the API to the deployed frontend. |
| Database | SQLite on the container filesystem, **ephemeral** | Simplest for a demo. Migrations and seed data run on every start, so the app is always usable; carts/orders reset on each new revision. SQLite over Azure Files (SMB) is unreliable with WAL, and several replicas would each have their own DB, so the API is pinned to **1 replica**. For durable data move to Azure SQL/PostgreSQL. |
| Registry auth | User-assigned managed identity + `AcrPull`, ACR admin user disabled | No registry secrets anywhere. |
| Secrets | `PAYMENT_API_KEY` is a Container App secret (`secretRef`), sourced from a GitHub secret | Nothing sensitive in source. Default provider is `demo` (needs no secrets). |
| Health | Startup, liveness and readiness probes on `/` | Revisions only take traffic once healthy. |
| Scale | Frontend: HTTP concurrency rule (50) | API stays at 1 replica (see Database). |
| Hostnames | `<app>.<environment default domain>` computed in Bicep | Avoids a circular reference between frontend and API settings. |

## Files

| File | Purpose |
| --- | --- |
| `main.bicep` | Resource-group scope. Identity, ACR, Log Analytics, environment, api and frontend apps. `deployApps=false` creates only the shared pieces (phase 1). |
| `modules/registry.bicep` | ACR plus `AcrPull` role assignment. |
| `modules/containerapp.bicep` | Reusable app module (identity, ingress, probes, secret, scale rules). |
| `modules/containerapp-env.bicep`, `modules/loganalytics.bicep` | Environment and logs. |
| `main.parameters.json` | Non-secret defaults. Images are passed by the workflow. |
| `../src/api-cs/Dockerfile` | API image. Build context is `src/` so `database/` (migrations + seed) is included. |
| `../scripts/smoke-test.sh` | Post-deploy check of the real cart → checkout flow. |

## One-time setup (OIDC, no stored credentials)

The pipeline signs in with a federated credential on a user-assigned managed identity.
It needs to create the `AcrPull` role assignment, so give it **Owner** on the resource group
(or Contributor + User Access Administrator).

1. Open Azure Cloud Shell (portal.azure.com → `>_`) or run `az login` locally, and select the subscription.
2. Find the resource group's location:
   `az group show -n <rg> --query location -o tsv`
3. From the repo root run (the `dev` environment must match the workflow):
   ```bash
   ./microhack-sdlc/scripts/setup-oidc.sh -g <rg> -l <location> -r <owner>/<repo> -e dev -R Owner
   ```
   It sets the secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` and the
   variables `AZURE_RESOURCE_GROUP`, `AZURE_LOCATION`. If `gh` can't write them, it prints them to add manually.
4. In GitHub: **Settings → Environments → New environment → `dev`** (add reviewers if you want an approval gate).
5. Optional Stripe: set variable `PAYMENT_PROVIDER=stripe`, variable `PAYMENT_PUBLISHABLE_KEY`, secret `PAYMENT_API_KEY`.

## Deploy

Run **Actions → Deploy to Azure → Run workflow** (it also runs on pushes to `main` that touch `microhack-sdlc/`).
The job summary and the `dev` environment link show the frontend URL.

Manual equivalent:

```bash
az deployment group create -g <rg> -f infra/main.bicep -p infra/main.parameters.json -p deployApps=false
# build + push api and frontend images to the ACR from the output, then:
az deployment group create -g <rg> -f infra/main.bicep -p infra/main.parameters.json \
  -p apiImage=<acr>.azurecr.io/api:<tag> frontendImage=<acr>.azurecr.io/frontend:<tag>
```

## Notes

- Validate locally: `az bicep build --file infra/main.bicep`.
- The first phase-2 attempt can fail while the `AcrPull` assignment propagates; the workflow retries.
- Logs: Log Analytics workspace `octocat-dev-logs`, or `az containerapp logs show -g <rg> -n octocat-dev-api`.

## Monitoring and the SRE Agent exercise

- Health: `GET /health` (liveness) and `GET /health/ready` (readiness, checks the database). Probes in `modules/containerapp.bicep` use them.
- Telemetry: Container Apps logs go to Log Analytics; the API also sends traces/requests to Application Insights (`octocat-dev-appi`) through `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- Alerts (`modules/alerts.bicep`): API 5xx responses and container restarts.
- Fault switch: run the workflow manually (Actions > Deploy to Azure > Run workflow) with `fault = bad-payment-provider`. That deploys `PAYMENT_PROVIDER=bogus-provider`: the API stays up and `/health` stays green, but every checkout returns 500.
- Remediate: ask the SRE Agent for the root cause, then re-run the workflow with `fault = none` (or roll back the revision).
- Known gap: the bad config is only detected at checkout time. Validating `PAYMENT_PROVIDER` at startup or in `/health/ready` would stop the bad revision from taking traffic.
