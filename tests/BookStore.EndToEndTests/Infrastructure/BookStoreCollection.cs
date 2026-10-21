namespace BookStore.EndToEndTests.Infrastructure;

[CollectionDefinition(nameof(BookStoreCollection), DisableParallelization = true)]
public sealed class BookStoreCollection : ICollectionFixture<BookStoreFixture>;