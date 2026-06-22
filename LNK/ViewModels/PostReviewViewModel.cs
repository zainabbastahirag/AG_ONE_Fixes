using LNK.Models;

namespace LNK.ViewModels;

public class PostReviewViewModel
{
    public Post Post { get; set; } = null!;
    public string ClipboardText { get; set; } = string.Empty;
}
