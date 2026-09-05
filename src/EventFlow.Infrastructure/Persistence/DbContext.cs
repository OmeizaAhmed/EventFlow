namespace EventFlow.Infrastructure.Persistence;
    
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using EventFlow.Infrastructure.Identity;
using EventFlow.Domain.Entities;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<Endpoint> Endpoints { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<EndpointSubscription> EndpointSubscriptions { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Delivery> Deliveries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>(u => 
        {
            u.Property(x => x.FirstName).HasMaxLength(100);
            u.Property(x => x.LastName).HasMaxLength(100);
            u.Property(x => x.Email).HasMaxLength(200);
            u.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Project>(p =>
        {
            p.HasKey(x => x.ProjectId);
            p.Property(x => x.Name).HasMaxLength(200);
            p.Property(x => x.Description).HasMaxLength(1000);
            p.Property(x => x.CreatedAt).IsRequired();
            p.Property(x => x.UpdatedAt).IsRequired();
            p.Property(x => x.IsActive).IsRequired();

            p.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Endpoint>(e =>
        {
            e.HasKey(x => x.EndpointId);
            e.Property(x => x.Url).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
            e.Property(x => x.IsActive).IsRequired();

            e.HasOne<Project>()
                .WithMany(p => p.Endpoints)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(ev =>
        {
            ev.HasKey(x => x.EventId);
            ev.Property(x => x.EventType).HasMaxLength(200);
            ev.Property(x => x.CreatedAt).IsRequired();
            ev.Property(x => x.UpdatedAt).IsRequired();

            ev.HasOne<Project>()
                .WithMany(p => p.Events)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<EndpointSubscription>(es =>
        {
            es.HasKey(x => x.EndpointSubscriptionId);
            es.Property(x => x.CreatedAt).IsRequired();

            es.HasOne<Endpoint>()
                .WithMany(e => e.EndpointSubscriptions)
                .HasForeignKey(x => x.EndpointId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Delivery>(d =>
        {
            d.HasKey(x => x.DeliveryId);
            d.Property(x => x.CreatedAt).IsRequired();

            d.HasOne<Endpoint>()
                .WithMany(e => e.Deliveries)
                .HasForeignKey(x => x.EndpointId)
                .OnDelete(DeleteBehavior.Cascade);

            d.HasOne<Event>()
                .WithMany(ev => ev.Deliveries)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

            
    }
}