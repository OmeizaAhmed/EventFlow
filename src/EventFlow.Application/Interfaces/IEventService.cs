using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Domain.Entities;
using EventFlow.Application.DTOs;

namespace EventFlow.Application.Interfaces;
public interface IEventService
{
    Task<Event> CreateEventAsync(EventInput input);
}
