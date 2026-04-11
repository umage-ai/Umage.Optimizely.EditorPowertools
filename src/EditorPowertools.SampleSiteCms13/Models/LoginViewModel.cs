using System.ComponentModel.DataAnnotations;

namespace EditorPowertools.SampleSiteCms13.Models;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; }

    [Required]
    public string Password { get; set; }
}
