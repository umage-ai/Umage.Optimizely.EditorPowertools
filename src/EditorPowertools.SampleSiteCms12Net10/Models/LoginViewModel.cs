using System.ComponentModel.DataAnnotations;

namespace UmageAI.Optimizely.EditorPowerTools.SampleSite.Models;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; }

    [Required]
    public string Password { get; set; }
}
