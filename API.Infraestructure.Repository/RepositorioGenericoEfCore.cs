using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public abstract class RepositorioGenericoEfCore<TEntity, TKey> : IRepositorioGenerico<TEntity, TKey>
        where TEntity : class
    {
        protected readonly ApiDbTestContext Contexto;

        protected RepositorioGenericoEfCore(ApiDbTestContext contexto)
        {
            Contexto = contexto;
        }

        protected DbSet<TEntity> DbSet => Contexto.Set<TEntity>();

        public virtual async Task<TEntity?> ObtenerAsync(TKey id)
        {
            return await DbSet.FindAsync(id);
        }

        public virtual async Task<TEntity> InsertarAsync(TEntity entity)
        {
            await DbSet.AddAsync(entity);
            await Contexto.SaveChangesAsync();
            return entity;
        }

        public virtual async Task<bool> ActualizarAsync(TKey id, TEntity entity)
        {
            var existente = await ObtenerAsync(id);
            if (existente is null)
                return false;

            Contexto.Entry(existente).CurrentValues.SetValues(entity);
            return await Contexto.SaveChangesAsync() > 0;
        }

        public virtual async Task<bool> EliminarAsync(TKey id)
        {
            var existente = await ObtenerAsync(id);
            if (existente is null)
                return false;

            DbSet.Remove(existente);
            return await Contexto.SaveChangesAsync() > 0;
        }

        public virtual Task<IQueryable<TEntity>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.AsQueryable());
        }
    }
}
