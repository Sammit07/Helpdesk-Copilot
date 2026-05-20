# Troubleshooting Azure App Service 500 Errors

**Category:** App Service | **Severity:** High  
**Last Updated:** 2026-05-20

---

## Symptoms

- HTTP 5xx responses visible in Application Insights `requests` table
- Elevated `Failed Requests` metric in Azure Monitor
- Customer-facing error pages or degraded functionality
- Alerts from Azure Monitor action groups

---

## Common Causes

| # | Cause | Likelihood |
|---|-------|------------|
| 1 | Unhandled application exception in request pipeline | High |
| 2 | Database connection string misconfiguration | High |
| 3 | Missing required app settings / environment variables | Medium |
| 4 | Downstream dependency unavailability (API, queue, cache) | Medium |
| 5 | Out-of-memory condition causing process recycling | Low |
| 6 | Deployment failure leaving app in broken state | Low |

---

## Diagnostic Steps

### Step 1 — Query Application Insights for Exceptions

```kql
exceptions
| where timestamp > ago(1h)
| where cloud_RoleName has "your-app-name"
| project timestamp, type, outerMessage, problemId, operation_Name
| order by timestamp desc
| take 50
```

### Step 2 — Analyze Failed Request Rate by Endpoint

```kql
requests
| where timestamp > ago(1h)
| where success == false
| summarize failCount = count() by resultCode, name
| order by failCount desc
```

### Step 3 — Check Kudu Log Stream

Navigate to `https://<app-name>.scm.azurewebsites.net/api/logstream` for real-time logs.

### Step 4 — Azure CLI Health Check

```bash
az webapp show --name <app-name> --resource-group <rg> --query state
az monitor metrics list \
  --resource /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Web/sites/<app-name> \
  --metric Http5xx \
  --interval PT1M \
  --start-time $(date -u -d '1 hour ago' '+%Y-%m-%dT%H:%M:%SZ')
```

---

## Resolution

### If database-related (SqlException / connection timeout):
1. Verify connection string in App Service → Configuration → Application Settings
2. Test connectivity from Kudu console: `curl -v <db-host>:1433`
3. Check SQL Server firewall rules allow the App Service outbound IPs
4. See [Database Connection Timeout Guide](./database-connection-timeout.md)

### If missing configuration:
1. Compare expected app settings against `appsettings.json` required keys
2. Add missing settings: `az webapp config appsettings set --name <app> --resource-group <rg> --settings KEY=VALUE`

### If dependency failure (downstream 503/timeout):
1. Implement circuit breaker pattern (Polly library)
2. Add health checks for each dependency
3. Configure retry policies with exponential backoff

### Emergency restart:
```bash
az webapp restart --name <app-name> --resource-group <rg>
```

---

## Prevention

- Enable App Service **Always On** to prevent cold start issues
- Set up Azure Monitor alert for `Http5xx > 5% of requests`
- Implement `/health` endpoint with dependency checks
- Use deployment slots to validate before production swap
- Enable **Auto Heal** for automatic recovery on high error rates
