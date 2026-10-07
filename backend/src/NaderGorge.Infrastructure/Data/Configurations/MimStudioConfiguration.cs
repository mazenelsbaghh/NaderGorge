using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Data.Configurations;

public static class MimStudioConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<LessonMimStudio>(e =>
        {
            e.ToTable("lesson_mim_studios");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LessonId).IsUnique();
            e.Property(x => x.DocumentJson).HasColumnType("jsonb");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<LessonVideo>().WithMany().HasForeignKey(x => x.SourceVideoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<HiggsfieldMcpConnection>(e =>
        {
            e.ToTable("higgsfield_mcp_connections");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AdminUserId).IsUnique();
            e.Property(x => x.ClientId).HasMaxLength(2048);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
