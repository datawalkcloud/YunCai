namespace YunCai.Modules.Identity.Domain
{
    public class User
    {
        public required string Id { get; init; }
        public required string Username { get; init; }
        public required string PasswordHash { get; init; }
        public required string DisplayName { get; init; }
    }

    public interface IUserRepository
    {
        User? FindByUsername(string username);
    }
}
