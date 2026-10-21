using BookStore.Domain.Entities;
using BookStore.Domain.Enums;

namespace BookStore.UnitTests.Loans;

[Trait("Category", "Unit")]
public sealed class LoanTests
{
    private static readonly DateTimeOffset LoanedAt = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset DueAt = new(2026, 10, 28, 9, 0, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenDueDateIsNotAfterLoanDate_ShouldRejectLoan(int dueDateOffsetMinutes)
    {
        var dueAt = LoanedAt.AddMinutes(dueDateOffsetMinutes);

        var create = () => Loan.Create(Guid.NewGuid(), Guid.NewGuid(), LoanedAt, dueAt, "create-loan");

        Assert.Throws<ArgumentOutOfRangeException>("dueAt", create);
    }

    [Fact]
    public void Create_WhenDatesHaveAnOffset_ShouldStoreInstantsInUtc()
    {
        var loan = CreateLoan();

        Assert.Equal(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), loan.LoanedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), loan.DueAt);
        Assert.Equal(TimeSpan.Zero, loan.LoanedAt.Offset);
        Assert.Equal(TimeSpan.Zero, loan.DueAt.Offset);
    }

    [Theory]
    [InlineData(LoanStatus.Returned)]
    [InlineData(LoanStatus.Cancelled)]
    public void Close_WhenLoanIsActive_ShouldRecordClosureAndPreserveOriginalData(LoanStatus status)
    {
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var loan = Loan.Create(bookId, userId, LoanedAt, DueAt, "original-key");
        var closedAt = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(-3));

        Close(loan, status, closedAt);

        Assert.Equal(status, loan.Status);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 13, 0, 0, TimeSpan.Zero), loan.ClosedAt);
        Assert.Equal(TimeSpan.Zero, loan.ClosedAt?.Offset);
        Assert.Equal(bookId, loan.BookId);
        Assert.Equal(userId, loan.UserId);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), loan.LoanedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), loan.DueAt);
        Assert.Equal("original-key", loan.IdempotencyKey);
    }

    [Theory]
    [InlineData(LoanStatus.Returned, LoanStatus.Returned)]
    [InlineData(LoanStatus.Returned, LoanStatus.Cancelled)]
    [InlineData(LoanStatus.Cancelled, LoanStatus.Cancelled)]
    [InlineData(LoanStatus.Cancelled, LoanStatus.Returned)]
    public void Close_WhenLoanIsAlreadyClosed_ShouldPreserveOriginalClosure(LoanStatus original, LoanStatus attempted)
    {
        var loan = CreateLoan();
        var closedAt = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        Close(loan, original, closedAt);

        var closeAgain = () => Close(loan, attempted, closedAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(closeAgain);
        Assert.Equal(original, loan.Status);
        Assert.Equal(closedAt, loan.ClosedAt);
    }

    [Theory]
    [InlineData(LoanStatus.Returned)]
    [InlineData(LoanStatus.Cancelled)]
    public void Close_WhenClosurePrecedesLoan_ShouldKeepLoanActive(LoanStatus status)
    {
        var loan = CreateLoan();

        var close = () => Close(loan, status, LoanedAt.AddTicks(-1));

        Assert.Throws<ArgumentOutOfRangeException>("closedAt", close);
        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Null(loan.ClosedAt);
    }

    [Theory]
    [InlineData(LoanStatus.Returned)]
    [InlineData(LoanStatus.Cancelled)]
    public void Close_WhenClosureEqualsLoanDate_ShouldAcceptClosure(LoanStatus status)
    {
        var loan = CreateLoan();

        Close(loan, status, LoanedAt);

        Assert.Equal(status, loan.Status);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), loan.ClosedAt);
    }

    private static Loan CreateLoan()
    {
        return Loan.Create(Guid.NewGuid(), Guid.NewGuid(), LoanedAt, DueAt, "create-loan");
    }

    private static void Close(Loan loan, LoanStatus status, DateTimeOffset closedAt)
    {
        switch (status)
        {
            case LoanStatus.Returned:
                loan.Return(closedAt);
                break;
            case LoanStatus.Cancelled:
                loan.Cancel(closedAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}