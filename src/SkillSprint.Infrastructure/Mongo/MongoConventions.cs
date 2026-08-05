namespace SkillSprint.Infrastructure;

using MongoDB.Bson.Serialization.Conventions;

public static class MongoConventions
{
    public static void Register()
    {
        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),      // CourseName -> "courseName"
            new IgnoreExtraElementsConvention(true),
        };
        ConventionRegistry.Register("skillsprint", pack, _ => true);
    }
}
