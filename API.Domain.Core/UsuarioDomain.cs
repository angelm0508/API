using System.Data;
using System.Text.RegularExpressions;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class UsuarioDomain : IUsuarioDomain
    {
        private const int LongitudMaximaCodigo = 25;
        private const int LongitudMaximaNombre = 150;
        private const int LongitudMaximaEmail = 75;
        private const int LongitudMinimaPassword = 8;

        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly IRepositorioGenerico<Usuario, int> _repoGenericoUsuario;
        private readonly IPasswordHasher<Usuario> _passwordHasher;
        private readonly IEjecutorTransaccion _tx;

        public UsuarioDomain(
            IRepositorioGenerico<Usuario, int> repoGenericoUsuario,
            IPasswordHasher<Usuario> passwordHasher,
            IEjecutorTransaccion tx)
        {
            _repoGenericoUsuario = repoGenericoUsuario;
            _passwordHasher = passwordHasher;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(Usuario obj)
        {
            var codigo = (obj.Codigo ?? string.Empty).Trim();
            if (codigo.Length == 0)
                throw new Exception("El código es obligatorio.");
            if (codigo.Length > LongitudMaximaCodigo)
                throw new Exception($"El código no puede superar {LongitudMaximaCodigo} caracteres.");

            var nombre = NormalizarNombre(obj.Nombre);
            var email = NormalizarEmail(obj.Email);

            if (string.IsNullOrEmpty(obj.Password))
                throw new Exception("La contraseña es obligatoria.");
            ValidarPassword(obj.Password, codigo);

            try
            {
                // Serializable: los bloqueos de rango sobre la lectura por código y sobre el MAX(Id) evitan
                // fantasmas (Id o código duplicado) entre inserciones concurrentes; el perdedor espera o
                // recibe un deadlock/violación de PK, que se traduce a un mensaje amable.
                return await _tx.EjecutarAsync(async () =>
                {
                    // Incluye eliminados lógicamente: el código de un usuario eliminado no se reutiliza.
                    if (await ObtenerAsync(codigo) != null)
                        throw new Exception($"Ya existe un usuario con el código: {codigo}");

                    // La columna Id no es identity: se asigna max + 1. La lectura del máximo y el INSERT van
                    // en la misma transacción (el SaveChanges lo hace el ejecutor); sin reintentos.
                    var queryable = await _repoGenericoUsuario.ObtenerTodoAsync();
                    var maxId = await queryable.Select(x => (int?)x.Id).MaxAsync() ?? 0;

                    var nuevo = new Usuario
                    {
                        Id = maxId + 1,
                        Codigo = codigo,
                        Nombre = nombre,
                        Email = email,
                        SuperUsuario = NormalizarSn(obj.SuperUsuario),
                        Bloqueado = NormalizarSn(obj.Bloqueado),
                        Eliminado = "N"
                    };
                    nuevo.Password = _passwordHasher.HashPassword(nuevo, obj.Password);

                    await _repoGenericoUsuario.AgregarSinGuardarAsync(nuevo);
                    return nuevo.Id;
                }, IsolationLevel.Serializable);
            }
            catch (Exception ex) when (EsConflictoDeConcurrencia(ex))
            {
                throw new Exception("No se pudo crear el usuario porque otra operación se estaba ejecutando. Intente nuevamente.");
            }
        }

        public async Task<bool> ActualizarAsync(int id, Usuario obj, int? usuarioActualId)
        {
            var nombre = NormalizarNombre(obj.Nombre);
            var email = NormalizarEmail(obj.Email);
            var superUsuario = NormalizarSn(obj.SuperUsuario);
            var bloqueado = NormalizarSn(obj.Bloqueado);

            return await _tx.EjecutarAsync(async () =>
            {
                var existente = await _repoGenericoUsuario.ObtenerAsync(id);
                if (existente is null || existente.Eliminado == "S")
                    throw new Exception("El usuario no existe.");

                var codigoEntrante = (obj.Codigo ?? string.Empty).Trim();
                if (codigoEntrante.Length > 0 && !string.Equals(codigoEntrante, (existente.Codigo ?? string.Empty).Trim(), StringComparison.Ordinal))
                    throw new Exception("El código de usuario no se puede modificar.");

                if (!string.IsNullOrEmpty(obj.Password))
                    ValidarPassword(obj.Password, existente.Codigo);

                if (usuarioActualId == id)
                {
                    if (bloqueado == "S" && existente.Bloqueado != "S")
                        throw new Exception("No puede bloquear su propio usuario.");
                    if (existente.SuperUsuario == "S" && superUsuario != "S")
                        throw new Exception("No puede quitarse a sí mismo el permiso de superusuario.");
                }

                if (EsSuperUsuarioActivo(existente) && (superUsuario != "S" || bloqueado == "S"))
                    await ValidarQueQuedeOtroSuperUsuarioActivoAsync(id);

                // Solo estos campos: Id, Codigo, LlaveInterna, Eliminado y UltimaContra no se tocan.
                existente.Nombre = nombre;
                existente.Email = email;
                existente.SuperUsuario = superUsuario;
                existente.Bloqueado = bloqueado;
                if (!string.IsNullOrEmpty(obj.Password))
                    existente.Password = _passwordHasher.HashPassword(existente, obj.Password);

                return true;
            }, IsolationLevel.Serializable);
        }

        public async Task<bool> EliminarAsync(int id, int? usuarioActualId)
        {
            return await _tx.EjecutarAsync(async () =>
            {
                var existente = await _repoGenericoUsuario.ObtenerAsync(id);
                if (existente is null || existente.Eliminado == "S")
                    return false;

                if (usuarioActualId == id)
                    throw new Exception("No puede eliminar su propio usuario.");

                if (EsSuperUsuarioActivo(existente))
                    await ValidarQueQuedeOtroSuperUsuarioActivoAsync(id);

                // Eliminación lógica: Bloqueado, Password y UsuarioPermiso se conservan.
                existente.Eliminado = "S";
                return true;
            }, IsolationLevel.Serializable);
        }

        public async Task<IQueryable<Usuario>> ObtenerTodoAsync()
        {
            var queryable = await _repoGenericoUsuario.ObtenerTodoAsync();
            return queryable.Where(x => x.Eliminado != "S");
        }

        public async Task<Usuario> ObtenerAsync(int codigo)
        {
            var queryable = await _repoGenericoUsuario.ObtenerTodoAsync();
            var usuario = await queryable.FirstOrDefaultAsync(x => x.Id == codigo && x.Eliminado != "S");
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
            var usuario = await queryable.Where(x => x.Eliminado != "S" && x.Nombre.Contains(name)).ToListAsync();
            return usuario;
        }
        #endregion

        #region helpers
        private static bool EsSuperUsuarioActivo(Usuario u) =>
            u.SuperUsuario == "S" && u.Bloqueado != "S" && u.Eliminado != "S";

        private async Task ValidarQueQuedeOtroSuperUsuarioActivoAsync(int idExcluido)
        {
            var queryable = await _repoGenericoUsuario.ObtenerTodoAsync();
            var hayOtro = await queryable.AnyAsync(x =>
                x.Id != idExcluido && x.SuperUsuario == "S" && x.Bloqueado != "S" && x.Eliminado != "S");
            if (!hayOtro)
                throw new Exception("Debe existir al menos un superusuario activo.");
        }

        // Duplicado de PK/índice único (2627, 2601) o deadlock/serialización (1205) de SQL Server. El Domain
        // no referencia Microsoft.Data.SqlClient: "Number" se lee por reflexión y se contrasta el mensaje.
        private static bool EsConflictoDeConcurrencia(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                // Las validaciones del propio domain son Exception "pelada": nunca se traducen.
                if (e.GetType() == typeof(Exception))
                    continue;

                var numero = e.GetType().GetProperty("Number")?.GetValue(e) as int?;
                if (numero is 2627 or 2601 or 1205)
                    return true;

                var mensaje = e.Message ?? string.Empty;
                if (mensaje.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase)
                    || mensaje.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                    || mensaje.Contains("deadlock", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string NormalizarSn(string? valor) => valor?.Trim().ToUpperInvariant() == "S" ? "S" : "N";

        private static string NormalizarNombre(string? nombre)
        {
            var valor = (nombre ?? string.Empty).Trim();
            if (valor.Length == 0)
                throw new Exception("El nombre es obligatorio.");
            if (valor.Length > LongitudMaximaNombre)
                throw new Exception($"El nombre no puede superar {LongitudMaximaNombre} caracteres.");
            return valor;
        }

        private static string? NormalizarEmail(string? email)
        {
            var valor = (email ?? string.Empty).Trim();
            if (valor.Length == 0)
                return null;
            if (valor.Length > LongitudMaximaEmail)
                throw new Exception($"El correo electrónico no puede superar {LongitudMaximaEmail} caracteres.");
            if (!EmailRegex.IsMatch(valor))
                throw new Exception("El correo electrónico no es válido.");
            return valor;
        }

        private static void ValidarPassword(string password, string codigo)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new Exception("La contraseña no puede estar vacía ni contener solo espacios.");
            if (password.Length < LongitudMinimaPassword)
                throw new Exception($"La contraseña debe tener al menos {LongitudMinimaPassword} caracteres.");
            if (string.Equals(password, codigo, StringComparison.OrdinalIgnoreCase))
                throw new Exception("La contraseña no puede ser igual al código de usuario.");
        }
        #endregion
    }
}
