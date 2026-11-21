namespace API.Infraestructure.Interface
{
    public interface IRepositorioGenerico<TEntity, TKey> where TEntity : class
    {
        Task<TEntity?> ObtenerAsync(TKey id);
        Task<TEntity> InsertarAsync(TEntity entity);
        Task<bool> ActualizarAsync(TKey id, TEntity entity);
        Task<bool> EliminarAsync(TKey id);
        Task<IQueryable<TEntity>> ObtenerTodoAsync();

        /// <summary>
        /// Adjunta una entidad nueva al ChangeTracker SIN llamar SaveChangesAsync.
        /// El caller es responsable de persistir (p. ej. junto con el INSERT de un documento,
        /// en una sola transacción implícita). Usado por el servicio de asiento de inventario.
        /// </summary>
        Task AgregarSinGuardarAsync(TEntity entity);

        /// <summary>
        /// Deja de rastrear las entidades de este tipo que quedaron pendientes (nuevas, modificadas o eliminadas) en el contexto,
        /// para que un guardado fallido no se vuelva a intentar en un SaveChanges posterior de la misma petición.
        /// No toca la base de datos ni las entidades de otros tipos.
        /// </summary>
        void DescartarCambiosPendientes();
    }
}
