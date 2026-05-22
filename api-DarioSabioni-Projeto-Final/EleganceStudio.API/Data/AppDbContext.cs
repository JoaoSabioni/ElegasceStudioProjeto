using EleganceStudio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EleganceStudio.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Barber>     Barbers     => Set<Barber>();
    public DbSet<Service>    Services    => Set<Service>();
    public DbSet<Booking>    Bookings    => Set<Booking>();
    public DbSet<User>       Users       => Set<User>();
    public DbSet<BookingLog> BookingLogs => Set<BookingLog>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Barber>(entity =>
        {
            entity.Property(b => b.Name).IsRequired().HasMaxLength(80);
            entity.Property(b => b.Phone).IsRequired().HasMaxLength(20);
            entity.Property(b => b.Email).IsRequired().HasMaxLength(160);
            entity.HasIndex(b => b.Phone).IsUnique();
            entity.HasIndex(b => b.Email);
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.Property(s => s.Name).IsRequired().HasMaxLength(80);
            entity.Property(s => s.Price).HasPrecision(10, 2);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Services_Price_NonNegative", "\"Price\" >= 0");
                table.HasCheckConstraint("CK_Services_Duration_Positive", "\"DurationMinutes\" > 0");
            });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Username).IsRequired().HasMaxLength(64);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Role).IsRequired().HasMaxLength(20);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.ToTable(table =>
                table.HasCheckConstraint("CK_Users_Role", "\"Role\" IN ('Admin', 'Barber')"));
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(b => b.ClientName).IsRequired().HasMaxLength(100);
            entity.Property(b => b.ClientPhone).IsRequired().HasMaxLength(20);
            entity.Property(b => b.ClientEmail).IsRequired().HasMaxLength(160);
            entity.Property(b => b.Status).IsRequired().HasMaxLength(20);
            entity.HasQueryFilter(b => !b.IsDeleted);
            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_Bookings_Status",
                    $"\"Status\" IN ('{BookingStatus.Pending}', '{BookingStatus.Confirmed}', '{BookingStatus.Cancelled}')"));
        });

        modelBuilder.Entity<BookingLog>(entity =>
        {
            entity.Property(l => l.BarberName).IsRequired().HasMaxLength(80);
            entity.Property(l => l.ServiceName).IsRequired().HasMaxLength(80);
            entity.Property(l => l.ServicePrice).HasPrecision(10, 2);
            entity.Property(l => l.ClientName).IsRequired().HasMaxLength(100);
            entity.Property(l => l.ClientPhone).IsRequired().HasMaxLength(20);
            entity.Property(l => l.ClientEmail).IsRequired().HasMaxLength(160);
            entity.Property(l => l.Status).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.Property(l => l.Channel).IsRequired().HasMaxLength(20);
            entity.Property(l => l.Provider).IsRequired().HasMaxLength(40);
            entity.Property(l => l.Recipient).IsRequired().HasMaxLength(180);
            entity.Property(l => l.Subject).IsRequired().HasMaxLength(180);
            entity.Property(l => l.Status).IsRequired().HasMaxLength(20);
            entity.Property(l => l.Error).HasMaxLength(1000);
            entity.HasIndex(l => l.CreatedAt);
            entity.HasIndex(l => l.Recipient);
        });

        // ─── Seed — Barbeiros ────────────────────────────────────────────────
        modelBuilder.Entity<Barber>().HasData(
            new Barber { Id = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001"), Name = "Edi",   Phone = "+351933320269", Email = "t82704366@gmail.com" },
            new Barber { Id = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000002"), Name = "Tomas", Phone = "+351914302079", Email = "t82704366@gmail.com" },
            new Barber { Id = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000003"), Name = "Abreu", Phone = "+351913388301", Email = "t82704366@gmail.com" }
        );

        // ─── Seed — Serviços ─────────────────────────────────────────────────
        modelBuilder.Entity<Service>().HasData(
            new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000001"), Name = "Sobrancelhas",  Price = 3,  DurationMinutes = 30 },
            new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000002"), Name = "Barba",         Price = 6,  DurationMinutes = 30 },
            new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000003"), Name = "Corte Simples", Price = 10, DurationMinutes = 30 },
            new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000004"), Name = "Corte/Degradê", Price = 15, DurationMinutes = 45 },
            new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000005"), Name = "Corte & Barba", Price = 17, DurationMinutes = 60 }
        );

        // ─── Bookings — índice único de constraint ───────────────────────────
        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.BarberId, b.BookingDate, b.BookingTime })
            .HasFilter($"\"Status\" != '{BookingStatus.Cancelled}' AND \"IsDeleted\" = false")
            .IsUnique();

        // ─── Bookings — índices de performance ──────────────────────────────
        // Lookup por telefone/email e consultas de cliente
        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.ClientPhone);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.ClientEmail);

        // Dashboard do barbeiro: GET /api/bookings/barber/{id}
        // e queries de disponibilidade por data
        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.BarberId, b.BookingDate });

        // Arquivo automático à meia-noite: WHERE BookingDate < today
        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.BookingDate);

        // ─── BookingLog — índices ────────────────────────────────────────────
        modelBuilder.Entity<BookingLog>().HasIndex(l => l.BarberId);
        modelBuilder.Entity<BookingLog>().HasIndex(l => l.BookingDate);
        modelBuilder.Entity<BookingLog>().HasIndex(l => l.ArchivedAt);
    }
}
