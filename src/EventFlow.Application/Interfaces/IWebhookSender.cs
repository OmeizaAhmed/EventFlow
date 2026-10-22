using System;
using System.Collections.Generic;
using System.Text;
using EventFlow.Application.DTOs;
namespace EventFlow.Application.Interfaces
{
    public interface IWebhookSender
    {
        Task<WebhookSendResult> SendAsync(WebhookSendRequest request, CancellationToken ct = default);
    }

}
