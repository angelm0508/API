using API.Application.DTO;
using API.Application.DTO.articulo.almacen;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class ArticuloAlmacenApplication : IArticuloAlmacenApplication
    {
        public const string AlmacenSinCodigo = "Todas las filas deben indicar el código del almacén.";

        private readonly IArticuloAlmacenDomain _domain;

        public ArticuloAlmacenApplication(IArticuloAlmacenDomain domain)
        {
            _domain = domain;
        }

        #region async methods
        public async Task<Respuesta<IEnumerable<ArticuloAlmacenDTO>>> ObtenerAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloAlmacenDTO>>();
            try
            {
                var filas = await _domain.ObtenerFilasAsync(codigo);
                respuesta.Dato = filas.Select(f => new ArticuloAlmacenDTO
                {
                    CodAlmacen = f.CodAlmacen,
                    NombreAlmacen = f.NombreAlmacen,
                    Bloqueado = f.Bloqueado,
                    EnStock = f.EnStock,
                    Comprometido = f.Comprometido,
                    Pedido = f.Pedido,
                    Disponible = f.Disponible,
                    Minimo = f.Minimo,
                    Maximo = f.Maximo,
                    NivelNecesario = f.NivelNecesario,
                    Estado = f.Estado
                }).ToList();
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }

        public async Task<Respuesta<bool>> GuardarAsync(string codigo, IEnumerable<ArticuloAlmacenGuardarDTO>? filas)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var configuracion = new List<ArticuloAlmacenConfig>();
                foreach (var fila in filas ?? Enumerable.Empty<ArticuloAlmacenGuardarDTO>())
                {
                    // El dominio no admite almacenes sin código: se rechaza aquí con un mensaje claro.
                    if (fila is null || string.IsNullOrWhiteSpace(fila.CodAlmacen))
                    {
                        respuesta.Mensaje = AlmacenSinCodigo;
                        return respuesta;
                    }
                    configuracion.Add(new ArticuloAlmacenConfig(fila.CodAlmacen.Trim(), fila.Minimo, fila.Maximo, fila.Bloqueado));
                }

                await _domain.GuardarAsync(codigo, configuracion);
                respuesta.Dato = true;
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<AlmacenBajoMinimoDTO>>> ObtenerBajoMinimoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenBajoMinimoDTO>>();
            try
            {
                var filas = await _domain.ObtenerBajoMinimoAsync();
                respuesta.Dato = filas.Select(f => new AlmacenBajoMinimoDTO
                {
                    CodArticulo = f.CodArticulo,
                    NombreArticulo = f.NombreArticulo,
                    CodAlmacen = f.CodAlmacen,
                    NombreAlmacen = f.NombreAlmacen,
                    EnStock = f.EnStock,
                    Minimo = f.Minimo
                }).ToList();
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }
        #endregion
    }
}
