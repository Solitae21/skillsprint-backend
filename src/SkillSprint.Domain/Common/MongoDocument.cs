namespace SkillSprint.Domain;

public abstract class MongoDocument
{
    public string Id { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}