using Microsoft.AspNetCore.Mvc;
using aiquiz_api.Services;
using System.Threading.Tasks;

namespace aiquiz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private readonly IQuizManager _quizManager;
        /// <summary>
        /// Initializes a new instance of the <see cref="QuizController"/> class.
        /// </summary>
        /// <param name="quizManager">The quiz manager service.</param>
        /// <remarks>
        /// This constructor initializes the QuizController with a QuizManager instance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when quizManager is null.</exception>
        /// <example>
        /// <code>
        /// var quizController = new QuizController(new QuizManager());
        /// </code>
        /// </example>
        /// <returns>A new instance of the QuizController.</returns>
        /// <remarks>
        /// This controller handles requests related to quiz generation.
        /// </remarks>          
        public QuizController(IQuizManager quizManager)
        {
            _quizManager = quizManager;
        }


        /// <summary>
        /// Get Quiz
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="numQuestions"></param>
        /// <returns></returns>
        [HttpGet("GetQuiz")]
        public async Task<IActionResult> GetQuiz([FromQuery] string category = "general knowledge", [FromQuery] int numQuestions = 4)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return BadRequest("Category cannot be empty.");
            }

            if (numQuestions < 1)
            {
                numQuestions = 4; // Default to 4 questions if invalid number is provided
            }

            var quiz = await _quizManager.GenerateQuizForCategoryAsync(category, numQuestions);
            return Ok(quiz);
        }

        /// <summary>
        /// Get topic
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetTopic")]
        public async Task<IActionResult> GetTopic([FromQuery] string? category)
        {
            var topics = await _quizManager.GenerateQuizTopicsAsync(category);
            return Ok(topics);
        }
    }
}
