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

        /// <summary>
        /// Nombres de propiedad que ActualizarAsync NUNCA debe copiar desde el objeto entrante,
        /// aunque no sean parte de la clave. Para columnas cuyo valor lo gobierna otra parte del
        /// sistema (p. ej. costo/existencia de inventario) y que los DTO de actualización no traen.
        /// Vacío por defecto: sin cambio de comportamiento para las entidades existentes.
        /// </summary>
        protected virtual ISet<string> PropiedadesNoActualizables { get; } = new HashSet<string>();

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

            // No se usa CurrentValues.SetValues(entity) porque los DTO de actualización normalmente
            // no incluyen la clave primaria (viaja en la ruta, no en el body): el objeto "entity"
            // mapeado desde ese DTO trae la clave en su valor por defecto (0/null), y EF Core rechaza
            // de inmediato cualquier intento de modificarla ("part of a key and so cannot be
            // modified"). En su lugar se copian a mano solo las columnas que no son parte de la clave
            // (ni de PropiedadesNoActualizables), leyendo los valores del objeto "entity" sin que EF
            // llegue a rastrearlo.
            CopiarPropiedadesActualizables(entrada, entity);

            return await Contexto.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// Copia sobre la entidad rastreada (<paramref name="entrada"/>) el valor de cada propiedad
        /// escalar de <paramref name="origen"/>, saltando la clave primaria y las declaradas en
        /// <see cref="PropiedadesNoActualizables"/>. Extraído de ActualizarAsync para poder probarlo
        /// sin base de datos (Attach / Entry / Property().CurrentValue son operaciones en memoria).
        /// </summary>
        protected virtual void CopiarPropiedadesActualizables(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entrada, TEntity origen)
        {
            var clave = entrada.Metadata.FindPrimaryKey()!.Properties.Select(p => p.Name).ToHashSet();
            foreach (var propiedad in entrada.Metadata.GetProperties())
            {
                if (clave.Contains(propiedad.Name) || PropiedadesNoActualizables.Contains(propiedad.Name))
                    continue;

                entrada.Property(propiedad.Name).CurrentValue = propiedad.PropertyInfo?.GetValue(origen);
            }
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

        public virtual async Task AgregarSinGuardarAsync(TEntity entity)
        {
            await DbSet.AddAsync(entity);
        }
    }
}
