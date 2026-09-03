using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class MovimientoInventarioApplication : IMovimientoInventarioApplication
    {
        private readonly IMovimientoInventarioDomain _domain;
        private readonly IMapper _mapper;
        private readonly INumeracionDocumentoDomain _numeracion;

        public MovimientoInventarioApplication(IMovimientoInventarioDomain domain, IMapper mapper, INumeracionDocumentoDomain numeracion)
        {
            _domain = domain;
            _mapper = mapper;
            _numeracion = numeracion;
        }

        public async Task<Respuesta<IEnumerable<MovimientoInventarioDTO>>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta)
        {
            var respuesta = new Respuesta<IEnumerable<MovimientoInventarioDTO>>();
            try
            {
                var lista = await _domain.ObtenerPorArticuloAsync(codArticulo, almacen, desde, hasta);
                var dtos = _mapper.Map<List<MovimientoInventarioDTO>>(lista);

                var alias = (await (await _numeracion.ObtenerTodoAsync())
                        .Where(n => n.SubTipoDoc == "--" && n.DocAlias != null)
                        .ToListAsync())
                    .ToDictionary(n => n.CodigoObj, n => n.DocAlias!);

                foreach (var dto in dtos)
                    dto.TipoDocNombre = alias.TryGetValue(dto.TipoDoc, out var nombre) ? nombre : dto.TipoDoc;

                respuesta.Dato = dtos;
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }
    }
}
