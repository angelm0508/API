using API.Application.DTO.articulo.articulo;
using API.Application.DTO.articulo.grupo_articulo;
using API.Application.DTO.articulo.fabricante_articulo;
using API.Application.DTO.articulo.medida_articulo;
using API.Application.DTO.articulo.grupo_medida_articulo;
using API.Application.DTO.articulo.grupo_medida_det_articulo;
using API.Application.DTO.articulo.grupo_sn;
using API.Application.DTO.precio.listado_precio;
using API.Application.DTO.numeracion.numeracion_documento_det;
using API.Application.DTO.usuario.usuario;
using API.Application.DTO.cotizacion;
using API.Application.DTO.pais;
using API.Application.DTO.departamento;
using API.Application.DTO.municipio;
using API.Application.DTO.almacen;
using API.Application.DTO.socioNegocio;
using API.Application.DTO.direccionSocioNegocio;
using API.Application.DTO.numeracionDocumento;
using API.Application.DTO.moneda;
using API.Domain.Entity.Models;
using AutoMapper;

namespace API.Transversal.Mapper
{
    public class PerfilMapeo : Profile
    {
        public PerfilMapeo()
        {
            CreateMap<Articulo, ArticuloDTO>();
            CreateMap<ArticuloCrearDTO, Articulo>();
            CreateMap<ArticuloActualizarDTO, Articulo>();

            CreateMap<GrupoArticulo, GrupoArticuloDTO>();
            CreateMap<GrupoArticuloCrearDTO, GrupoArticulo>();
            CreateMap<GrupoArticuloActualizarDTO, GrupoArticulo>();

            // Pais
            CreateMap<Pai, PaisDTO>();
            CreateMap<PaisCrearDTO, Pai>();
            CreateMap<PaisActualizarDTO, Pai>();

            // Departamento
            CreateMap<Departamento, DepartamentoDTO>();
            CreateMap<DepartamentoCrearDTO, Departamento>();
            CreateMap<DepartamentoActualizarDTO, Departamento>();

            // Municipio
            CreateMap<Municipio, MunicipioDTO>();
            CreateMap<MunicipioCrearDTO, Municipio>();
            CreateMap<MunicipioActualizarDTO, Municipio>();

            // Almacen
            CreateMap<Almacen, AlmacenDTO>();
            CreateMap<AlmacenCrearDTO, Almacen>();
            CreateMap<AlmacenActualizarDTO, Almacen>();

            // SocioNegocio
            CreateMap<SocioNegocio, SocioNegocioDTO>();
            CreateMap<SocioNegocioCrearDTO, SocioNegocio>();
            CreateMap<SocioNegocioActualizarDTO, SocioNegocio>();

            // DireccionSocioNegocio
            CreateMap<DireccionSocioNegocio, DireccionSocioNegocioDTO>();
            CreateMap<DireccionSocioNegocioCrearDTO, DireccionSocioNegocio>();
            CreateMap<DireccionSocioNegocioActualizarDTO, DireccionSocioNegocio>();

            // NumeracionDocumento
            CreateMap<NumeracionDocumento, NumeracionDocumentoDTO>();
            CreateMap<NumeracionDocumentoCrearDTO, NumeracionDocumento>();
            CreateMap<NumeracionDocumentoActualizarDTO, NumeracionDocumento>();

            // Moneda
            CreateMap<Monedum, MonedaDTO>();
            CreateMap<MonedaCrearDTO, Monedum>();
            CreateMap<MonedaActualizarDTO, Monedum>();

            // FabricanteArticulo
            CreateMap<FabricanteArticulo, FabricanteArticuloDTO>();
            CreateMap<FabricanteArticuloCrearDTO, FabricanteArticulo>();
            CreateMap<FabricanteArticuloActualizarDTO, FabricanteArticulo>();

            // MedidaArticulo
            CreateMap<MedidaArticulo, MedidaArticuloDTO>();
            CreateMap<MedidaArticuloCrearDTO, MedidaArticulo>();
            CreateMap<MedidaArticuloActualizarDTO, MedidaArticulo>();

            // GrupoMedidaArticulo
            CreateMap<GrupoMedidaArticulo, GrupoMedidaArticuloDTO>();
            CreateMap<GrupoMedidaArticuloCrearDTO, GrupoMedidaArticulo>();
            CreateMap<GrupoMedidaArticuloActualizarDTO, GrupoMedidaArticulo>();

            // GrupoMedidaDetArticulo
            CreateMap<GrupoMedidaDetArticulo, GrupoMedidaDetArticuloDTO>();
            CreateMap<GrupoMedidaDetArticuloCrearDTO, GrupoMedidaDetArticulo>();
            CreateMap<GrupoMedidaDetArticuloActualizarDTO, GrupoMedidaDetArticulo>();

            // GrupoSn
            CreateMap<GrupoSn, GrupoSnDTO>();
            CreateMap<GrupoSnCrearDTO, GrupoSn>();
            CreateMap<GrupoSnActualizarDTO, GrupoSn>();

            // ListadoPrecio
            CreateMap<ListadoPrecio, ListadoPrecioDTO>();
            CreateMap<ListadoPrecioCrearDTO, ListadoPrecio>();
            CreateMap<ListadoPrecioActualizarDTO, ListadoPrecio>();

            // NumeracionDocumentoDet
            CreateMap<NumeracionDocumentoDet, NumeracionDocumentoDetDTO>();
            CreateMap<NumeracionDocumentoDetCrearDTO, NumeracionDocumentoDet>();
            CreateMap<NumeracionDocumentoDetActualizarDTO, NumeracionDocumentoDet>();

            // Usuario (el hash de Password nunca se expone en las respuestas de la API)
            CreateMap<Usuario, UsuarioDTO>()
                .ForMember(d => d.Password, opt => opt.Ignore());
            CreateMap<UsuarioCrearDTO, Usuario>();
            CreateMap<UsuarioActualizarDTO, Usuario>();

            // Cotizacion
            CreateMap<Cotizacion, CotizacionDTO>();
            CreateMap<CotizacionCrearDTO, Cotizacion>();
            CreateMap<CotizacionActualizarDTO, Cotizacion>();
        }
    }
}