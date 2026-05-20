# Helpdesk Copilot

An AI-powered cloud support assistant for IT support teams. Automatically ingests Azure Monitor alerts, analyzes logs with KQL, searches a troubleshooting knowledge base using RAG, and creates incident tickets — all with AI-generated summaries and plain-English recommendations.

---

## Features

### Alert Ingestion & Management
- Receives Azure Monitor alerts via REST API — plug directly into Azure Monitor action groups
- Supports **7 alert types**: High CPU, Failed Requests, Slow API Response, App Service Unavailable, Database Connection Failure, Memory Spike, Login Failure Spike
- Built-in **mock alert simulator** to demo any alert type without real Azure infrastructure
- Per-alert status lifecycle: New → Acknowledged → In Progress → Resolved
- Alerts automatically trigger dashboard notifications on arrival

### AI-Powered Alert Analysis
- One-click **AI analysis** for any alert, powered by Azure OpenAI GPT-4o
- Generates plain-English explanations: what triggered the alert, what to check first, and immediate actions
- Context-aware: each alert type produces a different, targeted analysis (e.g. SQL connection failure analysis differs from CPU spike analysis)
- Analysis is persisted with the alert and reused in chat and ticket creation
- Runs fully offline with rule-based fallback when Azure OpenAI is not configured

### Log Analysis with KQL
- Fetches correlated telemetry from **Azure Log Analytics** using pre-built KQL queries
- Each alert type has a tailored KQL query (exception counts, slow requests, CPU counters, auth failures, etc.)
- Displays top error events with timestamps, sources, exception types, and occurrence counts
- Falls back to **realistic mock log data** for local development — no workspace required

### RAG Troubleshooting Knowledge Base
- **6 built-in troubleshooting articles** covering the most common Azure support scenarios:
  - App Service 500 errors
  - Database connection timeouts
  - High CPU investigation
  - Azure Function failures
  - Memory spike resolution
  - Login failure / security incidents
- Semantic search powered by **Azure AI Search** (with automatic in-memory fallback)
- Knowledge base is seeded automatically on startup — zero configuration needed
- New articles can be indexed at runtime via `POST /api/knowledge`
- Search results are injected as context into every AI chat response (grounded answers)

### AI Copilot Chat
- Conversational interface for support engineers to ask free-form questions
- **Session-based**: full message history maintained per conversation, last 10 messages sent as context
- **RAG-grounded**: relevant knowledge articles are retrieved and included in every GPT-4o prompt
- **Alert-aware**: opening chat from an alert page pre-loads that alert's details and analysis as context
- Displays **source citations** beneath each AI response so engineers know which runbook was used
- Suggests follow-up actions (e.g. "Create ticket", "Scale out App Service") based on conversation content
- Works without Azure OpenAI using an intelligent rule-based fallback that still surfaces knowledge articles

### Automated Ticket Creation
- **One-click ticket generation** from any alert — auto-populated with:
  - Severity-mapped priority (Critical alert → Critical ticket)
  - AI-generated incident summary
  - Inferred possible root cause
  - Step-by-step recommended actions
  - Link to the relevant knowledge base runbook
- Full ticket lifecycle: Open → In Progress → Resolved → Closed
- Assign tickets to engineers, update priority and status inline
- Add threaded comments to tickets for investigation notes
- Filter tickets by status or priority

### Notification System
- **Dashboard notifications** created automatically for every new alert and ticket
- Severity-based routing: Critical alerts also trigger mock **Teams** and **email** notifications
- Unread notification count displayed in the top navigation bar
- Mark individual notifications or all as read
- Ready to wire up to real Microsoft Teams webhooks or SendGrid email

### Blazor Web Dashboard
- **Dashboard** — live stats (critical alerts, active alerts, open tickets, resolved today), recent alerts and tickets with color-coded severity badges, quick-action buttons
- **Alerts page** — filterable alert list with inline AI analysis and log viewer panels, mock alert simulator with type selector
- **Tickets page** — expandable ticket cards showing AI summary, root cause, recommended actions, comments, and inline status/assignment editing
- **AI Copilot page** — full chat UI with session sidebar, message bubbles, source citations, typing indicator, and suggested action chips
- **Knowledge Base page** — searchable card grid of all troubleshooting articles with tag filtering and expandable full content

