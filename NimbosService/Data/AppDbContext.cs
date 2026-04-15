using Microsoft.EntityFrameworkCore;
using NimbosService.Models;

namespace NimbosService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<DailySnapshot> DailySnapshots => Set<DailySnapshot>();
    public DbSet<Shield> Shields => Set<Shield>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<FamilyInvite> FamilyInvites => Set<FamilyInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.DeviceId);
            e.HasIndex(u => u.GoogleId).IsUnique().HasFilter("[GoogleId] IS NOT NULL");
            e.HasIndex(u => u.AppleId).IsUnique().HasFilter("[AppleId] IS NOT NULL");
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.UserId);
            e.HasOne(t => t.User)
             .WithMany(u => u.Tasks)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DailySnapshot>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.UserId, d.SnapshotDate }).IsUnique();
            e.HasOne(d => d.User)
             .WithMany(u => u.DailySnapshots)
             .HasForeignKey(d => d.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shield>(e =>
        {
            e.HasKey(s => s.UserId);
            e.HasOne(s => s.User)
             .WithOne(u => u.Shield)
             .HasForeignKey<Shield>(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Family>(e =>
        {
            e.HasKey(f => f.Id);
        });

        modelBuilder.Entity<FamilyInvite>(e =>
        {
            e.HasKey(fi => fi.Id);
            e.Property(fi => fi.Email).HasMaxLength(256).IsRequired();
            e.Property(fi => fi.InviteCode).HasMaxLength(8).IsRequired();
            e.HasIndex(fi => fi.InviteCode).IsUnique();
            e.HasOne(fi => fi.Family)
             .WithMany(f => f.Invites)
             .HasForeignKey(fi => fi.FamilyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FamilyMember>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.FamilyId, m.UserId }).IsUnique();
            e.HasIndex(m => m.UserId);
            e.HasOne(m => m.Family)
             .WithMany(f => f.Members)
             .HasForeignKey(m => m.FamilyId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User)
             .WithMany()
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
