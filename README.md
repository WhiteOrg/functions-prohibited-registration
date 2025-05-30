# Prohibited Registration Function

Azure Function that processes prohibited user registration attempts from restricted countries. Saves attempts to PostgreSQL database and sends Slack notifications.

---

## What It Does

- **Listens** to Service Bus topic for prohibited registration events
- **Saves** registration attempts to Neon PostgreSQL database  
- **Sends** formatted notifications to Slack channel
- **Provides** HTTP endpoint for testing

---

## Prerequisites

- .NET 8.0
- Azure Functions Core Tools
- PostgreSQL database (Neon recommended)
- Slack workspace with webhook URL

---

## Setup

### 1. Clone and Install
```bash
git clone <your-repo>
cd function-prohibitedregistration
dotnet restore
