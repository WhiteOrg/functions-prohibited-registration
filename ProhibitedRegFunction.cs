using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Npgsql;
using System.Text;
using Azure.Messaging.ServiceBus;
using function_prohibitedregistration.Model;

namespace function_prohibitedregistration
{
    public class ProhibitedRegFunction
    {
        private readonly ILogger<ProhibitedRegFunction> _logger;
        private static readonly HttpClient httpClient = new HttpClient();

        public ProhibitedRegFunction(ILogger<ProhibitedRegFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(ProhibitedRegFunction))]
        public async Task Run(
            [ServiceBusTrigger("%ProhibitedRegisteredCountryTopicName%", "%SubscriptionName%", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("Processing prohibited registration attempt: {id}", message.MessageId);

            try
            {
                var messageBody = message.Body.ToString();
                var member = JsonSerializer.Deserialize<Member>(messageBody);

                if (member == null)
                {
                    _logger.LogWarning("Failed to deserialize message to Member object");
                    await messageActions.CompleteMessageAsync(message);
                    return;
                }

                _logger.LogInformation("Prohibited registration: {email} from {country}", 
                                     member.Email, member.CountryCode);

                // Save to database
                await SaveToDatabase(member);

                // Send Slack notification  
                await SendSlackNotification(member);

                await messageActions.CompleteMessageAsync(message);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse message as JSON");
                await messageActions.DeadLetterMessageAsync(message, 
                    propertiesToModify: new Dictionary<string, object> 
                    { 
                        ["DeadLetterReason"] = "JsonParseError",
                        ["DeadLetterErrorDescription"] = ex.Message
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing prohibited registration");
                throw;
            }
        }

        private async Task SaveToDatabase(Member member)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings:CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("CONNECTION_STRING not configured - skipping database storage");
                return;
            }

            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Create table if it doesn't exist
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS prohibited_registration_attempts (
                    id SERIAL PRIMARY KEY,
                    email VARCHAR(255),
                    username VARCHAR(255),
                    country_code VARCHAR(10),
                    company_id INTEGER,
                    detected_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    raw_data JSONB
                );";

            using var createCommand = new NpgsqlCommand(createTableSql, connection);
            await createCommand.ExecuteNonQueryAsync();

            // Insert the record
            var insertSql = @"
                INSERT INTO prohibited_registration_attempts (email, username, country_code, company_id, raw_data) 
                VALUES (@email, @username, @country, @companyId, @data);";

            using var insertCommand = new NpgsqlCommand(insertSql, connection);
            insertCommand.Parameters.AddWithValue("@email", member.Email ?? "unknown");
            insertCommand.Parameters.AddWithValue("@username", member.Username ?? "unknown");
            insertCommand.Parameters.AddWithValue("@country", member.CountryCode ?? "unknown");
            insertCommand.Parameters.AddWithValue("@companyId", member.CompanyId);
            insertCommand.Parameters.AddWithValue("@data", NpgsqlTypes.NpgsqlDbType.Jsonb, JsonSerializer.Serialize(member));

            await insertCommand.ExecuteNonQueryAsync();
        }

        private async Task SendSlackNotification(Member member)
        {
            var slackWebhookUrl = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");
            if (string.IsNullOrEmpty(slackWebhookUrl))
            {
                _logger.LogWarning("SLACK_WEBHOOK_URL not configured - skipping Slack notification");
                return;
            }

            var slackMessage = new
            {
                text = $"A user attempted to register from a prohibited country.",
                blocks = new[]
                {
                    new
                    {
                        type = "section",
                        text = new
                        {
                            type = "mrkdwn",
                            text = $"*Prohibited Registration Attempt*\n\n" +
                                   $"Email: {member.Email}\n" +
                                   $"Username: {member.Username}\n" +
                                   $"Country: {member.CountryCode}\n" +
                                   $"Company ID: {member.CompanyId}\n" +
                                   $"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(slackMessage);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(slackWebhookUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Slack API returned {response.StatusCode}: {responseContent}");
            }
        }
    }
}