using OpenAI.Chat;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using aiquiz_api.Models;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace aiquiz_api.Services
{
    public class QuizManager : IQuizManager
    {
        private readonly ChatClient _chatClient;
        private readonly ILogger<QuizManager> _logger;

        public QuizManager(IConfiguration configuration, ILogger<QuizManager> logger)
        {
            _logger = logger;
            var azureConfig = configuration.GetSection("AzureOpenAI");
            var endpointStr = Environment.GetEnvironmentVariable("AzureOpenAIEndpoint") ?? azureConfig["Endpoint"];
            var deploymentName = Environment.GetEnvironmentVariable("DeploymentName") ?? azureConfig["DeploymentName"];
            var apiKey = Environment.GetEnvironmentVariable("ApiKey") ?? azureConfig["ApiKey"];

            if (string.IsNullOrWhiteSpace(endpointStr) || string.IsNullOrWhiteSpace(deploymentName) || string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("AzureOpenAI configuration is missing required values.");
            }

            var endpoint = new Uri(endpointStr);
            var azureClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
            _chatClient = azureClient.GetChatClient(deploymentName);
        }

        public async Task<List<string>> GenerateQuizTopicsAsync(string? category = default)
        {
            var chat = string.IsNullOrEmpty(category) ? "Generate 100 topics of quizzes? Only return json serialized List of string." :
                $"enerate 100 topics of quizzes base on catory {category}? Only return json serialized List of string.";
            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new UserChatMessage(chat),
            };
            _logger.LogDebug(chat);
            return await RetryAsync(async () =>
            {
                var response = await _chatClient.CompleteChatAsync(messages);
                return GetResponse<List<string>>(response.Value.Content[0].Text);
            }, result => result.Count > 0, 3, 500, "Quiz generation");
        }

        public async Task<List<Quiz>> GenerateQuizForCategoryAsync(string category, int numQuestions)
        {
            //var topics = await GenerateQuizTopicsAsync(category);
            //var rnd = new Random();
            //topics = topics.OrderBy(x => rnd.Next()).Take(20).ToList();
            //var quiz = await GenerateQuizAsync(topics, numQuestions * 4); // generate more quizzes
            //quiz = quiz.OrderBy(x => rnd.Next()).Take(numQuestions).ToList(); // randomly take questions
            //return quiz;
            _logger.LogInformation("Generate Question");
            return await Task.Run(() => this.CreateMockData());
        }

        public async Task<List<Quiz>> GenerateQuizAsync(List<string> topics, int numQuestions)
        {
            if (numQuestions < 1) numQuestions = 4;
            var chat = $"Generate {numQuestions} quizzes that bases on the following list of topics: {string.Join(",", topics)}?, with 4 options and the correct answer, the correct answer must be one of 4 options. Only return json serialized string of object {{ Question: string,  Options: string[] Answer: string. }}, Each time I run this, the questions should be different.";
            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new UserChatMessage(chat),
            };
            _logger.LogDebug(chat);
            return await RetryAsync(async () =>
            {
                var response = await _chatClient.CompleteChatAsync(messages);
                return GetResponse<List<Quiz>>(response.Value.Content[0].Text);
            }, result => result.Count > 0, 3, 500, "Quiz generation");
        }

        // Generic retry helper for async operations
        private async Task<T> RetryAsync<T>(Func<Task<T>> action, Func<T, bool> successCondition, int maxRetries, int delayMs, string operationName)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var result = await action();
                if (successCondition(result))
                {
                    return result;
                }
                _logger.LogWarning($"{operationName} attempt {attempt + 1} did not meet success condition. Retrying...");
                await Task.Delay(delayMs);
            }
            _logger.LogError($"{operationName} failed after {maxRetries} attempts. Returning default value.");
            return default!;
        }

        public T GetResponse<T>(string response)
        {
            try
            {
                // Remove the code block formatting and any unnecessary characters
                var jsonString = response?.Replace("\n", string.Empty).Replace("        ", string.Empty).Replace("`", string.Empty).Replace("\\", string.Empty) ?? string.Empty;

                // Remove the "json" prefix if present
                if (jsonString.StartsWith("json"))
                    jsonString = jsonString.Substring(4);
                // Remove the code block formatting

                var result = JsonSerializer.Deserialize<T>(jsonString ?? "[]");
                return result ?? Activator.CreateInstance<T>();
            }
            catch (JsonException ex)
            {
                // Handle JSON parsing errors
                _logger.LogDebug($"Error parsing JSON response: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                _logger.LogDebug($"An error occurred: {ex.Message}");
            }
            return Activator.CreateInstance<T>();
        }

        private List<Quiz> CreateMockData()
        {
            return new List<Quiz>()
            {
                new Quiz() {
                    Question = "Question 1",
                    Options = new List<string>() { "a", "b", "c", "d"},
                    Answer = "a"
                },
                new Quiz() {
                    Question = "Question 2",
                    Options = new List<string>() { "a", "b", "c", "d"},
                    Answer = "a"
                },
                new Quiz() {
                    Question = "Question 3",
                    Options = new List<string>() { "a", "b", "c", "d"},
                    Answer = "a"
                },
                new Quiz() {
                    Question = "Question 4",
                    Options = new List<string>() { "a", "b", "c", "d"},
                    Answer = "a"
                }
            };
        }
        
    }
}
