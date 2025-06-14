namespace aiquiz_api.Models
{
    public class Quiz
    {
        public int Id { get; set; }
        public required string Question { get; set; }
        public required List<string> Options { get; set; }
        public required string Answer { get; set; }
    }
}
