using Domain.Entities;
using Domain.Validation;
using Infrastructure.Persistence;
using MediatR;

namespace WebApi.Features.SearchLists;

public record CreateSearchListCommand(
    string Name,
    List<string> Keywords,
    List<string> Domains,
    Guid UserId) : IRequest<Result<Guid>>;

public class CreateSearchListHandler : IRequestHandler<CreateSearchListCommand, Result<Guid>>
{
    private readonly ApplicationDbContext _context;

    public CreateSearchListHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateSearchListCommand request, CancellationToken cancellationToken)
    {
        var result = SearchList.Create(
            request.Name,
            request.Keywords,
            request.Domains,
            request.UserId);

        if (!result.IsSuccess)
            return Result<Guid>.Failure(result.Errors);

        _context.SearchLists.Add(result.Value);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(result.Value.Id);
    }
}
