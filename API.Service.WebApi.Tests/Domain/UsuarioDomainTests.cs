using System.Data;
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class UsuarioDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Usuario, int>> _repo = new();
        private readonly Mock<IPasswordHasher<Usuario>> _hasher = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly List<Usuario> _tabla = new();
        private readonly UsuarioDomain _domain;

        public UsuarioDomainTests()
        {
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _tabla.AsAsyncQueryable());
            _repo.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _tabla.FirstOrDefault(u => u.Id == id));
            _repo.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<Usuario>()))
                .Callback<Usuario>(u => _tabla.Add(u))
                .Returns(Task.CompletedTask);
            _hasher.Setup(h => h.HashPassword(It.IsAny<Usuario>(), It.IsAny<string>())).Returns((Usuario _, string p) => "HASH(" + p + ")");
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>())).Returns<Func<Task<int>>, IsolationLevel>((f, _) => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<IsolationLevel>())).Returns<Func<Task<bool>>, IsolationLevel>((f, _) => f());
            _domain = new UsuarioDomain(_repo.Object, _hasher.Object, _tx.Object);
        }

        private static Usuario Existente(int id, string codigo, string super = "N", string bloqueado = "N", string eliminado = "N") => new()
        {
            Id = id,
            Codigo = codigo,
            Nombre = "Nombre " + codigo,
            Email = codigo + "@x.com",
            Password = "HASH-ORIGINAL-" + codigo,
            SuperUsuario = super,
            Bloqueado = bloqueado,
            Eliminado = eliminado,
            UltimaContra = "2024-01-01",
            LlaveInterna = id * 10
        };

        private static Usuario Nuevo(string codigo = "jperez", string password = "Clave1234", string nombre = "Juan Perez") => new()
        {
            Codigo = codigo,
            Password = password,
            Nombre = nombre
        };

        private static Usuario Cambios(string nombre = "Nuevo Nombre", string? email = null, string? super = "N", string? bloqueado = "N", string? password = null, string? codigo = null) => new()
        {
            Codigo = codigo!,
            Nombre = nombre,
            Email = email,
            SuperUsuario = super,
            Bloqueado = bloqueado,
            Password = password
        };

        private static async Task<string> MensajeDe(Func<Task> accion)
        {
            var ex = await Assert.ThrowsAsync<Exception>(accion);
            return ex.Message;
        }

        // ---------- Insertar ----------

        [Fact]
        public async Task Insertar_TablaVacia_AsignaId1()
        {
            var id = await _domain.InsertarAsync(Nuevo());

            Assert.Equal(1, id);
            Assert.Equal(1, Assert.Single(_tabla).Id);
        }

        [Fact]
        public async Task Insertar_ConMaxId7_AsignaId8()
        {
            _tabla.Add(Existente(3, "a"));
            _tabla.Add(Existente(7, "b"));
            _tabla.Add(Existente(5, "c"));

            var id = await _domain.InsertarAsync(Nuevo("nuevo"));

            Assert.Equal(8, id);
            Assert.Equal(8, _tabla.Single(u => u.Codigo == "nuevo").Id);
        }

        [Fact]
        public async Task Insertar_DosUsuariosSeguidos_NoColisionanEnId()
        {
            var id1 = await _domain.InsertarAsync(Nuevo("uno"));
            var id2 = await _domain.InsertarAsync(Nuevo("dos"));

            Assert.Equal(1, id1);
            Assert.Equal(2, id2);
        }

        [Fact]
        public async Task Insertar_LeeMaxEInsertaDentroDeLaTransaccion()
        {
            await _domain.InsertarAsync(Nuevo());

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), IsolationLevel.Serializable), Times.Once);
        }

        [Fact]
        public async Task Insertar_NormalizaYHasheaYMarcaNoEliminado()
        {
            var obj = Nuevo("  jperez  ", "Clave1234", "  Juan Perez  ");
            obj.Email = " juan@correo.com ";
            obj.SuperUsuario = "x";
            obj.Bloqueado = null;
            obj.Eliminado = "S"; // el cliente no puede forzarlo

            await _domain.InsertarAsync(obj);

            var u = Assert.Single(_tabla);
            Assert.Equal("jperez", u.Codigo);
            Assert.Equal("Juan Perez", u.Nombre);
            Assert.Equal("juan@correo.com", u.Email);
            Assert.Equal("N", u.SuperUsuario);
            Assert.Equal("N", u.Bloqueado);
            Assert.Equal("N", u.Eliminado);
            Assert.Equal("HASH(Clave1234)", u.Password);
        }

        [Fact]
        public async Task Insertar_NormalizaFlagsSyN()
        {
            var obj = Nuevo();
            obj.SuperUsuario = "s";
            obj.Bloqueado = "S";

            await _domain.InsertarAsync(obj);

            var u = Assert.Single(_tabla);
            Assert.Equal("S", u.SuperUsuario);
            Assert.Equal("S", u.Bloqueado);
        }

        [Fact]
        public async Task Insertar_EmailVacio_SeGuardaNulo()
        {
            var obj = Nuevo();
            obj.Email = "   ";

            await _domain.InsertarAsync(obj);

            Assert.Null(Assert.Single(_tabla).Email);
        }

        [Fact]
        public async Task Insertar_CodigoDuplicado_Falla()
        {
            _tabla.Add(Existente(1, "jperez"));

            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo("jperez")));

            Assert.Equal("Ya existe un usuario con el código: jperez", msg);
            Assert.Single(_tabla);
        }

        [Fact]
        public async Task Insertar_CodigoDeUsuarioEliminado_NoSePuedeReutilizar()
        {
            _tabla.Add(Existente(1, "jperez", eliminado: "S"));

            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo("jperez")));

            Assert.Equal("Ya existe un usuario con el código: jperez", msg);
            Assert.Single(_tabla);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Insertar_CodigoVacio_Falla(string? codigo)
        {
            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo(codigo!)));

            Assert.Equal("El código es obligatorio.", msg);
        }

        [Fact]
        public async Task Insertar_CodigoMayorA25_Falla()
        {
            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo(new string('a', 26))));

            Assert.Equal("El código no puede superar 25 caracteres.", msg);
        }

        [Fact]
        public async Task Insertar_CodigoDe25_Acepta()
        {
            await _domain.InsertarAsync(Nuevo(new string('a', 25)));

            Assert.Single(_tabla);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        public async Task Insertar_NombreVacio_Falla(string? nombre)
        {
            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo(nombre: nombre!)));

            Assert.Equal("El nombre es obligatorio.", msg);
        }

        [Fact]
        public async Task Insertar_NombreMayorA150_Falla()
        {
            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo(nombre: new string('n', 151))));

            Assert.Equal("El nombre no puede superar 150 caracteres.", msg);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Insertar_PasswordVacia_Falla(string? password)
        {
            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo(password: password!)));

            Assert.Equal("La contraseña es obligatoria.", msg);
        }

        [Fact]
        public async Task Insertar_PasswordCorta_Falla()
        {
            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo(password: "Corta12")));

            Assert.Equal("La contraseña debe tener al menos 8 caracteres.", msg);
            Assert.Empty(_tabla);
        }

        [Fact]
        public async Task Insertar_PasswordDe8_Acepta()
        {
            await _domain.InsertarAsync(Nuevo(password: "Clave123"));

            Assert.Single(_tabla);
        }

        [Fact]
        public async Task Insertar_PasswordIgualAlCodigoSinImportarMayusculas_Falla()
        {
            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo("JPerezUsr", "jperezusr")));

            Assert.Equal("La contraseña no puede ser igual al código de usuario.", msg);
        }

        [Theory]
        [InlineData("sin-arroba")]
        [InlineData("a@b")]
        [InlineData("a b@c.com")]
        [InlineData("@c.com")]
        public async Task Insertar_EmailInvalido_Falla(string email)
        {
            var obj = Nuevo();
            obj.Email = email;

            var msg = await MensajeDe(() => _domain.InsertarAsync(obj));

            Assert.Equal("El correo electrónico no es válido.", msg);
        }

        [Fact]
        public async Task Insertar_EmailMayorA75_Falla()
        {
            var obj = Nuevo();
            obj.Email = new string('a', 70) + "@x.com";

            var msg = await MensajeDe(() => _domain.InsertarAsync(obj));

            Assert.Equal("El correo electrónico no puede superar 75 caracteres.", msg);
        }

        // ---------- Actualizar ----------

        [Fact]
        public async Task Actualizar_CambiaSoloLosCamposPermitidos_YConservaPasswordEliminadoUltimaContra()
        {
            var original = Existente(2, "maria");
            _tabla.Add(Existente(1, "admin", super: "S"));
            _tabla.Add(original);

            var ok = await _domain.ActualizarAsync(2, Cambios("Maria Lopez", "maria@nueva.com", "N", "S", password: ""), null);

            Assert.True(ok);
            Assert.Equal("Maria Lopez", original.Nombre);
            Assert.Equal("maria@nueva.com", original.Email);
            Assert.Equal("N", original.SuperUsuario);
            Assert.Equal("S", original.Bloqueado);
            Assert.Equal("HASH-ORIGINAL-maria", original.Password);
            Assert.Equal("N", original.Eliminado);
            Assert.Equal("2024-01-01", original.UltimaContra);
            Assert.Equal(20, original.LlaveInterna);
            Assert.Equal(2, original.Id);
            Assert.Equal("maria", original.Codigo);
            _hasher.Verify(h => h.HashPassword(It.IsAny<Usuario>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_PasswordNula_ConservaElHash()
        {
            var original = Existente(2, "maria");
            _tabla.Add(original);

            await _domain.ActualizarAsync(2, Cambios(password: null), null);

            Assert.Equal("HASH-ORIGINAL-maria", original.Password);
        }

        [Fact]
        public async Task Actualizar_ConPassword_Rehashea()
        {
            var original = Existente(2, "maria");
            _tabla.Add(original);

            await _domain.ActualizarAsync(2, Cambios(password: "OtraClave99"), null);

            Assert.Equal("HASH(OtraClave99)", original.Password);
            Assert.Equal("N", original.Eliminado);
            Assert.Equal("2024-01-01", original.UltimaContra);
        }

        [Fact]
        public async Task Actualizar_PasswordCorta_Falla_YNoCambiaNada()
        {
            var original = Existente(2, "maria");
            _tabla.Add(original);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios("Otro", password: "corta"), null));

            Assert.Equal("La contraseña debe tener al menos 8 caracteres.", msg);
            Assert.Equal("HASH-ORIGINAL-maria", original.Password);
            Assert.Equal("Nombre maria", original.Nombre);
        }

        [Fact]
        public async Task Actualizar_PasswordIgualAlCodigoAlmacenado_Falla()
        {
            _tabla.Add(Existente(2, "mariaLopez"));

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(password: "MARIALOPEZ"), null));

            Assert.Equal("La contraseña no puede ser igual al código de usuario.", msg);
        }

        [Fact]
        public async Task Actualizar_CodigoDistinto_Falla()
        {
            var original = Existente(2, "maria");
            _tabla.Add(original);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(codigo: "otro"), null));

            Assert.Equal("El código de usuario no se puede modificar.", msg);
            Assert.Equal("maria", original.Codigo);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("maria")]
        [InlineData(" maria ")]
        public async Task Actualizar_CodigoVacioOIgual_SeIgnora(string? codigo)
        {
            var original = Existente(2, "maria");
            _tabla.Add(original);

            var ok = await _domain.ActualizarAsync(2, Cambios("Otro", codigo: codigo), null);

            Assert.True(ok);
            Assert.Equal("maria", original.Codigo);
            Assert.Equal("Otro", original.Nombre);
        }

        [Fact]
        public async Task Actualizar_UsuarioInexistente_FallaConUsuarioNoExiste()
        {
            var msg = await MensajeDe(() => _domain.ActualizarAsync(99, Cambios(), null));

            Assert.Equal("El usuario no existe.", msg);
        }

        [Fact]
        public async Task Actualizar_UsuarioEliminado_FallaConUsuarioNoExiste()
        {
            var eliminado = Existente(2, "maria", eliminado: "S");
            _tabla.Add(eliminado);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios("Otro"), null));

            Assert.Equal("El usuario no existe.", msg);
            Assert.Equal("Nombre maria", eliminado.Nombre);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Actualizar_NombreVacio_Falla(string? nombre)
        {
            _tabla.Add(Existente(2, "maria"));

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(nombre!), null));

            Assert.Equal("El nombre es obligatorio.", msg);
        }

        [Fact]
        public async Task Actualizar_EmailInvalido_Falla()
        {
            _tabla.Add(Existente(2, "maria"));

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(email: "no-es-correo"), null));

            Assert.Equal("El correo electrónico no es válido.", msg);
        }

        [Fact]
        public async Task Actualizar_EmailVacio_LoDejaNulo()
        {
            var original = Existente(2, "maria");
            _tabla.Add(original);

            await _domain.ActualizarAsync(2, Cambios(email: ""), null);

            Assert.Null(original.Email);
        }

        // ---------- Eliminar ----------

        [Fact]
        public async Task Eliminar_MarcaEliminadoS_SinQuitarLaFila_NiTocarOtrosCampos()
        {
            var original = Existente(2, "maria", bloqueado: "N");
            _tabla.Add(Existente(1, "admin", super: "S"));
            _tabla.Add(original);

            var ok = await _domain.EliminarAsync(2, null);

            Assert.True(ok);
            Assert.Equal("S", original.Eliminado);
            Assert.Equal("N", original.Bloqueado);
            Assert.Equal("HASH-ORIGINAL-maria", original.Password);
            Assert.Equal(2, _tabla.Count);
            _repo.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_Inexistente_DevuelveFalse()
        {
            Assert.False(await _domain.EliminarAsync(99, null));
        }

        [Fact]
        public async Task Eliminar_YaEliminado_DevuelveFalse()
        {
            _tabla.Add(Existente(2, "maria", eliminado: "S"));

            Assert.False(await _domain.EliminarAsync(2, null));
        }

        // ---------- Listado / lectura ----------

        [Fact]
        public async Task ObtenerTodo_ExcluyeEliminados_PeroIncluyeNulos()
        {
            var sinMarca = Existente(3, "c");
            sinMarca.Eliminado = null;
            _tabla.Add(Existente(1, "a"));
            _tabla.Add(Existente(2, "b", eliminado: "S"));
            _tabla.Add(sinMarca);

            var lista = (await _domain.ObtenerTodoAsync()).ToList();

            Assert.Equal(new[] { 1, 3 }, lista.Select(u => u.Id).ToArray());
        }

        [Fact]
        public async Task ObtenerPorId_UsuarioEliminado_DevuelveNulo()
        {
            _tabla.Add(Existente(2, "b", eliminado: "S"));
            _tabla.Add(Existente(3, "c"));

            Assert.Null(await _domain.ObtenerAsync(2));
            Assert.NotNull(await _domain.ObtenerAsync(3));
        }

        [Fact]
        public async Task ObtenerPorCodigo_IncluyeEliminados()
        {
            _tabla.Add(Existente(2, "b", eliminado: "S"));

            var u = await _domain.ObtenerAsync("b");

            Assert.NotNull(u);
            Assert.Equal("S", u!.Eliminado);
        }

        [Fact]
        public async Task ObtenerContengaNombre_ExcluyeEliminados()
        {
            _tabla.Add(Existente(1, "ana"));
            _tabla.Add(Existente(2, "anabel", eliminado: "S"));
            _tabla.Add(Existente(3, "pepe"));

            var lista = (await _domain.ObtenerContengaNombreAsync("ana")).ToList();

            Assert.Equal(new[] { 1 }, lista.Select(u => u.Id).ToArray());
        }

        // ---------- Auto-protección ----------

        [Fact]
        public async Task Eliminar_ASiMismo_Falla()
        {
            var yo = Existente(2, "maria");
            _tabla.Add(Existente(1, "admin", super: "S"));
            _tabla.Add(yo);

            var msg = await MensajeDe(() => _domain.EliminarAsync(2, 2));

            Assert.Equal("No puede eliminar su propio usuario.", msg);
            Assert.Equal("N", yo.Eliminado);
        }

        [Fact]
        public async Task Eliminar_OtroUsuarioConClaimDistinto_Funciona()
        {
            _tabla.Add(Existente(1, "admin", super: "S"));
            var otro = Existente(2, "maria");
            _tabla.Add(otro);

            Assert.True(await _domain.EliminarAsync(2, 1));
            Assert.Equal("S", otro.Eliminado);
        }

        [Fact]
        public async Task Actualizar_BloquearseASiMismo_Falla()
        {
            var yo = Existente(2, "maria");
            _tabla.Add(yo);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(bloqueado: "S"), 2));

            Assert.Equal("No puede bloquear su propio usuario.", msg);
            Assert.Equal("N", yo.Bloqueado);
        }

        [Fact]
        public async Task Actualizar_QuitarseElFlagSuperUsuarioASiMismo_Falla()
        {
            var yo = Existente(2, "maria", super: "S");
            _tabla.Add(Existente(1, "admin", super: "S"));
            _tabla.Add(yo);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(super: "N"), 2));

            Assert.Equal("No puede quitarse a sí mismo el permiso de superusuario.", msg);
            Assert.Equal("S", yo.SuperUsuario);
        }

        [Fact]
        public async Task Actualizar_ASiMismoSinCambiarFlags_Funciona()
        {
            var yo = Existente(2, "maria", super: "S");
            _tabla.Add(yo);

            var ok = await _domain.ActualizarAsync(2, Cambios("Nuevo", super: "S"), 2);

            Assert.True(ok);
            Assert.Equal("Nuevo", yo.Nombre);
        }

        [Fact]
        public async Task SinClaimDeUsuarioActual_SeOmiteLaAutoProteccion()
        {
            var yo = Existente(2, "maria");
            _tabla.Add(yo);

            Assert.True(await _domain.ActualizarAsync(2, Cambios(bloqueado: "S"), null));
            Assert.Equal("S", yo.Bloqueado);
        }

        // ---------- Último superusuario activo ----------

        [Fact]
        public async Task Eliminar_UltimoSuperUsuarioActivo_Falla()
        {
            var unico = Existente(1, "admin", super: "S");
            _tabla.Add(unico);
            _tabla.Add(Existente(2, "otroSuperBloqueado", super: "S", bloqueado: "S"));
            _tabla.Add(Existente(3, "otroSuperEliminado", super: "S", eliminado: "S"));
            _tabla.Add(Existente(4, "normal"));

            var msg = await MensajeDe(() => _domain.EliminarAsync(1, 4));

            Assert.Equal("Debe existir al menos un superusuario activo.", msg);
            Assert.Equal("N", unico.Eliminado);
        }

        [Fact]
        public async Task Eliminar_SuperUsuarioHabiendoOtroActivo_Funciona()
        {
            var uno = Existente(1, "admin", super: "S");
            _tabla.Add(uno);
            _tabla.Add(Existente(2, "admin2", super: "S"));

            Assert.True(await _domain.EliminarAsync(1, 2));
            Assert.Equal("S", uno.Eliminado);
        }

        [Fact]
        public async Task Actualizar_BloquearAlUltimoSuperUsuarioActivo_Falla()
        {
            var unico = Existente(1, "admin", super: "S");
            _tabla.Add(unico);
            _tabla.Add(Existente(4, "normal"));

            var msg = await MensajeDe(() => _domain.ActualizarAsync(1, Cambios(super: "S", bloqueado: "S"), 4));

            Assert.Equal("Debe existir al menos un superusuario activo.", msg);
            Assert.Equal("N", unico.Bloqueado);
        }

        [Fact]
        public async Task Actualizar_QuitarFlagAlUltimoSuperUsuarioActivo_Falla()
        {
            var unico = Existente(1, "admin", super: "S");
            _tabla.Add(unico);
            _tabla.Add(Existente(4, "normal"));

            var msg = await MensajeDe(() => _domain.ActualizarAsync(1, Cambios(super: "N"), 4));

            Assert.Equal("Debe existir al menos un superusuario activo.", msg);
            Assert.Equal("S", unico.SuperUsuario);
        }

        [Fact]
        public async Task Actualizar_QuitarFlagASuperUsuarioHabiendoOtroActivo_Funciona()
        {
            var uno = Existente(1, "admin", super: "S");
            _tabla.Add(uno);
            _tabla.Add(Existente(2, "admin2", super: "S"));

            Assert.True(await _domain.ActualizarAsync(1, Cambios(super: "N"), 2));
            Assert.Equal("N", uno.SuperUsuario);
        }

        [Fact]
        public async Task Actualizar_SuperUsuarioUnicoSinCambiarFlags_Funciona()
        {
            var unico = Existente(1, "admin", super: "S");
            _tabla.Add(unico);

            Assert.True(await _domain.ActualizarAsync(1, Cambios("Renombrado", super: "S"), 4));
            Assert.Equal("Renombrado", unico.Nombre);
        }

        [Fact]
        public async Task Eliminar_UsuarioNoSuperUsuario_NoExigeOtroSuperUsuario()
        {
            var normal = Existente(4, "normal");
            _tabla.Add(normal);

            Assert.True(await _domain.EliminarAsync(4, 1));
        }

        // ---------- Aislamiento Serializable ----------

        [Fact]
        public async Task Actualizar_UsaTransaccionSerializable()
        {
            _tabla.Add(Existente(2, "maria"));

            await _domain.ActualizarAsync(2, Cambios("Otro"), null);

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>(), IsolationLevel.Serializable), Times.Once);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_UsaTransaccionSerializable()
        {
            _tabla.Add(Existente(2, "maria"));

            await _domain.EliminarAsync(2, null);

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>(), IsolationLevel.Serializable), Times.Once);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_NoUsaLaSobrecargaSinNivel()
        {
            await _domain.InsertarAsync(Nuevo());

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
        }

        // ---------- Auto-protección: 's' minúscula / nulos ----------

        [Fact]
        public async Task Actualizar_BloquearseASiMismoConSMinuscula_Falla()
        {
            var yo = Existente(2, "maria");
            _tabla.Add(yo);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(bloqueado: "s"), 2));

            Assert.Equal("No puede bloquear su propio usuario.", msg);
            Assert.Equal("N", yo.Bloqueado);
        }

        [Fact]
        public async Task Actualizar_QuitarseSuperUsuarioASiMismoConFlagNulo_Falla()
        {
            var yo = Existente(2, "maria", super: "S");
            _tabla.Add(Existente(1, "admin", super: "S"));
            _tabla.Add(yo);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(super: null), 2));

            Assert.Equal("No puede quitarse a sí mismo el permiso de superusuario.", msg);
            Assert.Equal("S", yo.SuperUsuario);
        }

        [Fact]
        public async Task Actualizar_QuitarseSuperUsuarioASiMismoConValorRaro_Falla()
        {
            var yo = Existente(2, "maria", super: "S");
            _tabla.Add(Existente(1, "admin", super: "S"));
            _tabla.Add(yo);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios(super: "x"), 2));

            Assert.Equal("No puede quitarse a sí mismo el permiso de superusuario.", msg);
        }

        [Fact]
        public async Task Actualizar_ASiMismoConSMinusculaMantieneSuperUsuario_Funciona()
        {
            var yo = Existente(2, "maria", super: "S");
            _tabla.Add(yo);

            var ok = await _domain.ActualizarAsync(2, Cambios("Nuevo", super: "s"), 2);

            Assert.True(ok);
            Assert.Equal("S", yo.SuperUsuario);
        }

        [Fact]
        public async Task Actualizar_BloqueadoNuloSeTrataComoNoBloqueado()
        {
            var otro = Existente(2, "maria", bloqueado: "S");
            _tabla.Add(otro);

            Assert.True(await _domain.ActualizarAsync(2, Cambios(bloqueado: null), 1));
            Assert.Equal("N", otro.Bloqueado);
        }

        // ---------- Contraseña solo con espacios ----------

        [Theory]
        [InlineData(" ")]
        [InlineData("        ")]
        [InlineData("\t\t\t\t\t\t\t\t")]
        public async Task Insertar_PasswordSoloEspacios_Falla(string password)
        {
            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo(password: password)));

            Assert.Equal("La contraseña no puede estar vacía ni contener solo espacios.", msg);
            Assert.Empty(_tabla);
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("        ")]
        public async Task Actualizar_PasswordSoloEspacios_Falla_YNoCambiaNada(string password)
        {
            var original = Existente(2, "maria");
            _tabla.Add(original);

            var msg = await MensajeDe(() => _domain.ActualizarAsync(2, Cambios("Otro", password: password), null));

            Assert.Equal("La contraseña no puede estar vacía ni contener solo espacios.", msg);
            Assert.Equal("HASH-ORIGINAL-maria", original.Password);
            Assert.Equal("Nombre maria", original.Nombre);
        }

        // ---------- Código almacenado con espacios (legado) ----------

        [Fact]
        public async Task Actualizar_CodigoAlmacenadoConEspacios_ComparaContraElCodigoRecortado()
        {
            var original = Existente(2, " maria ");
            _tabla.Add(original);

            var ok = await _domain.ActualizarAsync(2, Cambios("Otro", codigo: "maria"), null);

            Assert.True(ok);
            Assert.Equal("Otro", original.Nombre);
        }

        // ---------- Concurrencia: mensaje amable ----------

        private sealed class FalloSqlSimulado : Exception
        {
            public FalloSqlSimulado(int numero, string mensaje) : base(mensaje) { Number = numero; }
            public int Number { get; }
        }

        private const string MensajeConcurrencia = "No se pudo crear el usuario porque otra operación se estaba ejecutando. Intente nuevamente.";

        private void LaTransaccionLanza(Exception ex) =>
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>())).ThrowsAsync(ex);

        [Fact]
        public async Task Insertar_ViolacionDePkEnLaTransaccion_DevuelveMensajeAmable()
        {
            LaTransaccionLanza(new InvalidOperationException("Violation of PRIMARY KEY constraint 'pk_usuario'. Cannot insert duplicate key in object 'dbo.Usuario'."));

            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo()));

            Assert.Equal(MensajeConcurrencia, msg);
        }

        [Fact]
        public async Task Insertar_ViolacionDePkEnExcepcionInterna_DevuelveMensajeAmable()
        {
            LaTransaccionLanza(new InvalidOperationException(
                "An error occurred while saving the entity changes. See the inner exception for details.",
                new InvalidOperationException("Violation of PRIMARY KEY constraint 'pk_usuario'.")));

            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo()));

            Assert.Equal(MensajeConcurrencia, msg);
        }

        [Theory]
        [InlineData(2627)]
        [InlineData(2601)]
        [InlineData(1205)]
        public async Task Insertar_ErrorSqlConNumeroDeDuplicadoODeadlock_DevuelveMensajeAmable(int numero)
        {
            LaTransaccionLanza(new InvalidOperationException("wrapper", new FalloSqlSimulado(numero, "mensaje sin pistas")));

            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo()));

            Assert.Equal(MensajeConcurrencia, msg);
        }

        [Fact]
        public async Task Insertar_Deadlock_DevuelveMensajeAmable()
        {
            LaTransaccionLanza(new InvalidOperationException("Transaction was deadlocked on lock resources with another process and has been chosen as the deadlock victim."));

            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo()));

            Assert.Equal(MensajeConcurrencia, msg);
        }

        [Fact]
        public async Task Insertar_OtroErrorDeInfraestructura_NoSeTraduce()
        {
            LaTransaccionLanza(new InvalidOperationException("Timeout expired."));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _domain.InsertarAsync(Nuevo()));

            Assert.Equal("Timeout expired.", ex.Message);
        }

        [Fact]
        public async Task Insertar_ErrorDeValidacionDelDomain_NoSeTraduce()
        {
            _tabla.Add(Existente(1, "jperez"));

            var msg = await MensajeDe(() => _domain.InsertarAsync(Nuevo("jperez")));

            Assert.Equal("Ya existe un usuario con el código: jperez", msg);
        }
    }
}
