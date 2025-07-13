using aiquiz_api.Models;

public interface IQuizManager
{
    Task<List<Quiz>> GenerateQuizAsync(List<string> topics, int numQuestions);
    Task<List<Quiz>> GenerateQuizForCategoryAsync(string category, int numQuestions);
    Task<List<string>> GenerateQuizTopicsAsync(string? category = default);
    T GetResponse<T>(string response);
}