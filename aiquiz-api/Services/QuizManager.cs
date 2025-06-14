using OpenAI.Chat;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using aiquiz_api.Models;
using System.Text.RegularExpressions;

namespace aiquiz_api.Services
{
    public class QuizManager
    {
        private readonly ChatClient _chatClient;

        public QuizManager(IConfiguration configuration)
        {
            var azureConfig = configuration.GetSection("AzureOpenAI");
            var endpointStr = azureConfig["Endpoint"] ?? string.Empty;
            var deploymentName = azureConfig["DeploymentName"] ?? string.Empty;
            var apiKey = azureConfig["ApiKey"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(endpointStr) || string.IsNullOrWhiteSpace(deploymentName) || string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("AzureOpenAI configuration is missing required values.");
            }

            var endpoint = new Uri(endpointStr);
            var azureClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
            _chatClient = azureClient.GetChatClient(deploymentName);
        }

        public async Task<string> GenerateQuizAsync(string topic)
        {
            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new UserChatMessage($"Generate 4 quizs about {topic}?, with 4 options and the correct answer. The quiz should be in the following format: Question: [question text] Options: [option1, option2, option3, option4] Answer: [correct answer]."),
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            System.Console.WriteLine(response.Value.Content[0].Text);
            // Append the model response to the chat history.
            messages.Add(new AssistantChatMessage(response.Value.Content[0].Text));
            return response.Value.Content[0].Text;
        }

        public List<Quiz> GetQuizzesFromResponse(string response)
        {
            var quizzes = new List<Quiz>();
            var quizBlocks = Regex.Split(response, @"### Quiz \\d+").Where(q => !string.IsNullOrWhiteSpace(q));
            int id = 1;
            foreach (var block in quizBlocks)
            {
                var questionMatch = Regex.Match(block, @"Question: (.*?)  ");
                var optionsMatch = Regex.Match(block, @"Options: \[(.*?)\]  ");
                var answerMatch = Regex.Match(block, @"Answer: (.*?)  ");
                if (questionMatch.Success && optionsMatch.Success && answerMatch.Success)
                {
                    var options = optionsMatch.Groups[1].Value.Split(',').Select(o => o.Trim()).ToList();
                    quizzes.Add(new Quiz
                    {
                        Id = id++,
                        Question = questionMatch.Groups[1].Value.Trim(),
                        Options = options,
                        Answer = answerMatch.Groups[1].Value.Trim()
                    });
                }
            }
            return quizzes;
        }
    }
}
