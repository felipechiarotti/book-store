using BookStore.Application.Features.Users.Diagnostics;
using BookStore.Application.Common.Interfaces;
using BookStore.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Users.Commands;

public sealed record CreateUserCommand(string Name)
    : IRequest<CreateUserResponse>;

public sealed record CreateUserResponse(Guid Id, string Name);

public sealed class CreateUserCommandValidator
    : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("O nome é obrigatório.")
            .Must(name => name.Trim().Length <= 200)
            .WithMessage("O nome deve ter no máximo 200 caracteres.");
    }
}

public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateUserCommandHandler> logger)
    : IRequestHandler<CreateUserCommand, CreateUserResponse>
{
    public async Task<CreateUserResponse> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = User.Create(request.Name);

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation(UserLogEvents.Created,
            "User {UserId} created.",
            user.Id);

        return new CreateUserResponse(user.Id, user.Name);
    }
}
