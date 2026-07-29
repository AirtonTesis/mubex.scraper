using Domain.Entities;
using Domain.Validation;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.SearchLists;

public record UpdateSearchListCommand(
    Guid Id,
    string Name,
    List<string> Keywords,
    List<string> Domains) : IRequest<Result<bool>>;

public class UpdateSearchListHandler : IRequestHandler<UpdateSearchListCommand, Result<bool>>
{
    private readonly ApplicationDbContext _context;

    public UpdateSearchListHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateSearchListCommand request, CancellationToken cancellationToken)
    {
        var searchList = await _context.SearchLists
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (searchList == null)
            return Result<bool>.Failure(new List<ValidationKey>
            {
                ValidationKey.Custom("search_list", "id", "not_found")
            });

        searchList.Update(request.Name, request.Keywords, request.Domains);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
