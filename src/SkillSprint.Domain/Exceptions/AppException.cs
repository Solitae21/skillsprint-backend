namespace SkillSprint.Domain;

public class AppException : Exception
{
    public int Status;
    public string Code;
    public object? Details;
    public AppException(int status, string code, string message, object? details = null) : base(message)
    {
        Status = status;
        Code = code;
        Details = details;
    }

}