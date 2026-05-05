using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI.Baba.Web.Models;

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

    /// Avatar key chosen for this conversation (preset key like "sage" or a custom Avatar.Id).
    [MaxLength(64)]
    public string? AvatarKey { get; set; }

    /// Mindset key chosen for this conversation ("balanced" / "logical" / ...).
    [MaxLength(64)]
    public string? MindsetKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
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

/// Long-term semantic memory (facts, preferences, summaries BABA should remember about a user).
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

    /// Embedding stored as raw float[] bytes for fast cosine recall.
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

    /// Optional canonical key matching legacy avatars (sage / philosopher / healer / elder / storyteller).
    [MaxLength(32)]
    public string? AvatarKey { get; set; }

    /// Optional canonical mindset (balanced / logical / spiritual / motivational / creative).
    [MaxLength(32)]
    public string? MindsetKey { get; set; }

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
    public string Kind { get; set; } = "robot"; // robot / photo / glb / emoji

    /// Emoji fallback (e.g. "🧙‍♂️") used by the legacy avatar layout.
    [MaxLength(16)]
    public string? Emoji { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    [MaxLength(2048)]
    public string? ModelUrl { get; set; }

    [MaxLength(32)]
    public string PrimaryColor { get; set; } = "#D4A853";

    /// Stable preset key (sage / philosopher / ...) when UserId is null.
    [MaxLength(32)]
    public string? PresetKey { get; set; }

    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
