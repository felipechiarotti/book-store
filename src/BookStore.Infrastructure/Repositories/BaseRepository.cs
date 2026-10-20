using BookStore.Application.Common.Interfaces;
using BookStore.Domain.Entities;
using BookStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Repositories;

public class BaseRepository<TEntity>(AppDbContext dbContext) : IBaseRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly AppDbContext DbContext = dbContext;
    protected readonly DbSet<TEntity> DbSet = dbContext.Set<TEntity>();

    public void Add(IEnumerable<TEntity> entities)
    {
        DbSet.AddRange(entities);
    }

    public void Add(TEntity entity)
    {
        DbSet.Add(entity);
    }
}