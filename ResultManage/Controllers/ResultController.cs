using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ResultManage.Hubs;
using ResultManage.Models;
using ResultManage.Services;

namespace ResultManage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResultController : ControllerBase
    {
        private readonly ResultService _resultService;
        private readonly IHubContext<PollHub> _hubContext;
        private readonly ILogger<ResultController> _logger;

        public ResultController(
            ResultService resultService,
            IHubContext<PollHub> hubContext,
            ILogger<ResultController> logger)
        {
            _resultService = resultService;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<PollResultDto>> Get(string code)
        {
            var results = await _resultService.GetResultsAsync(code);
            if (results == null)
            {
                return NotFound();
            }

            return Ok(results);
        }

        // Called by VoteManage after it saves a vote. Rebuilds the results and pushes
        // them to every browser watching this poll.
        [HttpPost("{code}/broadcast")]
        public async Task<ActionResult<PollResultDto>> Broadcast(string code)
        {
            var results = await _resultService.GetResultsAsync(code);
            if (results == null)
            {
                return NotFound();
            }

            await _hubContext.Clients.Group(PollHub.GroupFor(code)).SendAsync("ResultsUpdated", results);
            _logger.LogInformation("pushed new results for poll {Code}", code);

            return Ok(results);
        }

        // Download the results as a CSV file, for opening in Excel.
        [HttpGet("{code}/csv")]
        public async Task<IActionResult> GetCsv(string code)
        {
            var results = await _resultService.GetResultsAsync(code);
            if (results == null)
            {
                return NotFound();
            }

            var csv = new StringBuilder();
            csv.AppendLine("option_index,option_text,votes,percentage");
            foreach (var option in results.Options)
            {
                var text = option.Text.Contains(',') ? $"\"{option.Text}\"" : option.Text;
                csv.AppendLine($"{option.Index},{text},{option.Votes},{option.Percentage}");
            }

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"poll-{code}-results.csv");
        }
    }
}
