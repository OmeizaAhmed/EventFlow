using EventFlow.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;

namespace EventFlow.Presentation.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/[controller]")]
public class EventsController: ControllerBase
{
    private readonly IEventService _eventService;
    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme, Policy = ApiKeyDefaults.LiveKeyPolicy)]
    [HttpPost]
    public async Task<IActionResult> CreateEvent(Guid projectId, [FromBody] EventRequest eventRequest)
    {
        // get Idempotency-Key from header
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return BadRequest("Idempotency-Key header is required.");
        }
        var input = new EventInput(projectId, eventRequest.EventType, eventRequest.Payload, idempotencyKey);
        var newEvent = await _eventService.CreateEventAsync(input);
        // return accepted
        return Accepted();
    }

    
}
