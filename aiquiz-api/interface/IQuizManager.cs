using aiquiz_api.Models;

public interface IQuizManager
{
    Task<List<Quiz>> GenerateQuizAsync(string topic, int numQuestions);
    List<Quiz> GetQuizzesFromResponse(string response);
}