namespace HighLoadedCache.Domain.Dto;

[GenerateBinarySerializer]
public partial class UserProfile
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public static UserProfile Create() => new() { Id = 1, Username = "Popkov", CreatedAt = DateTime.UtcNow };
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class GenerateBinarySerializerAttribute : Attribute
{

}