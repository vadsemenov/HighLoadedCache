namespace HighLoadedCache.Domain.Dto;

public class UserProfile
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}