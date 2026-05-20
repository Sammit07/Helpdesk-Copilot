# High CPU Investigation Checklist

**Category:** Performance | **Severity:** High  
**Last Updated:** 2026-05-20

---

## Triage Checklist

- [ ] Confirm CPU is genuinely high (not alert misconfiguration)
- [ ] Identify which instance(s) are affected (scale-set or single)
- [ ] Correlate spike with recent deployment timeline
- [ ] Check if traffic volume increased proportionally
- [ ] Determine if CPU is user-mode or kernel-mode

---

## Diagnostic Commands

### Get CPU metrics via Azure CLI

```bash
az monitor metrics list \
  --resource /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Web/sites/<app-name> \
  --metric CpuPercentage \
  --interval PT1M \
  --start-time $(date -u -d '2 hours ago' '+%Y-%m-%dT%H:%M:%SZ') \
  --output table
```

### KQL — CPU correlated with request volume

```kql
performanceCounters
| where name == "% Processor Time"
| where timestamp > ago(2h)
| summarize avgCpu = avg(value) by bin(timestamp, 5m), cloud_RoleInstance
| join kind=inner (
    requests
    | where timestamp > ago(2h)
    | summarize reqCount = count() by bin(timestamp, 5m)
) on timestamp
| project timestamp, avgCpu, reqCount
| render timechart
```

### KQL — Long-running operations

```kql
requests
| where timestamp > ago(1h)
| where duration > 5000
| summarize
    count(),
    avgDuration = avg(duration),
    p95Duration = percentile(duration, 95)
  by name, operation_Name
| order by avgDuration desc
| take 20
```

### KQL — CPU-heavy dependency calls

```kql
dependencies
| where timestamp > ago(1h)
| where duration > 2000
| summarize count(), avg(duration) by name, type, target
| order by avg_duration desc
```

---

## Common Causes & Fixes

### 1. Traffic Increase (Legitimate Load)

**Fix:** Scale out horizontally

```bash
az appservice plan update \
  --name <plan-name> \
  --resource-group <rg> \
  --number-of-workers 3
```

Or enable **autoscale**:

```bash
az monitor autoscale create \
  --resource-group <rg> \
  --resource <plan-id> \
  --resource-type Microsoft.Web/serverfarms \
  --name autoscale-cpu \
  --min-count 1 --max-count 5 --count 1
```

### 2. Inefficient Database Queries (N+1 Problem)

Enable EF Core query logging to spot N+1:

```csharp
optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information)
              .EnableSensitiveDataLogging();
```

**Fix:** Use `.Include()` for eager loading, or raw SQL for complex queries.

### 3. Background Job Runaway

Check Kudu Process Explorer or Application Insights:

```kql
traces
| where timestamp > ago(1h)
| where message has "BackgroundService" or message has "HostedService"
| order by timestamp desc
```

**Fix:** Add `CancellationToken` support and timeout limits to all background jobs.

### 4. CPU Profiling with Application Insights

1. Azure Portal → App Service → **Diagnose and solve problems**
2. Select **Performance** → **CPU** → **Collect .NET Profiler Trace**
3. Download and analyze the `.diagsession` file
4. Review hot paths in the flame graph

### 5. Memory Pressure Causing GC CPU Spike

High GC CPU (seen as `System.GC` in profiler):

```kql
performanceCounters
| where name has "GC" or name has "Heap"
| where timestamp > ago(1h)
| summarize avg(value) by name, bin(timestamp, 5m)
| render timechart
```

**Fix:** Review large object allocations, implement object pooling (`ArrayPool<T>`, `MemoryPool<T>`).

---

## Profiling Tools Reference

| Tool | When to Use |
|------|-------------|
| Application Insights Profiler | Production CPU profiling, flame graphs |
| dotnet-trace | Detailed .NET runtime events |
| PerfView | Deep GC and JIT analysis |
| Kudu Process Explorer | Live process CPU/memory view |
| VS Diagnostic Tools | Local development profiling |

---

## Escalation

If CPU remains high after scaling and no code issue is found:
1. File Azure support ticket with subscription ID and resource details
2. Capture Profiler trace and attach to ticket
3. Consider temporary migration to higher-tier SKU (P2v3/P3v3)
