using System.ComponentModel.DataAnnotations;
using EventFlow.Application.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Presentation.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/deliveries")]
[Authorize(Policy = "ValidUserId")]
public sealed class DeliveriesController(DeliveryInspectionService service, ICurrentUser user) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] DeliveryQuery query, CancellationToken ct)
        => Ok(await service.ListAsync(projectId, user.UserId, query, ct));

    [HttpGet("{deliveryId}")]
    public async Task<IActionResult> Get(Guid projectId, Guid deliveryId, CancellationToken ct)
        => Ok(await service.GetAsync(projectId, deliveryId, user.UserId, ct));

    [HttpGet("{deliveryId}/attempts")]
    public async Task<IActionResult> Attempts(Guid projectId, Guid deliveryId, CancellationToken ct,
        [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 25)
        => Ok(await service.AttemptsAsync(projectId, deliveryId, user.UserId, page, pageSize, ct));

    [HttpPost("{deliveryId}/replay")]
    public async Task<IActionResult> Replay(Guid projectId, Guid deliveryId, CancellationToken ct)
    {
        var replay = await service.ReplayAsync(projectId, deliveryId, user.UserId, ct);
        return AcceptedAtAction(nameof(Get), new { projectId, deliveryId = replay.DeliveryId }, replay);
    }
}
