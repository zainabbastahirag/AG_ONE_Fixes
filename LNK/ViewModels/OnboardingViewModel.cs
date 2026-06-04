using System.ComponentModel.DataAnnotations;

namespace LNK.ViewModels;

public class OnboardingViewModel
{
    [Required, Display(Name = "Industry")]
    public string Industry { get; set; } = "Technology";

    [Required, Display(Name = "Topics (comma-separated)")]
    public string Topics { get; set; } = "Leadership, Innovation";

    public string Keywords { get; set; } = "";

    [Required]
    public string Tone { get; set; } = "Professional";

    [Required]
    public string PostLength { get; set; } = "Medium";

    [Required, Display(Name = "Daily post time")]
    public TimeSpan DailyPostTime { get; set; } = new(9, 0, 0);

    public string Timezone { get; set; } = "UTC";
}
