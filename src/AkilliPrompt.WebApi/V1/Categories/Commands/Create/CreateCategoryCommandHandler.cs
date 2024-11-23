
using AkilliPrompt.Domain.Entities;
using AkilliPrompt.Domain.ValueObjects;
using AkilliPrompt.Persistence.EntityFramework.Contexts;
using AkilliPrompt.WebApi.Helpers;
using AkilliPrompt.WebApi.Models;
using MediatR;

namespace AkilliPrompt.WebApi.V1.Categories.Commands.Create;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, ResponseDto<Guid>>
{
    private readonly ApplicationDbContext _dbContext;
    public CreateCategoryCommandHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<ResponseDto<Guid>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = Category.Create(request.Name, request.Description);

        _dbContext.Categories.Add(category);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ResponseDto<Guid>.Success(category.Id, MessageHelper.GetApiSuccessCreatedMessage("Kategori"));
    }
}