using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IUsuarioDomain
    {
        #region async methods
        /// <summary>
        /// Crea un usuario. <c>obj.Password</c> llega en texto plano: el domain valida la política,
        /// la hashea, asigna el Id (max + 1) y normaliza los flags S/N.
        /// </summary>
        Task<int> InsertarAsync(Usuario obj);

        /// <summary>
        /// Actualiza solo Nombre, Email, SuperUsuario, Bloqueado y (si <c>obj.Password</c> no está vacío) la contraseña.
        /// <paramref name="usuarioActualId"/> es el Id del usuario autenticado (null = desconocido: se omite la auto-protección).
        /// </summary>
        Task<bool> ActualizarAsync(int id, Usuario obj, int? usuarioActualId);

        /// <summary>Eliminación lógica (Eliminado = 'S'). Devuelve false si no existe o ya está eliminado.</summary>
        Task<bool> EliminarAsync(int id, int? usuarioActualId);

        /// <summary>Obtiene un usuario no eliminado por Id (un eliminado se comporta como inexistente).</summary>
        Task<Usuario> ObtenerAsync(int id);

        /// <summary>Obtiene por código, INCLUYENDO eliminados (unicidad de código y login).</summary>
        Task<Usuario> ObtenerAsync(string codigo);

        /// <summary>Usuarios no eliminados.</summary>
        Task<IQueryable<Usuario>> ObtenerTodoAsync();
        Task<IEnumerable<Usuario>> ObtenerContengaNombreAsync(string name);
        #endregion
    }
}
