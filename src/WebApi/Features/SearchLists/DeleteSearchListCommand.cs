using Domain.Entities;
using Domain.Validation;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.SearchLists;

public record DeleteSearchListCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteSearchListHandler : IRequestHandler<DeleteSearchListCommand, Result<bool>>
{
    private readonly ApplicationDbContext _context;

    public DeleteSearchListHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteSearchListCommand request, CancellationToken cancellationToken)
    {
        var searchList = await _context.SearchLists
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (searchList == null)
            return Result<bool>.Failure(new List<ValidationKey>
            {
                ValidationKey.Custom("search_list", "id", "not_found")
            });

        _context.SearchLists.Remove(searchList);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
