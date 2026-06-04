using LNK.Data;
using LNK.Helpers;
using LNK.Models;
using LNK.Services;
using LNK.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LNK.Configuration;

namespace LNK.Controllers;

[Authorize]
public class PostsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPostGenerationService _generator;
    private readonly IEmailService _email;
    private readonly EmailSettings _emailSettings;

    public PostsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IPostGenerationService generator,
        IEmailService email,
        IOptions<EmailSettings> emailSettings)
    {
        _db = db;
        _userManager = userManager;
        _generator = generator;
        _email = email;
        _emailSettings = emailSettings.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Review(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == user!.Id);
        if (post == null) return NotFound();

        return View(new PostReviewViewModel
        {
            Post = post,
            ClipboardText = PostFormatter.BuildClipboardText(post)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Regenerate(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == user!.Id);
        if (user == null || settings == null) return Challenge();

        var post = await _generator.GenerateForUserAsync(user, settings);
        return RedirectToAction("Review", new { id = post.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string hook, string content, string callToAction, string hashtags)
    {
        var user = await _userManager.GetUserAsync(User);
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == user!.Id);
        if (post == null) return NotFound();

        post.Hook = hook;
        post.Content = content;
        post.CallToAction = callToAction;
        post.Hashtags = hashtags;
        post.FullText = PostFormatter.BuildFullText(post);
        await _db.SaveChangesAsync();
        return RedirectToAction("Review", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailPost(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == user!.Id);
        if (post == null || user == null) return NotFound();

        var url = $"{_emailSettings.AppBaseUrl.TrimEnd('/')}/Posts/Review/{post.Id}";
        await _email.SendPostEmailAsync(user, post, url);
        TempData["Toast"] = "Email sent successfully.";
        return RedirectToAction("Review", new { id });
    }
}
