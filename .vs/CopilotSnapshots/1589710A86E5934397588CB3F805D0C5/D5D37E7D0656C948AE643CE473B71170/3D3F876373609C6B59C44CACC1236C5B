using API.Application.Interface;
using API.Application.Main;
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Infraestructure.Repository;
using API.Transversal.Mapper;
using Microsoft.EntityFrameworkCore;

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
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            services.AddDbContext<ApiDbTestContext>(options =>
            {
                options.UseSqlServer(Configuration.GetConnectionString("API_DB"));
            });

            #region set up authentication 
            /*
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]))
                };
            });

            services.AddAuthorization();
            */
            #endregion

            services.AddAutoMapper(x => x.AddProfile(new PerfilMapeo()));

            services.AddTransient<IRepositorioGenerico<GrupoArticulo>, GrupoArticuloRepositorio>();
            services.AddTransient<IGrupoArticuloDomain, GrupoArticuloDomain>();
            services.AddTransient<IGrupoArticuloApplication, GrupoArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<FabricanteArticulo>, FabricanteArticuloRepositorio>();
            services.AddTransient<IFabricanteArticuloDomain, FabricanteArticuloDomain>();
            services.AddTransient<IFabricanteArticuloApplication, FabricanteArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<MedidaArticulo>, MedidaArticuloRepositorio>();
            services.AddTransient<IMedidaArticuloDomain, MedidaArticuloDomain>();
            services.AddTransient<IMedidaArticuloApplication, MedidaArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<GrupoMedidaArticulo>, GrupoMedidaArticuloRepositorio>();
            services.AddTransient<IGrupoMedidaArticuloDomain, GrupoMedidaArticuloDomain>();
            services.AddTransient<IGrupoMedidaArticuloApplication, GrupoMedidaArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<GrupoMedidaDetArticulo>, GrupoMedidaDetArticuloRepositorio>();
            services.AddTransient<IGrupoMedidaDetArticuloDomain, GrupoMedidaDetArticuloDomain>();
            services.AddTransient<IGrupoMedidaDetArticuloApplication, GrupoMedidaDetArticuloApplication>();

            services.AddTransient<IRepositorioGenericoDos<Articulo>, ArticuloRepositorio>();
            services.AddTransient<IArticuloDomain, ArticuloDomain>();
            services.AddTransient<IArticuloApplication, ArticuloApplication>();

            services.AddTransient<IRepositorioGenerico<GrupoSn>, GrupoSnRepositorio>();
            services.AddTransient<IGrupoSnDomain, GrupoSnDomain>();
            services.AddTransient<IGrupoSnApplication, GrupoSnApplication>();

            services.AddTransient<IRepositorioGenerico<ListadoPrecio>, ListadoPrecioRepositorio>();
            services.AddTransient<IListadoPrecioDomain, ListadoPrecioDomain>();
            services.AddTransient<IListadoPrecioApplication, ListadoPrecioApplication>();

            services.AddTransient<IRepositorioGenerico<NumeracionDocumentoDet>, NumeracionDocumentoDetRepositorio>();
            services.AddTransient<INumeracionDocumentoDetDomain, NumeracionDocumentoDetDomain>();
            services.AddTransient<INumeracionDocumentoDetApplication, NumeracionDocumentoDetApplication>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

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
