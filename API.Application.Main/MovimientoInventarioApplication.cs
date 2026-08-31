using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class MovimientoInventarioApplication : IMovimientoInventarioApplication
    {
        private readonly IMovimientoInventarioDomain _domain;
        private readonly IMapper _mapper;

        public MovimientoInventarioApplication(IMovimientoInventarioDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        public async Task<Respuesta<IEnumerable<MovimientoInventarioDTO>>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta)
        {
            var respuesta = new Respuesta<IEnumerable<MovimientoInventarioDTO>>();
            try
            {
                var lista = await _domain.ObtenerPorArticuloAsync(codArticulo, almacen, desde, hasta);
                respuesta.Dato = _mapper.Map<IEnumerable<MovimientoInventarioDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }
    }
}
