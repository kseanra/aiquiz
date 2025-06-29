using Microsoft.AspNetCore.Mvc;
using aiquiz_api.Services;
using System.Threading.Tasks;

namespace aiquiz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private readonly QuizManager _quizManager;
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
        public QuizController(QuizManager quizManager)
        {
            _quizManager = quizManager;
        }

        [HttpGet("GetQuiz")]
        public async Task<IActionResult> GetQuiz([FromQuery] string topic = "general knowledge")
        {
            var quiz = await _quizManager.GenerateQuizAsync(topic);
            return Ok(quiz);
        }
    }
}
