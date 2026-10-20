using BookStore.Domain.Entities;

namespace BookStore.Application.Common.Interfaces;

public interface IBaseRepository<in TEntity> where TEntity: BaseEntity
{
    void Add(TEntity entity);
}