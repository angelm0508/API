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
    }
}
