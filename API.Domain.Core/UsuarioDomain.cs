using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class UsuarioDomain : IUsuarioDomain
    {
        private readonly IRepositorioGenerico<Usuario, int> _repoGenericoUsuario;

        public UsuarioDomain(IRepositorioGenerico<Usuario, int> repoGenericoUsuario)
        {
            _repoGenericoUsuario = repoGenericoUsuario;
        }

        #region async methods
        public async Task<int> InsertarAsync(Usuario obj)
        {
            if (await ObtenerAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un usuario con el código: {obj.Codigo}");
            }

            var insertado = await _repoGenericoUsuario.InsertarAsync(obj);
            return insertado.Id;
        }

        public async Task<bool> ActualizarAsync(int codigo, Usuario obj)
        {
            var existente = await ObtenerAsync(obj.Codigo);
            if (existente != null && existente.Id != codigo)
            {
                throw new Exception($"Ya existe un usuario con el código: {obj.Codigo}");
            }

            return await _repoGenericoUsuario.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            return await _repoGenericoUsuario.EliminarAsync(codigo);
        }

        public async Task<IQueryable<Usuario>> ObtenerTodoAsync()
        {
            return await _repoGenericoUsuario.ObtenerTodoAsync();
        }

        public async Task<Usuario> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoUsuario.ObtenerTodoAsync();
            var usuario = await queryable.FirstOrDefaultAsync(x => x.Id == codigo);
            return usuario;
        }

        public async Task<Usuario> ObtenerAsync(string codigo)
        {
            var queryable = await _repoGenericoUsuario.ObtenerTodoAsync();
            var usuario = await queryable.FirstOrDefaultAsync(x => x.Codigo == codigo);
            return usuario;
        }

        public async Task<IEnumerable<Usuario>> ObtenerContengaNombreAsync(string name)
        {
            var queryable = await _repoGenericoUsuario.ObtenerTodoAsync();
            var usuario = await queryable.Where(x => x.Nombre.Contains(name)).ToListAsync();
            return usuario;
        }

        #endregion
    }
}
