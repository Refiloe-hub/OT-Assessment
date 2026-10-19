using Microsoft.AspNetCore.Mvc;
using OT.Assessment.App.Data;
using OT.Assessment.App.Messaging;
using OT.Assessment.App.Models;

namespace OT.Assessment.App.Controllers;

[ApiController]
[Route("api/player")]
public class PlayerController : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int MaxTopSpenders = 1000;

    private readonly IWagerPublisher _publisher;
    private readonly IPlayerQueryRepository _queries;

    public PlayerController(IWagerPublisher publisher, IPlayerQueryRepository queries)
    {
        _publisher = publisher;
        _queries = queries;
    }

    // POST api/player/casinowager
    [HttpPost("casinowager")]
    public async Task<IActionResult> Casinowager([FromBody] CasinoWagerRequest request, CancellationToken cancellationToken)
    {
        await _publisher.PublishAsync(request, cancellationToken);
        return Ok();
    }

    // GET api/player/{playerId}/casino?pageSize=10&page=1
    [HttpGet("{playerId:guid}/casino")]
    public async Task<ActionResult<PagedResponse<WagerResponse>>> GetPlayerWagers(
        Guid playerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            return BadRequest($"page must be >= 1 and pageSize must be between 1 and {MaxPageSize}.");

        return Ok(await _queries.GetPlayerWagersAsync(playerId, page, pageSize, cancellationToken));
    }

    // GET api/player/topSpenders?count=10
    [HttpGet("topSpenders")]
    public async Task<ActionResult<IReadOnlyList<TopSpenderResponse>>> GetTopSpenders(
        [FromQuery] int count = 10, CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > MaxTopSpenders)
            return BadRequest($"count must be between 1 and {MaxTopSpenders}.");

        return Ok(await _queries.GetTopSpendersAsync(count, cancellationToken));
    }
}