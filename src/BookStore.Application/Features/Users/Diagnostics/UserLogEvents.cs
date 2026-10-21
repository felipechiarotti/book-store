using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Users.Diagnostics;

internal static class UserLogEvents
{
    internal static readonly EventId Created = new(4000, "UserCreated");
}
