namespace KuaiJi.Models
{
    /// <summary>
    /// 应用内的用户实体（当前为内存演示用户）。
    /// </summary>
    public class AppUser
    {
        public required string Id { get; init; }
        public required string Username { get; init; }
        public required string PasswordHash { get; init; }
        public required string DisplayName { get; init; }
    }
}
