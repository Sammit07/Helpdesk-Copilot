# Database Connection Timeout — Resolution Guide

**Category:** Database | **Severity:** Critical  
**Last Updated:** 2026-05-20

---

## Symptoms

- `SqlException: Timeout expired. The timeout period elapsed prior to completion of the operation`
- `InvalidOperationException: Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool`
- All database-dependent requests failing or severely degraded
- Connection pool exhaustion alerts

---

## Root Causes (Most to Least Common)

1. **Connection pool exhaustion** — `DbContext` or `SqlConnection` objects not disposed properly
2. **Long-running blocking queries** — queries holding locks, preventing other connections
3. **Under-resourced Azure SQL** — insufficient DTUs/vCores for current load
4. **Network firewall blocking** — new outbound IPs not added to SQL Server firewall
5. **Credential expiration** — password-based connection string with rotated credentials
6. **Connection string targeting wrong server** — misconfigured environment settings

---

## Diagnostic Queries

### Detect connection timeout exceptions (KQL)

```kql
exceptions
| where timestamp > ago(1h)
| where type has "SqlException" or type has "TimeoutException" or outerMessage has "timeout"
| summarize count() by bin(timestamp, 5m), type, problemId
| order by timestamp desc
```

### Check request error rate correlation

```kql
requests
| where timestamp > ago(1h)
| summarize
    total = count(),
    failed = countif(success == false),
    avgDuration = avg(duration)
  by bin(timestamp, 5m)
| extend failRate = (failed * 100.0) / total
| render timechart
```

### Azure SQL blocking queries (via Query Performance Insight)

```sql
-- Run in Azure SQL Query Editor
SELECT
    r.session_id,
    r.blocking_session_id,
    r.wait_type,
    r.wait_time / 1000.0 AS wait_seconds,
    SUBSTRING(t.text, (r.statement_start_offset/2)+1,
        ((CASE r.statement_end_offset WHEN -1 THEN DATALENGTH(t.text)
          ELSE r.statement_end_offset END - r.statement_start_offset)/2)+1) AS statement_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.blocking_session_id != 0;
```

---

## Resolution Steps

### 1. Check for Connection Leaks (Most Common)

Ensure all `DbContext` usage is scoped (DI-managed):

```csharp
// BAD — never do this
var db = new MyDbContext(options);
db.Users.ToList();
// context never disposed!

// GOOD — use DI
public class MyService(MyDbContext db) { ... }
// DI disposes it at end of request scope
```

### 2. Scale Up Azure SQL

```bash
# Scale to Standard S2 (50 DTUs)
az sql db update \
  --resource-group <rg> \
  --server <server-name> \
  --name HelpdeskDb \
  --service-objective S2
```

### 3. Increase Max Pool Size in Connection String

```
Server=tcp:...;Database=...;Max Pool Size=200;Connection Timeout=30;
```

### 4. Add EF Core Retry-on-Failure

```csharp
services.AddDbContext<HelpdeskDbContext>(opt =>
    opt.UseSqlServer(connectionString, sqlOpt =>
        sqlOpt.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));
```

### 5. Check and Update SQL Firewall

```bash
# Get current App Service outbound IPs
az webapp show --name <app-name> --resource-group <rg> \
  --query "outboundIpAddresses" -o tsv

# Add firewall rule for each IP
az sql server firewall-rule create \
  --resource-group <rg> \
  --server <server-name> \
  --name AppServiceIPs \
  --start-ip-address <IP> \
  --end-ip-address <IP>
```

---

## Prevention

- Use **Azure SQL Insights** for proactive monitoring
- Set alert: `DTU consumption > 80%` → scale up automatically
- Implement **Polly** retry policies for transient faults
- Use **connection resiliency** in EF Core (always)
- Regularly review **Query Performance Insight** for slow queries
- Add missing indexes identified by **Index Advisor**
