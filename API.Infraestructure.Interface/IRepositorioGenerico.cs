namespace API.Infraestructure.Interface
{
    public interface IRepositorioGenerico<T1>
    {
        #region async methods
        Task<T1> ObtenerAsync(int id);
        Task<int> InsertarAsync(T1 obj);
        Task<bool> ActualizarAsync(int id, T1 obj);
        Task<bool> EliminarAsync(int id);
        Task<IQueryable<T1>> ObtenerTodoAsync();
        #endregion
    }

    public interface IRepositorioGenericoDos<T1>
    {
        #region async methods
        Task<bool> InsertarAsync(T1 obj);
        Task<bool> ActualizarAsync(string codigo, T1 obj);
        Task<bool> EliminarAsync(string codigo);
        Task<T1> ObtenerAsync(string codigo);
        Task<IQueryable<T1>> ObtenerTodoAsync();
        #endregion
    }
}
