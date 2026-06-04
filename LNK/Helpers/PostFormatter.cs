using LNK.Models;

namespace LNK.Helpers;

public static class PostFormatter
{
    public static string BuildFullText(Post post) =>
        $"{post.Hook}\n\n{post.Content}\n\n{post.CallToAction}\n\n{post.Hashtags}".Trim();

    public static string BuildClipboardText(Post post) => BuildFullText(post);
}
