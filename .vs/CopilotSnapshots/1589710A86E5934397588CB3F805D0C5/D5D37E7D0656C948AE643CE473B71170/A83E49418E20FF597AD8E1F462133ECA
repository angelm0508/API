using API.Application.DTO.articulo.articulo;
using API.Application.DTO.articulo.grupo_articulo;
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
        }
    }
}