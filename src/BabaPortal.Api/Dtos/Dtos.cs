namespace BabaPortal.Api.Dtos;

public record RegisterRequest(string Username, string Email, string Password, string? DisplayName);
public record LoginRequest(string UsernameOrEmail, string Password);
public record AuthResponse(string Token, UserDto User);
public record UserDto(Guid Id, string Username, string? DisplayName, string Email, string? AvatarUrl);

public record GuestChatRequest(string Message, Guid? PersonalityId);
public record ChatRequest(string Message, Guid? ConversationId, Guid? PersonalityId);

public record CreatePersonalityRequest(string Name, string? Tagline, string SystemPrompt, string? Voice, string? AvatarUrl, bool IsPublic);
public record CreateAvatarRequest(string Name, string Kind, string? ImageUrl, string? ModelUrl, string? PrimaryColor, bool IsPublic);
public record AddMemoryRequest(string Content, string? Kind, float? Importance);
