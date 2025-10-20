using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ValidatorService.Dtos;
using ValidatorService.Services;

namespace ValidatorService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ValidatorController : ControllerBase
    {
        private readonly GrammarValidatorService _validatorService;
        private readonly ILogger<ValidatorController> _logger;

        // inyectar el servicio
        public ValidatorController(ILogger<ValidatorController> logger, GrammarValidatorService validatorService)
        {
            _logger = logger;
            _validatorService = validatorService;
        }

        [HttpGet("message")]
        public ActionResult<string> Get()
        {
            var message = _validatorService.GetMessage();
            return Ok(message);
        }

        [HttpGet("validate/{id}")]
        public async Task<IActionResult> Generate(string id, [FromQuery] string word)
        {
            var grammar = await _validatorService.GetGrammarByIdAsync(id);
            if (grammar == null)
                return NotFound("No se encontró la gramática.");

            var generator = new GrammarGeneratorService(grammar);
            var words = generator.GenerateWords();

            bool belongs = generator.WordBelongs(word);

            return Ok(new
            {
                grammar.Id,
                grammar.StartSymbol,
                generatedCount = words.Count,
                words,
                testWord = word,
                belongs
            });
        }
    }
}
