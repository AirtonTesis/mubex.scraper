using Domain.Entities;
using Domain.Validation;
using Infrastructure.Authentication;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.Auth;

public record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(string Token, Guid UserId, string Email);

public class LoginHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginHandler(ApplicationDbContext context, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken);

        if (user == null)
        {
            return Result<LoginResponse>.Failure(new List<ValidationKey>
            {
                ValidationKey.Custom("auth", "credentials", "invalid")
            });
        }

        // Verify password using BCrypt
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Result<LoginResponse>.Failure(new List<ValidationKey>
            {
                ValidationKey.Custom("auth", "credentials", "invalid")
            });
        }

        var token = _jwtTokenService.GenerateToken(user);

        return Result<LoginResponse>.Success(new LoginResponse(
            token,
            user.Id,
            user.Email));
    }
}
