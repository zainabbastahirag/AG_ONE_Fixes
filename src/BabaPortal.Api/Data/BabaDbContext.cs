using BabaPortal.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BabaPortal.Api.Data;

public class BabaDbContext : DbContext
{
    public BabaDbContext(DbContextOptions<BabaDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MemoryEntry> Memories => Set<MemoryEntry>();
    public DbSet<Personality> Personalities => Set<Personality>();
    public DbSet<Avatar> Avatars => Set<Avatar>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();

        b.Entity<Conversation>().HasIndex(c => new { c.UserId, c.UpdatedAt });
        b.Entity<Message>().HasIndex(m => new { m.ConversationId, m.CreatedAt });
        b.Entity<MemoryEntry>().HasIndex(m => new { m.UserId, m.LastUsedAt });
        b.Entity<MemoryEntry>().HasIndex(m => new { m.UserId, m.Importance });

        b.Entity<Personality>().HasIndex(p => p.UserId);
        b.Entity<Avatar>().HasIndex(a => a.UserId);
    }
}
