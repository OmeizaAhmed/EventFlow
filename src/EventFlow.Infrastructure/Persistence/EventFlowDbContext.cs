namespace EventFlow.Infrastructure.Persistence;
    
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Identity;
using EventFlow.Infrastructure.Persistence.Configurations;

public class EventFlowDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public EventFlowDbContext(DbContextOptions<EventFlowDbContext> options)
        : base(options)
    {
    }
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<Administrator> Administrators => Set<Administrator>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventSubscription> EventSubscriptions => Set<EventSubscription>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventFlowDbContext).Assembly);
       
    }
}