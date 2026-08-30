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

            var entrada = Contexto.Entry(existente);
            var clavePrimaria = entrada.Metadata.FindPrimaryKey()!.Properties
                .Select(p => p.Name)
                .ToHashSet();

            // No se usa CurrentValues.SetValues(entity) porque los DTO de actualización normalmente
            // no incluyen la clave primaria (viaja en la ruta, no en el body): el objeto "entity"
            // mapeado desde ese DTO trae la clave en su valor por defecto (0/null), y EF Core rechaza
            // de inmediato cualquier intento de modificarla ("part of a key and so cannot be
            // modified"). En su lugar se copian a mano solo las columnas que no son parte de la clave,
            // leyendo los valores del objeto "entity" sin que EF llegue a rastrearlo.
            foreach (var propiedad in entrada.Metadata.GetProperties())
            {
                if (clavePrimaria.Contains(propiedad.Name))
                    continue;

                entrada.Property(propiedad.Name).CurrentValue = propiedad.PropertyInfo?.GetValue(entity);
            }

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
