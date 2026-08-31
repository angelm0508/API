namespace API.Domain.Interface
{
    /// <summary>
    /// Ejecuta una operación dentro de una transacción de base de datos. Al terminar sin
    /// excepción: SaveChangesAsync (flushea todo lo que la operación dejó pendiente en el
    /// ChangeTracker) + Commit. Si la operación lanza: Rollback y se repropaga la excepción.
    /// Permite que los domains orquesten "guardar encabezado -> obtener Entry -> añadir
    /// líneas + inventario -> guardar" de forma atómica sin llamar SaveChangesAsync
    /// directamente (quedan 100% mockeables).
    /// </summary>
    public interface IEjecutorTransaccion
    {
        Task<T> EjecutarAsync<T>(Func<Task<T>> operacion);
    }
}
