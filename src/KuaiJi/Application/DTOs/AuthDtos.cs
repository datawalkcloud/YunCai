using System.ComponentModel.DataAnnotations;
using MediatR;

namespace KuaiJi.Application.DTOs
{
    public class LoginCommand : IRequest<LoginResult?>
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResult
    {
        public required string Token { get; init; }
        public required DateTime ExpiresAt { get; init; }
        public required UserDto User { get; init; }
    }

    public class UserDto
    {
        public required string Id { get; init; }
        public required string Username { get; init; }
        public required string DisplayName { get; init; }
    }
}
