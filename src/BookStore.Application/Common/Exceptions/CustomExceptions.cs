namespace BookStore.Application.Common.Exceptions;

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message, Exception? innerException = null) : Exception(message, innerException);

public class BadRequestException(string message) : Exception(message);

public sealed class IdempotencyKeyConflictException(Exception innerException)
    : ConflictException("A chave de idempotência já foi utilizada.", innerException);
