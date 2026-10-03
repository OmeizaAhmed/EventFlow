using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.DTOs;
public record AuthOutput(string AccessToken, string RefreshToken);
