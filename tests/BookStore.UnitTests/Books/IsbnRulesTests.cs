using BookStore.Domain.Rules;

namespace BookStore.UnitTests.Books;

[Trait("Category", "Unit")]
public sealed class IsbnRulesTests
{
    [Theory]
    [InlineData(" 0-8044-2957-x ", "080442957X")]
    [InlineData("978-0-13-235088-4", "9780132350884")]
    [InlineData("978 0 13 235088 4", "9780132350884")]
    public void Normalize_WhenIsbnContainsFormatting_ShouldReturnCanonicalValue(string value, string expected)
    {
        var normalized = IsbnRules.Normalize(value);

        Assert.Equal(expected, normalized);
    }
}