# Azure Function Failure Runbook

**Category:** Azure Functions | **Severity:** High  
**Last Updated:** 2026-05-20

---

## Failure Mode Index

| Failure | Symptom | Quick Fix |
|---------|---------|-----------|
| Cold Start | First invocation 10–30s | Premium Plan / Always Ready |
| Timeout | `FunctionInvocationException: Timeout` | Increase `functionTimeout` |
| DI Error | `InvalidOperationException` on startup | Check `Program.cs` registrations |
| Storage Error | Function app won't start | Verify `AzureWebJobsStorage` |
| Orchestration Stuck | Durable function not completing | Terminate via HTTP API |
| Scale Issue | Throughput lower than expected | Check host.json concurrency |

---

## Diagnostic KQL Queries

### All function failures in last hour

```kql
traces
| where timestamp > ago(1h)
| where severityLevel >= 3
| where customDimensions.Category has "Function"
| project timestamp, message, severityLevel, customDimensions
| order by timestamp desc
```

### Function invocation summary

```kql
customMetrics
| where timestamp > ago(1h)
| where name in ("Function Failures", "Function Duration")
| summarize
    totalFailures = sumif(value, name == "Function Failures"),
    avgDuration = avgif(value, name == "Function Duration")
  by bin(timestamp, 5m)
| render timechart
```

### Cold start detection

```kql
traces
| where timestamp > ago(24h)
| where message has "Host initialized" or message has "cold start"
| summarize count() by bin(timestamp, 1h)
| render barchart
```

---

## Failure Mode Details

### 1. Cold Start Latency

**Cause:** Consumption Plan spins down after 5 minutes of inactivity.

**Solutions (in order of preference):**

```json
// host.json — for Premium Plan: configure pre-warmed instances
{
  "functionTimeout": "00:10:00",
  "extensions": {
    "http": {
      "routePrefix": "api"
    }
  }
}
```

```bash
# Upgrade to Premium Plan with always-ready instances
az functionapp plan create \
  --resource-group <rg> \
  --name <plan-name> \
  --location eastus \
  --sku EP1 \
  --min-instances 1 \
  --max-burst 10 \
  --is-linux

az functionapp update \
  --resource-group <rg> \
  --name <function-app> \
  --plan <plan-name>
```

### 2. Function Timeout

**Cause:** Default timeout is 5 minutes (Consumption) or 30 minutes (Premium/Dedicated).

```json
// host.json
{
  "version": "2.0",
  "functionTimeout": "00:10:00"
}
```

For long-running work, refactor to **Durable Functions**:

```csharp
[Function("LongRunningOrchestrator")]
public async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
{
    await context.CallActivityAsync("Step1");
    await context.CallActivityAsync("Step2");
    return "Done";
}
```

### 3. Dependency Injection Failures

Check `Program.cs` for missing registrations:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddHttpClient();           // ← ensure this is registered
        services.AddScoped<IMyService, MyService>();
    })
    .Build();
```

**Diagnostic:** Check startup logs in Azure Portal → Function App → **Log stream**.

### 4. Storage Account Issues

```bash
# Verify AzureWebJobsStorage is set
az functionapp config appsettings list \
  --name <function-app> \
  --resource-group <rg> \
  --query "[?name=='AzureWebJobsStorage']"

# Check storage account accessibility
az storage account show \
  --name <storage-account> \
  --resource-group <rg> \
  --query "primaryEndpoints"
```

### 5. Durable Function Orchestration Stuck

```bash
# List running instances
GET https://<function-app>.azurewebsites.net/runtime/webhooks/durabletask/instances?runtimeStatus=Running

# Terminate stuck orchestration
POST https://<function-app>.azurewebsites.net/runtime/webhooks/durabletask/instances/<instanceId>/terminate?reason=StuckOrchestration
```

---

## Monitoring Setup

Enable Application Insights for all functions:

```csharp
services.AddApplicationInsightsTelemetryWorkerService();
services.ConfigureFunctionsApplicationInsights();
```

Set up alert for function failures:

```bash
az monitor metrics alert create \
  --name "FunctionFailureAlert" \
  --resource-group <rg> \
  --scopes <function-app-resource-id> \
  --condition "count FunctionExecutionUnits > 0" \
  --description "Azure Function execution failures detected"
```

---

## Escalation Path

1. **0–15 min:** Check Application Insights Live Metrics and Log Stream
2. **15–30 min:** Review invocation logs in Azure Portal → Function App → Monitor → Invocations
3. **30–60 min:** Collect full diagnostic logs, restart Function App
4. **60+ min:** Open Azure Support case with correlation IDs from Application Insights
