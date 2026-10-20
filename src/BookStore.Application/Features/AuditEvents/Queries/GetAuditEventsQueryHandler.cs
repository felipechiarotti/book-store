using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using BookStore.Application.Common.Options;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace BookStore.Application.Features.AuditEvents.Queries;

public sealed record GetAuditEventsQuery(
    int Page = 1,
    int? PageSize = null,
    string? EntityName = null,
    Guid? EntityId = null,
    string? CorrelationId = null) : IRequest<PageResponse<AuditEventResponse>>;

public sealed class GetAuditEventsQueryValidator : AbstractValidator<GetAuditEventsQuery>
{
    public GetAuditEventsQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0)
            .WithMessage("A página deve ser maior que zero.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("O tamanho da página deve estar entre 1 e 100.");

        RuleFor(query => query.EntityName)
            .NotEmpty().WithMessage("O nome da entidade não pode ser vazio.")
            .MaximumLength(100).WithMessage("O nome da entidade deve ter no máximo 100 caracteres.")
            .When(query => query.EntityName is not null);

        RuleFor(query => query.EntityId)
            .NotEqual(Guid.Empty)
            .WithMessage("O identificador da entidade não pode ser vazio.")
            .When(query => query.EntityId.HasValue);

        RuleFor(query => query.CorrelationId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O identificador de correlação não pode ser vazio.")
            .MaximumLength(128).WithMessage("O identificador de correlação deve ter no máximo 128 caracteres.")
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("O identificador de correlação não pode conter caracteres de controle.")
            .When(query => query.CorrelationId is not null);
    }
}

public sealed class GetAuditEventsQueryHandler(
    IAuditEventRepository auditEventRepository,
    IOptions<PaginationOptions> paginationOptions)
    : IRequestHandler<GetAuditEventsQuery, PageResponse<AuditEventResponse>>
{
    public async Task<PageResponse<AuditEventResponse>> Handle(
        GetAuditEventsQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize ?? paginationOptions.Value.DefaultPageSize;

        var result = await auditEventRepository.GetPaginatedAsync(
            request.Page,
            pageSize,
            request.EntityName,
            request.EntityId,
            request.CorrelationId,
            cancellationToken);

        return new PageResponse<AuditEventResponse>(
            result.Items,
            request.Page,
            pageSize,
            result.TotalCount);
    }
}
