using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.DTOs;
public record EventRequest(string EventType, object Payload);
public record EventInput(Guid ProjectId, string EventType, object Payload, string IdempotencyKey);



