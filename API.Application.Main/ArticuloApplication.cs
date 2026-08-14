using API.Application.DTO;
using API.Application.DTO.articulo.articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class ArticuloApplication : IArticuloApplication
    {
        private readonly IArticuloDomain _productoDomain;
        private readonly IMapper _mapper;

        public ArticuloApplication(IArticuloDomain productoDomain, IMapper mapper)
        {
            _productoDomain = productoDomain;
            _mapper = mapper;
        }


        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(ArticuloCrearDTO obj)
        {
            var respuseta = new Respuesta<bool>();

            try
            {
                var productoo = _mapper.Map<Articulo>(obj);

                respuseta.Dato = await _productoDomain
                                            .InsertarAsync(productoo);

                if (respuseta.Dato)
                {
                    respuseta.Resultado = true;
                    respuseta.Mensaje = "Registro agregado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuseta.Mensaje = $"{ex.Message} / {ex.InnerException}";
            }
            return respuseta;
        }
        public async Task<Respuesta<bool>> ActualizarAsync(string sku, ArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var producto = _mapper.Map<Articulo>(obj);
                respuesta.Dato = await _productoDomain.ActualizarAsync(sku, producto);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        public async Task<Respuesta<bool>> EliminarAsync(string sku)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _productoDomain.EliminarAsync(sku);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = $"{ex.Message} \n {ex.InnerException}";
            }
            return respuesta;
        }

        public async Task<Respuesta<ArticuloDTO>> ObtenerPorCodigoAsync(string sku)
        {
            var respuesta = new Respuesta<ArticuloDTO>();
            try
            {
                var producto = await _productoDomain
                                        .ObtenerPorCodigoAsync(sku);

                respuesta.Dato = _mapper.Map<ArticuloDTO>(producto);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<ArticuloDTO>> ObtenerPorNombreAsync(string name)
        {
            var respuesta = new Respuesta<ArticuloDTO>();
            try
            {
                var producto = await _productoDomain
                                        .ObtenerPorNombreAsync(name);

                respuesta.Dato = _mapper.Map<ArticuloDTO>(producto);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>>();
            try
            {
                var producto = await _productoDomain.ObtenerTodoAsync();

                respuesta.Dato = _mapper.Map<IEnumerable<ArticuloDTO>>(producto);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        /*
        public async Task<Respuesta<PagedList<productoDTO>>> GetAllWithPagingAsync(PaginationParametersDTO paginationParametersDTO)
        {
            var respuesta = new Respuesta<PagedList<productoDTO>>();
            try
            {
                var productos = await _productoDomain.GetAllWithPagingAsync();

                IEnumerable<productoDTO> productosIE = _mapper.Map<IEnumerable<productoDTO>>(await productos.ToListAsync());

                respuesta.Dato = PagedList<productoDTO>.ToPagedList(productosIE, paginationParametersDTO.PageNumber, paginationParametersDTO.PageSize);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";

            }
            catch (Exception ex)
            {
                respuesta.Mensaje = $"{ex.Message}";
            }

            return respuesta;
        }
        */

        public async Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerContenganNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>>();
            try
            {
                var producto = await _productoDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<ArticuloDTO>>(producto);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerContenganCodigoAsync(string sku)
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>>();

            try
            {
                var productos = await _productoDomain.ObtenerContengaCodigoAsync(sku);

                respuesta.Dato = _mapper.Map<IEnumerable<ArticuloDTO>>(productos);

                if (respuesta.Dato != null)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Consulta realizada correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        #endregion
    }
}
