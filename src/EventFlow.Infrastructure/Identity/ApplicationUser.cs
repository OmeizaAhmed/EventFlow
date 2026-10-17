using Microsoft.AspNetCore.Identity;
using System;

namespace EventFlow.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        // use base class properties for ID, email, and other identity-related fields
        public EventFlow.Domain.Entities.DomainUser? DomainUser { get; private set; }
    }
}   
