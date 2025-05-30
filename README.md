Prohibited Registration Function
Azure Function that processes prohibited user registration attempts from restricted countries. Saves attempts to PostgreSQL database and sends Slack notifications.
What It Does

Listens to Service Bus topic for prohibited registration events
Saves registration attempts to Neon PostgreSQL database
Sends formatted notifications to Slack channel
Provides HTTP endpoint for testing
