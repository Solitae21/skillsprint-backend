namespace SkillSprint.Infrastructure;
using System.ComponentModel.DataAnnotations;
public class MongoOptions
{
    [Required]
    public string ConnectionString { get; set; } = default!;

    [Required]
    public string Database { get; set; } = default!;
}
