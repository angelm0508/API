using API.Application.DTO.articulo.articulo;
using API.Application.DTO.articulo.grupo_articulo;
using API.Application.DTO.articulo.fabricante_articulo;
using API.Application.DTO.articulo.unidad_medida_articulo;
using API.Application.DTO.articulo.grupo_unidad_medida_articulo;
using API.Application.DTO.articulo.grupo_unidad_medida_det_articulo;
using API.Application.DTO.articulo.grupo_sn;
using API.Application.DTO.precio.listado_precio;
using API.Application.DTO.numeracion.numeracion_documento_det;
using API.Application.DTO.usuario.usuario;
using API.Application.DTO.cotizacion;
using API.Application.DTO.entrega;
using API.Application.DTO.pedido;
using API.Application.DTO.pedidoCompra;
using API.Application.DTO.entregaCompra;
using API.Application.DTO.facturaCompra;
using API.Application.DTO.factura;
using API.Application.DTO.impuesto;
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

            // UnidadMedidaArticulo
            CreateMap<UnidadMedidaArticulo, UnidadMedidaArticuloDTO>();
            CreateMap<UnidadMedidaArticuloCrearDTO, UnidadMedidaArticulo>();
            CreateMap<UnidadMedidaArticuloActualizarDTO, UnidadMedidaArticulo>();

            // GrupoUnidadMedidaArticulo
            CreateMap<GrupoUnidadMedidaArticulo, GrupoUnidadMedidaArticuloDTO>();
            CreateMap<GrupoUnidadMedidaArticuloCrearDTO, GrupoUnidadMedidaArticulo>();
            CreateMap<GrupoUnidadMedidaArticuloActualizarDTO, GrupoUnidadMedidaArticulo>();

            // GrupoUnidadMedidaDetArticulo
            CreateMap<GrupoUnidadMedidaDetArticulo, GrupoUnidadMedidaDetArticuloDTO>();
            CreateMap<GrupoUnidadMedidaDetArticuloCrearDTO, GrupoUnidadMedidaDetArticulo>();
            CreateMap<GrupoUnidadMedidaDetArticuloActualizarDTO, GrupoUnidadMedidaDetArticulo>();

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

            // CotizacionDetalle
            CreateMap<CotizacionDetalle, CotizacionDetalleDTO>();
            CreateMap<CotizacionDetalleCrearDTO, CotizacionDetalle>();
            CreateMap<CotizacionDetalleActualizarDTO, CotizacionDetalle>();

            // Entrega
            CreateMap<Entrega, EntregaDTO>();
            CreateMap<EntregaCrearDTO, Entrega>();
            CreateMap<EntregaActualizarDTO, Entrega>();

            // EntregaDetalle
            CreateMap<EntregaDetalle, EntregaDetalleDTO>();
            CreateMap<EntregaDetalleCrearDTO, EntregaDetalle>();
            CreateMap<EntregaDetalleActualizarDTO, EntregaDetalle>();

            // EntregaCompra
            CreateMap<EntregaCompra, EntregaCompraDTO>();
            CreateMap<EntregaCompraCrearDTO, EntregaCompra>();
            CreateMap<EntregaCompraActualizarDTO, EntregaCompra>();

            // EntregaCompraDetalle
            CreateMap<EntregaCompraDetalle, EntregaCompraDetalleDTO>();
            CreateMap<EntregaCompraDetalleCrearDTO, EntregaCompraDetalle>();
            CreateMap<EntregaCompraDetalleActualizarDTO, EntregaCompraDetalle>();

            // FacturaCompra
            CreateMap<FacturaCompra, FacturaCompraDTO>();
            CreateMap<FacturaCompraCrearDTO, FacturaCompra>();
            CreateMap<FacturaCompraActualizarDTO, FacturaCompra>();

            // FacturaCompraDetalle
            CreateMap<FacturaCompraDetalle, FacturaCompraDetalleDTO>();
            CreateMap<FacturaCompraDetalleCrearDTO, FacturaCompraDetalle>();
            CreateMap<FacturaCompraDetalleActualizarDTO, FacturaCompraDetalle>();

            // Factura
            CreateMap<Factura, FacturaDTO>();
            CreateMap<FacturaCrearDTO, Factura>();
            CreateMap<FacturaActualizarDTO, Factura>();

            // FacturaDetalle
            CreateMap<FacturaDetalle, FacturaDetalleDTO>();
            CreateMap<FacturaDetalleCrearDTO, FacturaDetalle>();
            CreateMap<FacturaDetalleActualizarDTO, FacturaDetalle>();

            // Pedido
            CreateMap<Pedido, PedidoDTO>();
            CreateMap<PedidoCrearDTO, Pedido>();
            CreateMap<PedidoActualizarDTO, Pedido>();

            // PedidoDetalle
            CreateMap<PedidoDetalle, PedidoDetalleDTO>();
            CreateMap<PedidoDetalleCrearDTO, PedidoDetalle>();
            CreateMap<PedidoDetalleActualizarDTO, PedidoDetalle>();

            // PedidoCompra
            CreateMap<PedidoCompra, PedidoCompraDTO>();
            CreateMap<PedidoCompraCrearDTO, PedidoCompra>();
            CreateMap<PedidoCompraActualizarDTO, PedidoCompra>();

            // PedidoCompraDetalle
            CreateMap<PedidoCompraDetalle, PedidoCompraDetalleDTO>();
            CreateMap<PedidoCompraDetalleCrearDTO, PedidoCompraDetalle>();
            CreateMap<PedidoCompraDetalleActualizarDTO, PedidoCompraDetalle>();

            // Impuesto
            CreateMap<Impuesto, ImpuestoDTO>();
            CreateMap<ImpuestoCrearDTO, Impuesto>();
            CreateMap<ImpuestoActualizarDTO, Impuesto>();
        }
    }
}