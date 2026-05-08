namespace AI.Baba.Web.Models;

// ─── Legacy DTOs (kept for backward compatibility with /api/ask) ─────
public class AskRequest
{
    public string Prompt { get; set; } = "";
    public string Avatar { get; set; } = "sage";
    public string Mindset { get; set; } = "balanced";
    public string? UserName { get; set; }
}

public class AskResponse
{
    public bool Success { get; set; }
    public string Response { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string Mindset { get; set; } = "";
}

public class ConversationEntry
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// Legacy short-term, in-process memory used by /api/ask for guests.
public class UserMemory
{
    public string? Name { get; set; }
    public string PreferredAvatar { get; set; } = "sage";
    public string PreferredMindset { get; set; } = "balanced";
    public List<ConversationEntry> History { get; set; } = new();
}

// ─── New DTOs for the optimized portal ─────────────────────────────────
public record RegisterRequest(string Username, string Email, string Password, string? DisplayName);
public record LoginRequest(string UsernameOrEmail, string Password);
public record AuthResponse(string Token, UserDto User);
public record UserDto(Guid Id, string Username, string? DisplayName, string Email, string? AvatarUrl);

public record StreamChatRequest(
    string Message,
    Guid? ConversationId,
    Guid? PersonalityId,
    string? Avatar,
    string? Mindset,
    string? Mode = null);   // 'chat' | 'voice' | 'panel' — controls num_predict + brevity

public record GuestStreamChatRequest(
    string Message,
    Guid? PersonalityId,
    string? Avatar,
    string? Mindset,
    string? Mode = null);

public record CreatePersonalityRequest(
    string Name,
    string? Tagline,
    string SystemPrompt,
    string? Voice,
    string? AvatarUrl,
    string? AvatarKey,
    string? MindsetKey,
    bool IsPublic);

public record CreateAvatarRequest(
    string Name,
    string Kind,
    string? Emoji,
    string? ImageUrl,
    string? ModelUrl,
    string? PrimaryColor,
    bool IsPublic);

public record AddMemoryRequest(string Content, string? Kind, float? Importance);
