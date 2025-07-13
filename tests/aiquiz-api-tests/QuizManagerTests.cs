using aiquiz_api.Services;
using aiquiz_api.Models;
using System.Collections.Generic;
using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace aiquiz_api_tests
{
    public class QuizManagerTests
    {
        [Fact]
        public void GetQuizzesFromResponse_ParsesQuizzesCorrectly()
        {
            string response = "```json\n[\n    {\n        \"Question\": \"Who won the NBA MVP award in 2021?\",\n        \"Options\": [\"Giannis Antetokounmpo\", \"Nikola Jokić\", \"LeBron James\", \"Stephen Curry\"],\n        \"Answer\": \"Nikola Jokić\"\n    },\n    {\n        \"Question\": \"Which team has the most NBA championships?\",\n        \"Options\": [\"Los Angeles Lakers\", \"Boston Celtics\", \"Chicago Bulls\", \"Golden State Warriors\"],\n        \"Answer\": \"Boston Celtics\"\n    },\n    {\n        \"Question\": \"Who holds the record for the most points scored in a single NBA game?\",\n        \"Options\": [\"Kobe Bryant\", \"Michael Jordan\", \"Wilt Chamberlain\", \"David Robinson\"],\n        \"Answer\": \"Wilt Chamberlain\"\n    },\n    {\n        \"Question\": \"Which player is known as 'The King' in the NBA?\",\n        \"Options\": [\"Kevin Durant\", \"Kobe Bryant\", \"LeBron James\", \"Shaquille O'Neal\"],\n        \"Answer\": \"LeBron James\"\n    }\n]\n```";
            var azureOpenAISection = new Mock<IConfigurationSection>();
            azureOpenAISection.Setup(x => x["Endpoint"]).Returns("https://dummy-endpoint");
            azureOpenAISection.Setup(x => x["DeploymentName"]).Returns("dummy-deployment");
            azureOpenAISection.Setup(x => x["ApiKey"]).Returns("dummy-key");

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x.GetSection("AzureOpenAI")).Returns(azureOpenAISection.Object);
            var mockLogger = new Mock<ILogger<QuizManager>>();

            var quizManager = new QuizManager(mockConfig.Object, mockLogger.Object);
            var quizzes = quizManager.GetResponse<List<Quiz>>(response);

            Assert.Equal(4, quizzes.Count);
            Assert.Equal("Who won the NBA MVP award in 2021?", quizzes[0].Question);
            Assert.Contains("LeBron James", quizzes[0].Options);
            Assert.Equal("Nikola Jokić", quizzes[0].Answer);
        }
    }
}