namespace BookStore.Domain.Entities;

public sealed class User : BaseEntity
{
    private User()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public static User Create(string name)
        => new()
        {
            Name = name.Trim()
        };
}