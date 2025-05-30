# Prohibited Registration Function

Azure Function that processes prohibited user registration attempts from restricted countries. Saves attempts to Neon database for record-keeping and debugging and sends Slack notifications to a Slack channel.

---

## What It Does

- **Listens** to the Service Bus topic for prohibited registration events
- **Saves** registration attempts to the Neon PostgreSQL database  
- **Sends** formatted notifications to a Slack channel
- **Provides** a HTTP endpoint for testing

---
