using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.DTOs
{
    public sealed record GeneratedApiKey(
    string PlainTextKey,   // show to the user ONCE, never store
    string KeyPrefix,      // safe to store + display, e.g. "ef_live_8a2f1c3d"
    string HashedKey);
}
