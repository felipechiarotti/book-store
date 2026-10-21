namespace BookStore.Domain.Entities;

public sealed class User : BaseEntity
{
    private User()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public static User Create(string name)
    {
        return new User
        {
            Name = name.Trim()
        };
    }
}