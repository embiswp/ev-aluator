using EvAluator.Domain.Entities;
using EvAluator.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EvAluator.Infrastructure.Data;

public sealed class EvAluatorDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Evaluation> Evaluations { get; set; } = null!;

    public EvAluatorDbContext(DbContextOptions<EvAluatorDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            
            entity.Property(u => u.Id)
                .HasConversion(
                    id => id.Value,
                    value => UserId.From(value))
                .ValueGeneratedNever();

            entity.Property(u => u.GoogleId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(u => u.Email)
                .HasMaxLength(320)
                .IsRequired();

            entity.Property(u => u.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(u => u.PictureUrl)
                .HasMaxLength(500);

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            entity.Property(u => u.UpdatedAt)
                .IsRequired();

            entity.Property(u => u.LastLoginAt);

            entity.Property(u => u.IsActive)
                .IsRequired();

            entity.HasIndex(u => u.GoogleId)
                .IsUnique();

            entity.HasIndex(u => u.Email);
        });

        modelBuilder.Entity<Evaluation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .HasConversion(
                    id => id.Value,
                    value => UserId.From(value))
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);

            entity.Ignore(e => e.Vehicle);
            entity.Ignore(e => e.Trips);
            entity.Ignore(e => e.Result);
        });
    }
}