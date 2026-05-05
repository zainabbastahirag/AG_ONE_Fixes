using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BabaPortal.Api.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? DisplayName { get; set; }

    [MaxLength(2048)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<MemoryEntry> Memories { get; set; } = new List<MemoryEntry>();
    public ICollection<Personality> Personalities { get; set; } = new List<Personality>();
    public ICollection<Avatar> CustomAvatars { get; set; } = new List<Avatar>();
}

public class Conversation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))] public User? User { get; set; }

    public Guid? PersonalityId { get; set; }
    [ForeignKey(nameof(PersonalityId))] public Personality? Personality { get; set; }

    [MaxLength(256)]
    public string Title { get; set; } = "New chat";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class Message
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }
    [ForeignKey(nameof(ConversationId))] public Conversation? Conversation { get; set; }

    [Required, MaxLength(16)]
    public string Role { get; set; } = "user"; // user / assistant / system

    [Required]
    public string Content { get; set; } = string.Empty;

    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// Long-term semantic memory (summaries, facts BABA should remember)
public class MemoryEntry
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))] public User? User { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Kind { get; set; } = "fact"; // fact / preference / event / summary

    public float Importance { get; set; } = 0.5f;

    /// Embedding stored as raw float[] bytes (length-prefixed) for fast cosine recall in-process
    public byte[] Embedding { get; set; } = Array.Empty<byte>();

    public int EmbeddingDim { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public int UseCount { get; set; }
}

public class Personality
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; } // null = global preset
    [ForeignKey(nameof(UserId))] public User? User { get; set; }

    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Tagline { get; set; }

    [Required]
    public string SystemPrompt { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Voice { get; set; } = "default";

    [MaxLength(2048)]
    public string? AvatarUrl { get; set; }

    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Avatar
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }
    [ForeignKey(nameof(UserId))] public User? User { get; set; }

    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Kind { get; set; } = "robot"; // robot / photo / glb

    [MaxLength(2048)]
    public string? ImageUrl { get; set; } // for photo-based billboard avatars

    [MaxLength(2048)]
    public string? ModelUrl { get; set; } // for glb models

    [MaxLength(32)]
    public string PrimaryColor { get; set; } = "#7c3aed";

    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
