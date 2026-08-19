using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Polls;
using PollBuilder.Polls.Repo;
using PollBuilder.Polls.Services;
using QRCoder;

namespace PollBuilder.Polls.Controllers
{
    /// <summary>
    /// Poll lifecycle endpoints. Reached by the SPA through the gateway at <c>/api/polls/...</c>.
    /// Controllers stay thin: read HTTP, delegate to <see cref="IPollService"/>, shape the response.
    /// Business-rule failures surface as ProblemDetails via the shared exception handler.
    /// </summary>
    [ApiController]
    [Route("api/polls")]
    [Produces("application/json")]
    public class PollsController : ControllerBase
    {
        private readonly IPollService _pollService;
        private readonly ServiceOptions _serviceOptions;

        public PollsController(IPollService pollService, IOptions<ServiceOptions> serviceOptions)
        {
            _pollService = pollService;
            _serviceOptions = serviceOptions.Value;
        }

        /// <summary>Creates a poll and returns its share link plus a one-time creator token.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreatedPollResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CreatedPollResponse>> Create(
            [FromBody] CreatePollRequest request, CancellationToken cancellationToken)
        {
            var created = await _pollService.CreateAsync(request, ShareBaseUrl(), cancellationToken);
            return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
        }

        /// <summary>Fetches a poll by its share code.</summary>
        [HttpGet("{code}")]
        [ProducesResponseType(typeof(PollDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PollDto>> GetByCode(string code, CancellationToken cancellationToken) =>
            Ok(await _pollService.GetAsync(code, cancellationToken));

        /// <summary>Replaces a poll wholesale. Creator only.</summary>
        [HttpPut("{code}")]
        [ProducesResponseType(typeof(PollDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PollDto>> Replace(
            string code,
            [FromBody] UpdatePollRequest request,
            [FromHeader(Name = ServiceDefaults.CreatorTokenHeader)] string? creatorToken,
            CancellationToken cancellationToken) =>
            Ok(await _pollService.ReplaceAsync(code, request, creatorToken, cancellationToken));

        /// <summary>Edits selected fields, including closing or reopening the poll. Creator only.</summary>
        [HttpPatch("{code}")]
        [ProducesResponseType(typeof(PollDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PollDto>> Patch(
            string code,
            [FromBody] PatchPollRequest request,
            [FromHeader(Name = ServiceDefaults.CreatorTokenHeader)] string? creatorToken,
            CancellationToken cancellationToken) =>
            Ok(await _pollService.PatchAsync(code, request, creatorToken, cancellationToken));

        /// <summary>Shortcut for PATCH { "status": "Closed" }. Creator only.</summary>
        [HttpPost("{code}/close")]
        [ProducesResponseType(typeof(PollDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PollDto>> Close(
            string code,
            [FromHeader(Name = ServiceDefaults.CreatorTokenHeader)] string? creatorToken,
            CancellationToken cancellationToken) =>
            Ok(await _pollService.CloseAsync(code, creatorToken, cancellationToken));

        /// <summary>PNG QR code of the poll's share link, for projecting during a live session.</summary>
        [HttpGet("{code}/qr")]
        [Produces("image/png")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetQrCode(string code, CancellationToken cancellationToken)
        {
            // Resolve through the service so an unknown code returns 404 rather than a QR code pointing
            // at a link that goes nowhere.
            var poll = await _pollService.GetAsync(code, cancellationToken);

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(
                $"{ShareBaseUrl().TrimEnd('/')}/poll/{poll.Code}", QRCodeGenerator.ECCLevel.Q);

            return File(new PngByteQRCode(data).GetGraphic(10), "image/png");
        }

        /// <summary>
        /// Base for share links: the configured SPA origin when set, otherwise the request's own origin
        /// so local development still produces a usable link.
        /// </summary>
        private string ShareBaseUrl() =>
            string.IsNullOrWhiteSpace(_serviceOptions.ShareBaseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : _serviceOptions.ShareBaseUrl;
    }
}
