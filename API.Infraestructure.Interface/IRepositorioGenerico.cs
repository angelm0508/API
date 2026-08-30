namespace API.Infraestructure.Interface
{
    public interface IRepositorioGenerico<TEntity, TKey> where TEntity : class
    {
        Task<TEntity?> ObtenerAsync(TKey id);
        Task<TEntity> InsertarAsync(TEntity entity);
        Task<bool> ActualizarAsync(TKey id, TEntity entity);
        Task<bool> EliminarAsync(TKey id);
        Task<IQueryable<TEntity>> ObtenerTodoAsync();
    }
}
