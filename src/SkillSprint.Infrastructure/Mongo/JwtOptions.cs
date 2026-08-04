namespace SkillSprint.Infrastructure;

using System.ComponentModel.DataAnnotations;

public class JwtOptions
{
    [Required]
    public string AccessSecret { get; set; } = default!;

    [Required]
    public string RefreshSecret { get; set; } = default!;

    [Required]
    public string Issuer { get; set; } = default!;

    [Required]
    public string Audience { get; set; } = default!;


    [Required]
    public int AccessTokenMinutes { get; set; } = default!;

    [Required]
    public int RefreshTokenDays { get; set; } = default!;
}
