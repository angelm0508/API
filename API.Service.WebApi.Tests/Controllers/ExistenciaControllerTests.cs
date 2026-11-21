using API.Application.DTO;
using API.Application.DTO.inventario;
using System.Reflection;
using API.Application.Interface;
using API.Domain.Interface;
using API.Domain.Interface.Existencias;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ExistenciaControllerTests
    {
        private readonly Mock<IExistenciaApplication> _app = new();
        private readonly ExistenciaController _controller;

        public ExistenciaControllerTests() => _controller = new ExistenciaController(_app.Object);

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_ConFiltros()
        {
            var resp = new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = true, Dato = new List<ExistenciaArticuloDTO>() };
            _app.Setup(a => a.ObtenerTodoAsync("ART1", "01")).ReturnsAsync(resp);

            var r = await _controller.ObtenerTodo("ART1", "01");

            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
            _app.Verify(a => a.ObtenerTodoAsync("ART1", "01"), Times.Once);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _app.Setup(a => a.ObtenerTodoAsync(null, null)).ReturnsAsync(new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = false });
            var r = await _controller.ObtenerTodo(null, null);
            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveOk()
        {
            var resp = new Respuesta<ExistenciaArticuloDTO> { Resultado = true, Dato = new ExistenciaArticuloDTO { CodArticulo = "ART1", CodAlmacen = "01" } };
            _app.Setup(a => a.ObtenerAsync("ART1", "01")).ReturnsAsync(resp);
            var r = await _controller.Obtener("ART1", "01");
            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorArticulo_DevuelveOk()
        {
            var resp = new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = true, Dato = new List<ExistenciaArticuloDTO>() };
            _app.Setup(a => a.ObtenerPorArticuloAsync("ART1")).ReturnsAsync(resp);
            var r = await _controller.ObtenerPorArticulo("ART1");
            Assert.IsType<OkObjectResult>(r.Result);
        }

        [Fact]
        public void RecalcularCompromisos_EsPostConRutaYPermisoDeActualizarExistencias()
        {
            var metodo = typeof(ExistenciaController).GetMethod("RecalcularCompromisos")!;

            var http = metodo.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(http);
            Assert.Equal("RecalcularCompromisos", http!.Template);

            var attr = metodo.GetCustomAttribute<RequierePermisoAttribute>();
            Assert.NotNull(attr);
            var campoModulo = typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var campoAccion = typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.Equal("Existencias", (string)campoModulo.GetValue(attr)!);
            Assert.True(Accion.Actualizar == (Accion)campoAccion.GetValue(attr)!);
        }

        [Fact]
        public async Task RecalcularCompromisos_DevuelveOk()
        {
            var resp = new Respuesta<ResultadoRecalculo> { Resultado = true, Dato = new ResultadoRecalculo(5, 1) };
            _app.Setup(a => a.RecalcularCompromisosAsync()).ReturnsAsync(resp);

            var r = await _controller.RecalcularCompromisos();

            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
        }

        [Fact]
        public async Task RecalcularCompromisos_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var resp = new Respuesta<ResultadoRecalculo> { Resultado = false, Mensaje = "falla" };
            _app.Setup(a => a.RecalcularCompromisosAsync()).ReturnsAsync(resp);

            var r = await _controller.RecalcularCompromisos();

            var bad = Assert.IsType<BadRequestObjectResult>(r.Result);
            Assert.Same(resp, bad.Value);
        }
    }
}
