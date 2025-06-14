using Microsoft.AspNetCore.Mvc;

namespace aiquiz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        [HttpGet("GetQuiz")]
        public IActionResult GetQuiz()
        {
            // Example response, replace with your logic
            var quiz = new {
                Id = 1,
                Question = "What is the capital of France?",
                Options = new[] { "Paris", "London", "Berlin", "Madrid" },
                Answer = "Paris"
            };
            return Ok(quiz);
        }
    }
}
