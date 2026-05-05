namespace AI.Baba.Web.Models;

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

public class UserMemory
{
    public string? Name { get; set; }
    public string PreferredAvatar { get; set; } = "sage";
    public string PreferredMindset { get; set; } = "balanced";
    public List<ConversationEntry> History { get; set; } = new();
}
