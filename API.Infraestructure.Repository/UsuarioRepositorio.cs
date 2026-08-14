using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class UsuarioRepositorio : IRepositorioGenerico<Usuario>
    {
        private readonly ApiDbTestContext _contexto;

        public UsuarioRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<Usuario> ObtenerAsync(int codigo)
        {
            return await _contexto.Usuarios
                                    .FirstOrDefaultAsync(x => x.Id == codigo);
        }

        public async Task<int> InsertarAsync(Usuario obj)
        {
            await _contexto.Usuarios.AddAsync(obj);
            await _contexto.SaveChangesAsync();

            return obj.Id;
        }

        public async Task<bool> ActualizarAsync(int codigo, Usuario obj)
        {
            var usuario = await _contexto
                                        .Usuarios
                                        .SingleOrDefaultAsync(x => x.Id == codigo);

            usuario.Id = obj.Id;
            usuario.Password = obj.Password;
            usuario.LlaveInterna = obj.LlaveInterna;
            usuario.Codigo = obj.Codigo;
            usuario.Nombre = obj.Nombre;
            usuario.Eliminado = obj.Eliminado;
            usuario.SuperUsuario = obj.SuperUsuario;
            usuario.Email = obj.Email;
            usuario.Bloqueado = obj.Bloqueado;
            usuario.UltimaContra = obj.UltimaContra;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }

        public async Task<bool> EliminarAsync(int codigo)
        {
            var usuario = await _contexto
                                        .Usuarios
                                        .SingleAsync(x => x.Id == codigo);

            _contexto.Usuarios.Remove(usuario);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }

        public async Task<IQueryable<Usuario>> ObtenerTodoAsync()
        {
            return _contexto.Usuarios;
        }
        #endregion
    }
}