### Infrastructure & CI/CD
- Full **Bicep IaC** for one-command Azure deployment: App Service, Azure OpenAI, Azure AI Search, Azure SQL, Log Analytics, Application Insights
- **GitHub Actions pipeline**: build → test → deploy infrastructure → deploy API → deploy Blazor app
- Environment-aware sizing (Basic tier for dev, Premium for prod)
- System-assigned managed identities on all App Service resources

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Blazor Web Frontend                      │
│  Dashboard | Alerts | Tickets | AI Chat | Knowledge Base   │
└───────────────────────┬─────────────────────────────────────┘
                        │ HTTP/REST
┌───────────────────────▼─────────────────────────────────────┐
│                  ASP.NET Core Web API                       │
│                                                             │
│  AlertIngestion  LogAnalysis   RAG        CopilotChat      │
│  TicketService   Notification  Knowledge  CRUD             │
└──┬──────────┬───────────┬────────────┬────────────────────┘
   │          │           │            │
   ▼          ▼           ▼            ▼
Azure      Azure       Azure AI     Azure
Monitor    OpenAI      Search       SQL
(KQL)      (GPT-4o)    (RAG)        (Tickets)

                   Azure Functions
                   ┌─────────────────────────────┐
                   │  AlertPoller (5 min timer)  │
                   │  TicketEscalation (10 min)  │
                   └─────────────────────────────┘
```

---

## Tech Stack

| Area | Technology |
|------|-----------|
| Backend API | C# / ASP.NET Core 8 Web API |
| Frontend | Blazor Server (.NET 8) |
| AI / LLM | Azure OpenAI (GPT-4o) |
| RAG Search | Azure AI Search + in-memory fallback |
| Monitoring | Azure Monitor Query SDK |
| Telemetry | Application Insights |
| Automation | Azure Functions v4 (isolated worker) |
| Database | EF Core InMemory (dev) / Azure SQL (prod) |
| IaC | Bicep |
| CI/CD | GitHub Actions |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 / VS Code / Rider
- **Optional for full AI features:**
  - Azure OpenAI resource with GPT-4o deployment
  - Azure AI Search resource
  - Azure Monitor Log Analytics workspace

---

## Quick Start (Local — No Azure Required)

The application runs fully without Azure credentials using in-memory storage and rule-based AI responses.

```bash
# Clone the repository
git clone <repo-url>
cd azure-ai-helpdesk-copilot

# Terminal 1 — Start the API (port 5000)
cd src/HelpdeskCopilot.Api
dotnet run

# Terminal 2 — Start the Web app (port 5001)
cd src/HelpdeskCopilot.Web
dotnet run
```

Open http://localhost:5001 in your browser.

The Swagger UI is available at http://localhost:5000/swagger.

---

## Configuring Azure Services

Edit `src/HelpdeskCopilot.Api/appsettings.json` (or use environment variables / Azure App Settings):

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4o",
    "EmbeddingDeploymentName": "text-embedding-3-small"
  },
  "AzureSearch": {
    "Endpoint": "https://your-search.search.windows.net",
    "ApiKey": "your-search-key",
    "IndexName": "helpdesk-knowledge"
  },
  "LogAnalytics": {
    "WorkspaceId": "your-workspace-guid"
  }
}
```

When Azure OpenAI is configured, the AI Chat uses real GPT-4o completions. When not configured, it uses rule-based responses with knowledge base snippets — still fully functional for demos.

---

## Modules

### 1. Alert Ingestion
- `POST /api/alerts` — ingest real alerts from Azure Monitor action groups
- `POST /api/alerts/mock` — simulate any of 7 alert types for demos
- `POST /api/alerts/{id}/analyze` — trigger AI analysis of an alert

### 2. Log Analysis
- `GET /api/logs/analyze/{alertId}` — run contextual KQL for an alert
- `POST /api/logs/query` — execute arbitrary KQL
- Falls back to realistic mock data when Log Analytics is not configured

### 3. RAG Troubleshooting Assistant
- 6 pre-loaded knowledge articles (App Service errors, SQL timeouts, CPU, Functions, Memory, Security)
- `POST /api/knowledge/search` — semantic search
- Uses Azure AI Search if configured, otherwise in-memory full-text search
- Knowledge base is automatically seeded on startup

