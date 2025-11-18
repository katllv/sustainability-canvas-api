using Microsoft.EntityFrameworkCore;
using SustainabilityCanvas.Api.Models;

namespace SustainabilityCanvas.Api.Data;

public class SustainabilityCanvasContext : DbContext
{
    public SustainabilityCanvasContext(DbContextOptions<SustainabilityCanvasContext> options) : base(options)
    {
    }

    // Dbset for each model
    public DbSet<Profile> Profiles { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectCollaborator> ProjectCollaborators { get; set; }
    public DbSet<Impact> Impacts { get; set; }
    public DbSet<Sdg> Sdgs { get; set; }
    public DbSet<ImpactSdg> ImpactSdgs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Composite key for ImpactSdg
        modelBuilder.Entity<ImpactSdg>()
            .HasKey(x => new { x.ImpactId, x.SdgId });

        // Configure ImpactSdg relationships with navigation properties
        modelBuilder.Entity<ImpactSdg>()
            .HasOne(impactSdg => impactSdg.Impact)
            .WithMany(i => i.ImpactSdgs)
            .HasForeignKey(impactSdg => impactSdg.ImpactId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ImpactSdg>()
            .HasOne(impactSdg => impactSdg.Sdg)
            .WithMany()
            .HasForeignKey(impactSdg => impactSdg.SdgId)
            .OnDelete(DeleteBehavior.Cascade);

        // When Project is deleted -> delete all Impacts
        modelBuilder.Entity<Impact>()
            .HasOne<Project>()
            .WithMany()
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ProjectCollaborator relationships with navigation properties
        modelBuilder.Entity<ProjectCollaborator>()
            .HasOne(pc => pc.Project)
            .WithMany()
            .HasForeignKey(pc => pc.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // When Profile is deleted -> delete all their collaborations
        modelBuilder.Entity<ProjectCollaborator>()
            .HasOne(pc => pc.Profile)
            .WithMany()
            .HasForeignKey(pc => pc.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Don't delete Profile if they have projects
        modelBuilder.Entity<Project>()
            .HasOne<Profile>()
            .WithMany()
            .HasForeignKey(p => p.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique Username
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(50);
        });

        // One-to-one User-Profile relationship, delete Profile if User is deleted
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasOne(p => p.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<Profile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AppSetting
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(a => a.Key);
            entity.Property(a => a.Key).HasMaxLength(100);
        });

        // Seed SDG data
        modelBuilder.Entity<Sdg>()
            .HasData(
                new Sdg { Id = 1, Title = "No Poverty" },
                new Sdg { Id = 2, Title = "Zero Hunger" },
                new Sdg { Id = 3, Title = "Good Health and Well-being" },
                new Sdg { Id = 4, Title = "Quality Education" },
                new Sdg { Id = 5, Title = "Gender Equality" },
                new Sdg { Id = 6, Title = "Clean Water and Sanitation" },
                new Sdg { Id = 7, Title = "Affordable and Clean Energy" },
                new Sdg { Id = 8, Title = "Decent Work and Economic Growth" },
                new Sdg { Id = 9, Title = "Industry, Innovation and Infrastructure" },
                new Sdg { Id = 10, Title = "Reduced Inequality" },
                new Sdg { Id = 11, Title = "Sustainable Cities and Communities" },
                new Sdg { Id = 12, Title = "Responsible Consumption and Production" },
                new Sdg { Id = 13, Title = "Climate Action" },
                new Sdg { Id = 14, Title = "Life Below Water" },
                new Sdg { Id = 15, Title = "Life on Land" },
                new Sdg { Id = 16, Title = "Peace, Justice and Strong Institutions" },
                new Sdg { Id = 17, Title = "Partnerships for the Goals" }
            );
    }
}