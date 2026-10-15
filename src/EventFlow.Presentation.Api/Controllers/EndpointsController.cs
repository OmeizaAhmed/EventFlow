using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EventFlow.Application.Interfaces;
using EventFlow.Application.DTOs;

namespace EventFlow.Presentation.Api.Controllers
{
    [Route("api/projects/{projectId}/[controller]")]
    [ApiController]
    [Authorize(Policy = "ValidUser")]
    public class EndpointsController : ControllerBase
    {
        private readonly IEndpointService _endpointService;
        private readonly ICurrentUser _currentUser;
        public EndpointsController(IEndpointService endpointService, ICurrentUser currentUser)
        {
            _endpointService = endpointService;
            _currentUser = currentUser;
        }
        [HttpPost]
        public async Task<IActionResult> CreateEndpoint(Guid projectId, [FromBody] EndpointRequest input)
        {
            
            var endpointInput = new CreateEndpointInput(projectId, _currentUser.UserId, input.Url, input.Description);
            var result = await _endpointService.CreateEndpointAsync(endpointInput);
            return CreatedAtAction(nameof(GetEndpoint), new { projectId = result.ProjectId, endpointId = result.EndpointId }, result);
        }
        [HttpGet("{endpointId}")]
        public async Task<IActionResult> GetEndpoint(Guid projectId, Guid endpointId)
        {
            var result = await _endpointService.GetEndpointAsync(endpointId, projectId, _currentUser.UserId);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetEndpoints(Guid projectId)
        {
            var result = await _endpointService.GetEndpointsAsync(projectId, _currentUser.UserId);
            return Ok(result);
        }

        [HttpPut("{endpointId}")]
        public async Task<IActionResult> UpdateEndpoint(Guid projectId, Guid endpointId, [FromBody] EndpointRequest input)
        {
            var endpointInput = new UpdateEndpointInput(endpointId, projectId, _currentUser.UserId, input.Url, input.Description);
            var result = await _endpointService.UpdateEndpointAsync(endpointInput);
            return Ok(result);
        }

        [HttpDelete("{endpointId}")]
        public async Task<IActionResult> DeleteEndpoint(Guid projectId, Guid endpointId)
        {
            await _endpointService.DeleteEndpointAsync(endpointId, projectId, _currentUser.UserId);
            return NoContent();
        }

        [HttpPost("{endpointId}/activate")]
        public async Task<IActionResult> ActivateEndpoint(Guid projectId, Guid endpointId)
        {
            await _endpointService.ActivateEndpointAsync(endpointId, projectId, _currentUser.UserId);
            return NoContent();
        }

        [HttpPost("{endpointId}/deactivate")]
        public async Task<IActionResult> DeactivateEndpoint(Guid projectId, Guid endpointId)
        {
            await _endpointService.DeactivateEndpointAsync(endpointId, projectId, _currentUser.UserId);
            return NoContent();
        }
    }
    
}
