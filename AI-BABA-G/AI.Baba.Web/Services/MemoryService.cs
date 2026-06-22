using AI.Baba.Web.Models;
using System.Collections.Concurrent;

namespace AI.Baba.Web.Services;

public class MemoryService
{
    private readonly ConcurrentDictionary<string, UserMemory> _memories = new();
    private const int MaxHistory = 10;

    public UserMemory GetMemory(string sessionId)
        => _memories.GetOrAdd(sessionId, _ => new UserMemory());

    public void AddToHistory(string sessionId, string role, string content)
    {
        var mem = GetMemory(sessionId);
        mem.History.Add(new ConversationEntry { Role = role, Content = content });
        if (mem.History.Count > MaxHistory)
            mem.History.RemoveRange(0, mem.History.Count - MaxHistory);
    }

    public void SetUserName(string sessionId, string name)
        => GetMemory(sessionId).Name = name;

    public string BuildContextPrompt(string sessionId, string avatar, string mindset)
    {
        var mem = GetMemory(sessionId);

        var avatarPersonality = avatar.ToLower() switch
        {
            "sage" => "You are The Sage — ancient, deeply wise, calm, and insightful. You speak with gravitas and timeless wisdom.",
            "philosopher" => "You are The Philosopher — analytical, deep-thinking, Socratic. You question assumptions and explore ideas.",
            "healer" => "You are The Healer — compassionate, gentle, empathetic. You focus on emotional well-being and inner peace.",
            "elder" => "You are The Elder — experienced, traditional, grounded. You share practical life wisdom from decades of living.",
            "storyteller" => "You are The Storyteller — creative, engaging, narrative-driven. You teach through parables and vivid stories.",
            _ => "You are a wise AI guide."
        };

        var mindsetTone = mindset.ToLower() switch
        {
            "balanced" => "Give well-rounded, fair perspectives for all situations.",
            "logical" => "Be clear, rational, and practical. Use evidence-based reasoning.",
            "spiritual" => "Be soulful, mindful, and focused on inner growth and consciousness.",
            "motivational" => "Be encouraging, uplifting, and empowering. Inspire action.",
            "creative" => "Be innovative, out-of-the-box, imaginative. Suggest unexpected approaches.",
            _ => "Be balanced and helpful."
        };

        var nameContext = !string.IsNullOrEmpty(mem.Name) ? $"The user's name is {mem.Name}. Use their name naturally." : "";

        var historyContext = "";
        if (mem.History.Count > 0)
        {
            var recent = mem.History.TakeLast(6);
            historyContext = "Recent conversation:\n" + string.Join("\n", recent.Select(h => $"{h.Role}: {h.Content}"));
        }

        return $@"You are AI Baba-G, a wise, slightly humorous, deeply intelligent assistant.
{avatarPersonality}
{mindsetTone}
{nameContext}

You speak short, natural, conversational sentences — like a real voice companion.
You avoid long essays. Keep responses under 3-4 sentences unless the user asks for detail.
You remember context and adapt your personality.
If the user tells you their name, remember it and use it warmly.

{historyContext}";
    }
}
