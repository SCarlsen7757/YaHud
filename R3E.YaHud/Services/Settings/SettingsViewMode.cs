using System.ComponentModel.DataAnnotations;

namespace R3E.YaHud.Services.Settings
{
    public enum SettingsViewMode
    {
        [Display(Name = "Easy")]
        Beginner = 0,
        [Display(Name = "Normal")]
        Intermediate = 1,
        [Display(Name = "Expert")]
        Expert = 2,
    }
}
