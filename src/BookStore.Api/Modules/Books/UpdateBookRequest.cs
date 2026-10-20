using BookStore.Application.Common.Exceptions;

namespace BookStore.Api.Modules.Books;

public sealed record UpdateBookRequest
{
    // Omitted properties keep their default; explicit null invokes the accessor.
    public string? Title
    {
        get;
        init => field = value ?? throw new BadRequestException("O título não pode ser null.");
    }

    public string? Author
    {
        get;
        init => field = value ?? throw new BadRequestException("O autor não pode ser null.");
    }

    public int? Quantity
    {
        get;
        init => field = value ?? throw new BadRequestException("A quantidade não pode ser null.");
    }
}
