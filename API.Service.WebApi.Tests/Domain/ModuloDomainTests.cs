using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ModuloDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Modulo, string>> _repo = new();
        private readonly ModuloDomain _domain;

        public ModuloDomainTests()
        {
            _domain = new ModuloDomain(_repo.Object);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveElCatalogo()
        {
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Artículos", Grupo = "Inventario" },
                new() { Codigo = "Usuarios", Nombre = "Usuarios", Grupo = "Administracion" },
            }.AsAsyncQueryable());

            var resultado = (await (await _domain.ObtenerTodoAsync()).ToListAsync());

            Assert.Equal(2, resultado.Count);
        }
    }
}
