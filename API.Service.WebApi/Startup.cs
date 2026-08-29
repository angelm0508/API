using API.Application.DTO;
using API.Application.Interface;
using API.Application.Main;
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Infraestructure.Repository;
using API.Transversal.Mapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace API.Service.WebApi
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var mensajes = context.ModelState
                            .Where(x => x.Value?.Errors.Count > 0)
                            .SelectMany(x => x.Value!.Errors)
                            .Select(x => x.ErrorMessage)
                            .ToArray();

                        var respuesta = new Respuesta<object>
                        {
                            Resultado = false,
                            Mensaje = string.Join(" | ", mensajes),
                            Dato = null!
                        };

                        return new BadRequestObjectResult(respuesta);
                    };
                });
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Ingresa el token JWT obtenido en api/Auth/login."
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            services.AddDbContext<ApiDbTestContext>(options =>
            {
                options.UseSqlServer(Configuration.GetConnectionString("API_DB"));
            });

            #region autenticación JWT
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Configuration["Jwt:Issuer"],
                        ValidAudience = Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]!))
                    };
                });

            services.AddAuthorization();
            #endregion

            services.AddAutoMapper(x => x.AddProfile(new PerfilMapeo()));

            services.AddTransient<IRepositorioGenerico<GrupoArticulo, int>, GrupoArticuloRepositorio>();
            services.AddTransient<IGrupoArticuloDomain, GrupoArticuloDomain>();
            services.AddTransient<IGrupoArticuloApplication, GrupoArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<FabricanteArticulo, int>, FabricanteArticuloRepositorio>();
            services.AddTransient<IFabricanteArticuloDomain, FabricanteArticuloDomain>();
            services.AddTransient<IFabricanteArticuloApplication, FabricanteArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<UnidadMedidaArticulo, int>, UnidadMedidaArticuloRepositorio>();
            services.AddTransient<IUnidadMedidaArticuloDomain, UnidadMedidaArticuloDomain>();
            services.AddTransient<IUnidadMedidaArticuloApplication, UnidadMedidaArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<GrupoUnidadMedidaArticulo, int>, GrupoUnidadMedidaArticuloRepositorio>();
            services.AddTransient<IGrupoUnidadMedidaArticuloDomain, GrupoUnidadMedidaArticuloDomain>();
            services.AddTransient<IGrupoUnidadMedidaArticuloApplication, GrupoUnidadMedidaArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<GrupoUnidadMedidaDetArticulo, (int GrpMedidaEntry, int NumLinea)>, GrupoUnidadMedidaDetArticuloRepositorio>();
            services.AddTransient<IGrupoUnidadMedidaDetArticuloDomain, GrupoUnidadMedidaDetArticuloDomain>();
            services.AddTransient<IGrupoUnidadMedidaDetArticuloApplication, GrupoUnidadMedidaDetArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<Articulo, string>, ArticuloRepositorio>();
            services.AddTransient<IArticuloDomain, ArticuloDomain>();
            services.AddTransient<IArticuloApplication, ArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<GrupoSn, int>, GrupoSnRepositorio>();
            services.AddTransient<IGrupoSnDomain, GrupoSnDomain>();
            services.AddTransient<IGrupoSnApplication, GrupoSnApplication>();

            services.AddTransient<IRepositorioGenerico<ListadoPrecio, int>, ListadoPrecioRepositorio>();
            services.AddTransient<IListadoPrecioDomain, ListadoPrecioDomain>();
            services.AddTransient<IListadoPrecioApplication, ListadoPrecioApplication>();

            services.AddTransient<IRepositorioGenerico<NumeracionDocumentoDet, int>, NumeracionDocumentoDetRepositorio>();
            services.AddTransient<INumeracionDocumentoDetDomain, NumeracionDocumentoDetDomain>();
            services.AddTransient<INumeracionDocumentoDetApplication, NumeracionDocumentoDetApplication>();

            services.AddTransient<IRepositorioGenerico<Pai, string>, PaisRepositorio>();
            services.AddTransient<IPaisDomain, PaisDomain>();
            services.AddTransient<IPaisApplication, PaisApplication>();

            services.AddTransient<IRepositorioGenerico<Departamento, string>, DepartamentoRepositorio>();
            services.AddTransient<IDepartamentoDomain, DepartamentoDomain>();
            services.AddTransient<IDepartamentoApplication, DepartamentoApplication>();

            services.AddTransient<IRepositorioGenerico<Municipio, string>, MunicipioRepositorio>();
            services.AddTransient<IMunicipioDomain, MunicipioDomain>();
            services.AddTransient<IMunicipioApplication, MunicipioApplication>();

            services.AddTransient<IRepositorioGenerico<Almacen, string>, AlmacenRepositorio>();
            services.AddTransient<IAlmacenDomain, AlmacenDomain>();
            services.AddTransient<IAlmacenApplication, AlmacenApplication>();

            services.AddTransient<IRepositorioGenerico<SocioNegocio, string>, SocioNegocioRepositorio>();
            services.AddTransient<ISocioNegocioDomain, SocioNegocioDomain>();
            services.AddTransient<ISocioNegocioApplication, SocioNegocioApplication>();

            services.AddTransient<IRepositorioGenerico<DireccionSocioNegocio, string>, DireccionSocioNegocioRepositorio>();
            services.AddTransient<IDireccionSocioNegocioDomain, DireccionSocioNegocioDomain>();
            services.AddTransient<IDireccionSocioNegocioApplication, DireccionSocioNegocioApplication>();

            services.AddTransient<IRepositorioGenerico<NumeracionDocumento, string>, NumeracionDocumentoRepositorio>();
            services.AddTransient<INumeracionDocumentoDomain, NumeracionDocumentoDomain>();
            services.AddTransient<INumeracionDocumentoApplication, NumeracionDocumentoApplication>();

            services.AddTransient<IRepositorioGenerico<Monedum, string>, MonedaRepositorio>();
            services.AddTransient<IMonedaDomain, MonedaDomain>();
            services.AddTransient<IMonedaApplication, MonedaApplication>();

            services.AddTransient<IRepositorioGenerico<Usuario, int>, UsuarioRepositorio>();
            services.AddTransient<IUsuarioDomain, UsuarioDomain>();
            services.AddTransient<IUsuarioApplication, UsuarioApplication>();

            services.AddTransient<IRepositorioGenerico<Cotizacion, int>, CotizacionRepositorio>();
            services.AddTransient<ICotizacionDomain, CotizacionDomain>();
            services.AddTransient<ICotizacionApplication, CotizacionApplication>();

            services.AddTransient<IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)>, CotizacionDetalleRepositorio>();
            services.AddTransient<ICotizacionDetalleDomain, CotizacionDetalleDomain>();
            services.AddTransient<ICotizacionDetalleApplication, CotizacionDetalleApplication>();

            services.AddTransient<IRepositorioGenerico<Entrega, int>, EntregaRepositorio>();
            services.AddTransient<IEntregaDomain, EntregaDomain>();
            services.AddTransient<IEntregaApplication, EntregaApplication>();

            services.AddTransient<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>, EntregaDetalleRepositorio>();
            services.AddTransient<IEntregaDetalleDomain, EntregaDetalleDomain>();
            services.AddTransient<IEntregaDetalleApplication, EntregaDetalleApplication>();

            services.AddTransient<IRepositorioGenerico<Pedido, int>, PedidoRepositorio>();
            services.AddTransient<IPedidoDomain, PedidoDomain>();
            services.AddTransient<IPedidoApplication, PedidoApplication>();

            services.AddTransient<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>, PedidoDetalleRepositorio>();
            services.AddTransient<IPedidoDetalleDomain, PedidoDetalleDomain>();
            services.AddTransient<IPedidoDetalleApplication, PedidoDetalleApplication>();

            services.AddTransient<IRepositorioGenerico<Impuesto, string>, ImpuestoRepositorio>();
            services.AddTransient<IImpuestoDomain, ImpuestoDomain>();
            services.AddTransient<IImpuestoApplication, ImpuestoApplication>();

            // Servicios de autenticación
            services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
            services.AddTransient<ITokenService, TokenService>();
            services.AddTransient<IAuthApplication, AuthApplication>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                    var respuesta = new Respuesta<object>
                    {
                        Resultado = false,
                        Mensaje = exceptionFeature?.Error.Message ?? "Ocurrió un error inesperado.",
                        Dato = null!
                    };

                    await context.Response.WriteAsJsonAsync(respuesta);
                });
            });

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });
        }

    }
}