### 4. AI Copilot Chat
- `POST /api/chat/message` — send a message, get an AI response with cited sources
- Full session history maintained per conversation
- Context-aware: passes alert details and relevant knowledge docs to GPT-4o
- Works without Azure OpenAI using intelligent rule-based fallback

### 5. Ticket Management
- Auto-creates tickets from alerts with AI-generated summaries, root cause, and recommended actions
- Full CRUD: status updates, priority, assignment, comments
- `POST /api/alerts/{id}/create-ticket` — one-click ticket from alert

### 6. Notifications
- Dashboard notifications for all new alerts and tickets
- Mock Teams and email notifications logged to console
- `GET /api/notifications?unreadOnly=true`

---

## API Reference

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/alerts | List alerts (filter by status) |
| POST | /api/alerts | Ingest alert |
| POST | /api/alerts/mock | Generate mock alert |
| POST | /api/alerts/{id}/analyze | AI analysis |
| POST | /api/alerts/{id}/create-ticket | Auto-create ticket |
| GET | /api/alerts/{id}/logs | Log analysis |
| GET | /api/tickets | List tickets |
| POST | /api/tickets | Create ticket |
| PUT | /api/tickets/{id} | Update ticket |
| POST | /api/tickets/{id}/comments | Add comment |
| POST | /api/chat/message | Chat with AI |
| POST | /api/chat/sessions | New chat session |
| GET | /api/knowledge | All knowledge docs |
| POST | /api/knowledge/search | Search knowledge base |
| GET | /api/notifications | Get notifications |
| GET | /health | Health check |

Full interactive docs: http://localhost:5000/swagger

---

## Deploying to Azure

### 1. Deploy Infrastructure

```bash
az group create --name rg-helpdesk-prod --location eastus

az deployment group create \
  --resource-group rg-helpdesk-prod \
  --template-file infrastructure/main.bicep \
  --parameters environment=prod sqlAdminPassword=<secure-password>
```

### 2. GitHub Actions (CI/CD)

Set these secrets in your GitHub repository:

| Secret | Value |
|--------|-------|
| `AZURE_CREDENTIALS` | Service principal JSON (`az ad sp create-for-rbac`) |
| `AZURE_SUBSCRIPTION_ID` | Your subscription ID |
| `RESOURCE_GROUP` | `rg-helpdesk-prod` |
| `API_APP_NAME` | `helpdesk-api-prod` |
| `WEB_APP_NAME` | `helpdesk-web-prod` |
| `SQL_ADMIN_PASSWORD` | Secure SQL password |

Push to `main` to trigger full deployment.

---

## Project Structure

```
├── src/
│   ├── HelpdeskCopilot.Api/        # ASP.NET Core Web API
│   │   ├── Controllers/            # REST endpoints
│   │   ├── Services/               # Business logic + Azure SDK
│   │   ├── Models/                 # Domain models
│   │   └── Data/                   # EF Core DbContext
│   ├── HelpdeskCopilot.Web/        # Blazor Server frontend
│   │   ├── Pages/                  # Dashboard, Alerts, Tickets, Chat
│   │   ├── Shared/                 # MainLayout, NavMenu
│   │   └── Services/               # ApiClient wrapper
│   └── HelpdeskCopilot.Functions/  # Azure Functions (background)
│       ├── AlertProcessorFunction  # 5-min alert poller
│       └── TicketNotifierFunction  # 10-min escalation checker
├── infrastructure/
│   ├── main.bicep                  # Root deployment
│   └── modules/                   # App Service, OpenAI, Search, SQL
├── docs/
│   └── troubleshooting/           # RAG knowledge base source docs
└── .github/workflows/ci-cd.yml    # Build, test, deploy pipeline
```

---

## Example Scenario

1. Click **Simulate Alert** on the Dashboard → selects a random alert type
2. On the Alerts page, click **AI Analyze** → GPT-4o (or rule-based) analysis appears inline
3. Click **Create Ticket** → ticket auto-populated with AI summary, root cause, recommended actions
4. Open **AI Copilot** → context-aware chat with cited knowledge base articles
5. In Tickets, add comments, update status, and resolve

---

## Contributing

1. Fork and create a feature branch
2. Run `dotnet build HelpdeskCopilot.sln` — must pass
3. Follow existing code patterns (no magic strings, use DI, prefer records for DTOs)
4. Submit PR against `main`
