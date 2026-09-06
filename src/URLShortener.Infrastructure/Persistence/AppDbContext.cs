using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using URLShortener.Domain.Entities;

namespace URLShortener.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Url> Urls => Set<Url>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UrlDailyStat> UrlDailyStats => Set<UrlDailyStat>();
    public DbSet<UrlCountryDailyStat> UrlCountryDailyStats => Set<UrlCountryDailyStat>();
    public DbSet<UrlDeviceDailyStat> UrlDeviceDailyStats => Set<UrlDeviceDailyStat>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Url>(entity =>
        {
            entity.ToTable("Urls");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.ShortCode)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(u => u.OriginalUrl)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(u => u.CreatedByIp)
                .HasMaxLength(45);

            // Unique index on ShortCode (case-sensitive via collation)
            entity.HasIndex(u => u.ShortCode)
                .IsUnique()
                .HasDatabaseName("IX_Urls_ShortCode");

            // Index for user URL listing
            entity.HasIndex(u => new { u.UserId, u.CreatedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_Urls_UserId_CreatedAt");

            // Index for expiration processing
            entity.HasIndex(u => u.ExpiresAt)
                .HasDatabaseName("IX_Urls_ExpiresAt");

            entity.HasOne(u => u.User)
                .WithMany(u => u.Urls)
                .HasForeignKey(u => u.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Analytics aggregates (Phase 7): composite keys mirror the MERGE upsert shape
        // used by the aggregation worker, so (UrlId, Date[, dimension]) lookups are index-only.
        builder.Entity<UrlDailyStat>(entity =>
        {
            entity.ToTable("UrlDailyStats");
            entity.HasKey(s => new { s.UrlId, s.Date });
            entity.Property(s => s.Date).HasColumnType("date");

            entity.HasOne<Url>().WithMany().HasForeignKey(s => s.UrlId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UrlCountryDailyStat>(entity =>
        {
            entity.ToTable("UrlCountryDailyStats");
            entity.HasKey(s => new { s.UrlId, s.Date, s.CountryCode });
            entity.Property(s => s.Date).HasColumnType("date");
            entity.Property(s => s.CountryCode).IsRequired().HasMaxLength(2);

            entity.HasOne<Url>().WithMany().HasForeignKey(s => s.UrlId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UrlDeviceDailyStat>(entity =>
        {
            entity.ToTable("UrlDeviceDailyStats");
            entity.HasKey(s => new { s.UrlId, s.Date, s.DeviceType });
            entity.Property(s => s.Date).HasColumnType("date");
            entity.Property(s => s.DeviceType).IsRequired().HasMaxLength(16);

            entity.HasOne<Url>().WithMany().HasForeignKey(s => s.UrlId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(rt => rt.Id);

            entity.Property(rt => rt.Token)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasIndex(rt => rt.Token)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_Token");

            entity.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
