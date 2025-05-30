using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Npgsql;
using System.Text;
using function_prohibitedregistration.Model;

namespace function_prohibitedregistration
{
    public class TestRegistrationFunction /* this wont be called in prod, just purely for debugging and should adhere to the S 
                                           in the SOLID principles
                                           */
    {
        private readonly ILogger _logger;
        private static readonly HttpClient httpClient = new HttpClient();

        public TestRegistrationFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TestRegistrationFunction>();
        }

        [Function("TestRegistration")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Testing prohibited registration processing...");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                
                if (string.IsNullOrEmpty(requestBody))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Request body is empty. Send Member JSON object.");
                    return badResponse;
                }

                // Deserialize as Member (same as real function)
                var member = JsonSerializer.Deserialize<Member>(requestBody);
                
                if (member == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Failed to deserialize as Member object");
                    return badResponse;
                }

                _logger.LogInformation("Processing prohibited registration: {email} from {country}", 
                                     member.Email, member.CountryCode);

                var results = new List<string>();
                results.Add($"Email: {member.Email}");
                results.Add($"Username: {member.Username}");
                results.Add($"Country: {member.CountryCode}");
                results.Add($"Company ID: {member.CompanyId}");

                // Test database save
                try
                {
                    await SaveToDatabase(member);
                    results.Add("Database: SUCCESS - Saved to Neon");
                }
                catch (Exception dbEx)
                {
                    results.Add($"Database: FAILED - {dbEx.Message}");
                    _logger.LogError(dbEx, "Database save failed");
                }

                // Test Slack notification
                try
                {
                    await SendSlackNotification(member);
                    results.Add("Slack: SUCCESS - Notification sent");
                }
                catch (Exception slackEx)
                {
                    results.Add($"Slack: FAILED - {slackEx.Message}");
                    _logger.LogError(slackEx, "Slack notification failed");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync(string.Join("\n", results));
                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing failed");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"JSON parsing failed: {ex.Message}");
                return errorResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test failed");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Test failed: {ex.Message}");
                return errorResponse;
            }
        }

        private async Task SaveToDatabase(Member member)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings:NEON_DB_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("NEON_DB_CONNECTION_STRING not configured");
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
                throw new InvalidOperationException("SLACK_WEBHOOK_URL not configured");
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