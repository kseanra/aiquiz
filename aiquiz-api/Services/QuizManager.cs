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

        public QuizManager(IConfiguration configuration)
        {
            var azureConfig = configuration.GetSection("AzureOpenAI");
            var endpointStr =  Environment.GetEnvironmentVariable("AzureOpenAIEndpoint") ??  azureConfig["Endpoint"] ;
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

        public async Task<List<Quiz>> GenerateQuizAsync(string topic, int numQuestions)
        {
            if (numQuestions < 1) numQuestions = 4;
            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new UserChatMessage($"Generate {numQuestions} quizs about {topic}?, with 4 options and the correct answer. Only return json serialized string of object {{ Question: string,  Options: string[] Answer: string. }}, Each time I run this, the questions should be different."),
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            return GetQuizzesFromResponse(response.Value.Content[0].Text);
        }

        public List<Quiz> GetQuizzesFromResponse(string response)
        {
            try
            {
                // Remove the code block formatting and any unnecessary characters
                var jsonString = response?.Replace("\n", string.Empty).Replace("        ", string.Empty).Replace("`", string.Empty).Replace("\\", string.Empty) ?? string.Empty;

                // Remove the "json" prefix if present
                if (jsonString.StartsWith("json"))
                    jsonString = jsonString.Substring(4);
                // Remove the code block formatting

                var quizzes = JsonSerializer.Deserialize<List<Quiz>>(jsonString ?? "[]") ?? new List<Quiz>();
                return quizzes;
            }
            catch (JsonException ex)
            {
                // Handle JSON parsing errors
                Console.WriteLine($"Error parsing JSON response: {ex.Message}");
                return new List<Quiz>();
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                Console.WriteLine($"An error occurred: {ex.Message}");
                return new List<Quiz>();
            }
        }
    }
}
