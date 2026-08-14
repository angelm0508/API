using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Entity.Models;

public partial class ApiDbTestContext : DbContext
{
    public ApiDbTestContext()
    {
    }

    public ApiDbTestContext(DbContextOptions<ApiDbTestContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Almacen> Almacens { get; set; }

    public virtual DbSet<Articulo> Articulos { get; set; }

    public virtual DbSet<Cotizacion> Cotizacions { get; set; }

    public virtual DbSet<Departamento> Departamentos { get; set; }

    public virtual DbSet<DireccionSocioNegocio> DireccionSocioNegocios { get; set; }

    public virtual DbSet<FabricanteArticulo> FabricanteArticulos { get; set; }

    public virtual DbSet<GrupoArticulo> GrupoArticulos { get; set; }

    public virtual DbSet<GrupoMedidaArticulo> GrupoMedidaArticulos { get; set; }

    public virtual DbSet<GrupoMedidaDetArticulo> GrupoMedidaDetArticulos { get; set; }

    public virtual DbSet<GrupoSn> GrupoSns { get; set; }

    public virtual DbSet<ListadoPrecio> ListadoPrecios { get; set; }

    public virtual DbSet<MedidaArticulo> MedidaArticulos { get; set; }

    public virtual DbSet<Monedum> Moneda { get; set; }

    public virtual DbSet<Municipio> Municipios { get; set; }

    public virtual DbSet<NumeracionDocumento> NumeracionDocumentos { get; set; }

    public virtual DbSet<NumeracionDocumentoDet> NumeracionDocumentoDets { get; set; }

    public virtual DbSet<Pai> Pais { get; set; }

    public virtual DbSet<SocioNegocio> SocioNegocios { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost; Database=API_DB_TEST; User=sa; Password=contra1234; TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Almacen>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("pk_almacen");

            entity.ToTable("Almacen");

            entity.Property(e => e.Codigo).HasMaxLength(8);
            entity.Property(e => e.Activo)
                .HasMaxLength(1)
                .HasDefaultValueSql("('S')");
            entity.Property(e => e.Bloqueado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Calle).HasMaxLength(100);
            entity.Property(e => e.CodigoPostal).HasMaxLength(20);
            entity.Property(e => e.Departamento).HasMaxLength(3);
            entity.Property(e => e.Municipio).HasMaxLength(3);
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Pais).HasMaxLength(3);

            entity.HasOne(d => d.PaisNavigation).WithMany(p => p.Almacens)
                .HasForeignKey(d => d.Pais)
                .HasConstraintName("fk_almacen_pais");

            entity.HasOne(d => d.DepartamentoNavigation).WithMany(p => p.Almacens)
                .HasForeignKey(d => new { d.Departamento, d.Pais })
                .HasConstraintName("fk_almacen_departamento");

            entity.HasOne(d => d.MunicipioNavigation).WithMany(p => p.Almacens)
                .HasForeignKey(d => new { d.Municipio, d.Departamento, d.Pais })
                .HasConstraintName("fk_almacen_municipio");
        });

        modelBuilder.Entity<Articulo>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("pk_articulo");

            entity.ToTable("Articulo");

            entity.Property(e => e.Codigo).HasMaxLength(15);
            entity.Property(e => e.Activo)
                .HasMaxLength(1)
                .HasDefaultValueSql("('S')");
            entity.Property(e => e.AlmacenDefecto).HasMaxLength(8);
            entity.Property(e => e.ArticuloCompra)
                .HasMaxLength(1)
                .HasDefaultValueSql("('S')");
            entity.Property(e => e.ArticuloInventario)
                .HasMaxLength(1)
                .HasDefaultValueSql("('S')");
            entity.Property(e => e.ArticuloVenta)
                .HasMaxLength(1)
                .HasDefaultValueSql("('S')");
            entity.Property(e => e.CantConfirmada).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.CantDisponible).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.CantPedida).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.Comentarios).HasMaxLength(1000);
            entity.Property(e => e.GestLote)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.GestNoSerie)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.GestPorAlmacen)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Maximo).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.Minimo).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.NoApliDesc)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Nombre).HasMaxLength(200);
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(19, 0)");

            entity.HasOne(d => d.AlmacenDefectoNavigation).WithMany(p => p.Articulos)
                .HasForeignKey(d => d.AlmacenDefecto)
                .HasConstraintName("fk_articulo_almc_defecto");

            entity.HasOne(d => d.CodigoGrpMedidaNavigation).WithMany(p => p.Articulos)
                .HasForeignKey(d => d.CodigoGrpMedida)
                .HasConstraintName("fk_articulo_grp_medida");

            entity.HasOne(d => d.CodigoGrupoNavigation).WithMany(p => p.Articulos)
                .HasForeignKey(d => d.CodigoGrupo)
                .HasConstraintName("fk_articulo_grupo");

            entity.HasOne(d => d.FabricanteEntryNavigation).WithMany(p => p.Articulos)
                .HasForeignKey(d => d.FabricanteEntry)
                .HasConstraintName("fk_articulo_fabricante");
        });

        modelBuilder.Entity<Cotizacion>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_cotizacion");

            entity.ToTable("Cotizacion");

            entity.Property(e => e.Cancelado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.Direccion).HasMaxLength(254);
            entity.Property(e => e.EstadoDoc)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.EstadoInv)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.FechaCancelado).HasColumnType("datetime");
            entity.Property(e => e.FechaDoc).HasColumnType("datetime");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.Imprimido)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.MonedaDoc).HasMaxLength(3);
            entity.Property(e => e.NombreSn)
                .HasMaxLength(200)
                .HasColumnName("NombreSN");
            entity.Property(e => e.NumManual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeImpuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(11)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.TotalBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDoc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalImp).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoSnNavigation).WithMany(p => p.Cotizacions)
                .HasForeignKey(d => d.CodigoSn)
                .HasConstraintName("fk_cotizacion_sn");

            entity.HasOne(d => d.MonedaDocNavigation).WithMany(p => p.Cotizacions)
                .HasForeignKey(d => d.MonedaDoc)
                .HasConstraintName("fk_cotizacion_moneda");

            entity.HasOne(d => d.SerieNavigation).WithMany(p => p.Cotizacions)
                .HasForeignKey(d => d.Serie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cotizacion_serie");
        });

        modelBuilder.Entity<Departamento>(entity =>
        {
            entity.HasKey(e => new { e.Codigo, e.Pais }).HasName("pk_departamento_codigo");

            entity.ToTable("Departamento");

            entity.Property(e => e.Codigo).HasMaxLength(3);
            entity.Property(e => e.Pais).HasMaxLength(3);
            entity.Property(e => e.Nombre).HasMaxLength(100);

            entity.HasOne(d => d.PaisNavigation).WithMany(p => p.Departamentos)
                .HasForeignKey(d => d.Pais)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_departamento_cod_pais");
        });

        modelBuilder.Entity<DireccionSocioNegocio>(entity =>
        {
            entity.HasKey(e => new { e.Direccion, e.CodigoSn }).HasName("pk_direccion_sn");

            entity.ToTable("DireccionSocioNegocio");

            entity.Property(e => e.Direccion).HasMaxLength(50);
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Bloque).HasMaxLength(100);
            entity.Property(e => e.Calle).HasMaxLength(100);
            entity.Property(e => e.CodigoPostal).HasMaxLength(20);
            entity.Property(e => e.Departamento).HasMaxLength(3);
            entity.Property(e => e.Municipio).HasMaxLength(3);
            entity.Property(e => e.Pais).HasMaxLength(3);
            entity.Property(e => e.TipoDireccion).HasMaxLength(3);

            entity.HasOne(d => d.CodigoSnNavigation).WithMany(p => p.DireccionSocioNegocios)
                .HasForeignKey(d => d.CodigoSn)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_dire_sn_cod_sn");

            entity.HasOne(d => d.PaisNavigation).WithMany(p => p.DireccionSocioNegocios)
                .HasForeignKey(d => d.Pais)
                .HasConstraintName("fk_dire_sn_pais");

            entity.HasOne(d => d.DepartamentoNavigation).WithMany(p => p.DireccionSocioNegocios)
                .HasForeignKey(d => new { d.Departamento, d.Pais })
                .HasConstraintName("fk_dire_sn_departamento");

            entity.HasOne(d => d.MunicipioNavigation).WithMany(p => p.DireccionSocioNegocios)
                .HasForeignKey(d => new { d.Municipio, d.Departamento, d.Pais })
                .HasConstraintName("fk_dire_sn_municipio");
        });

        modelBuilder.Entity<FabricanteArticulo>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_fabricante_art");

            entity.ToTable("FabricanteArticulo");

            entity.Property(e => e.Bloqueado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Nombre).HasMaxLength(30);
        });

        modelBuilder.Entity<GrupoArticulo>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("pk_grp_art");

            entity.ToTable("GrupoArticulo");

            entity.Property(e => e.Bloqueado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Nombre).HasMaxLength(100);
        });

        modelBuilder.Entity<GrupoMedidaArticulo>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_grp_medida_art");

            entity.ToTable("GrupoMedidaArticulo");

            entity.Property(e => e.Bloqueado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Codigo).HasMaxLength(20);
            entity.Property(e => e.Nombre).HasMaxLength(100);

            entity.HasOne(d => d.BaseMedidaNavigation).WithMany(p => p.GrupoMedidaArticulos)
                .HasForeignKey(d => d.BaseMedida)
                .HasConstraintName("fk_medida_art");
        });

        modelBuilder.Entity<GrupoMedidaDetArticulo>(entity =>
        {
            entity.HasKey(e => new { e.GrpMedidaEntry, e.NumLinea }).HasName("pk_grp_medida_det_art");

            entity.ToTable("GrupoMedidaDetArticulo");

            entity.Property(e => e.Activo).HasMaxLength(1);
            entity.Property(e => e.CantAlternativa).HasColumnType("decimal(19, 0)");
            entity.Property(e => e.CantBase).HasColumnType("decimal(19, 0)");

            entity.HasOne(d => d.MedidaEntryNavigation).WithMany(p => p.GrupoMedidaDetArticulos)
                .HasForeignKey(d => d.MedidaEntry)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_grp_medida_art");
        });

        modelBuilder.Entity<GrupoSn>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_grupo_sn");

            entity.ToTable("GrupoSN");

            entity.Property(e => e.Entry).ValueGeneratedNever();
            entity.Property(e => e.Bloqueado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Nombre).HasMaxLength(200);
            entity.Property(e => e.TipoGrupo).HasMaxLength(1);
        });

        modelBuilder.Entity<ListadoPrecio>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_listado_precio");

            entity.ToTable("ListadoPrecio");

            entity.Property(e => e.Entry).ValueGeneratedNever();
            entity.Property(e => e.ExtMonto).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.Factor).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.MetodoRedondeo).HasDefaultValueSql("((0))");
            entity.Property(e => e.Nombre).HasMaxLength(32);
            entity.Property(e => e.ReglaRedondeo)
                .HasMaxLength(1)
                .HasDefaultValueSql("('R')");
            entity.Property(e => e.RndFrmtDec).HasMaxLength(10);
            entity.Property(e => e.RndFrmtInt).HasMaxLength(10);
        });

        modelBuilder.Entity<MedidaArticulo>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_medida_art");

            entity.ToTable("MedidaArticulo");

            entity.Property(e => e.Altura).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.Ancho).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.Bloqueado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Codigo).HasMaxLength(20);
            entity.Property(e => e.Largo).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Peso).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.Volumen).HasColumnType("decimal(21, 6)");
        });

        modelBuilder.Entity<Monedum>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("pk_moneda");

            entity.Property(e => e.Codigo).HasMaxLength(3);
            entity.Property(e => e.Centena).HasMaxLength(20);
            entity.Property(e => e.CodigoIso)
                .HasMaxLength(3)
                .HasDefaultValueSql("('QTZ')")
                .HasColumnName("CodigoISO");
            entity.Property(e => e.Nombre).HasMaxLength(20);
            entity.Property(e => e.NombreImpresion).HasMaxLength(3);
            entity.Property(e => e.TipoReondeo).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<Municipio>(entity =>
        {
            entity.HasKey(e => new { e.Codigo, e.Departamento, e.Pais }).HasName("pk_municipio");

            entity.ToTable("Municipio");

            entity.Property(e => e.Codigo).HasMaxLength(3);
            entity.Property(e => e.Departamento).HasMaxLength(3);
            entity.Property(e => e.Pais).HasMaxLength(3);
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.DepartamentoNavigation).WithMany(p => p.Municipios)
                .HasForeignKey(d => new { d.Departamento, d.Pais })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_municipio_departamento");
        });

        modelBuilder.Entity<NumeracionDocumento>(entity =>
        {
            entity.HasKey(e => new { e.CodigoObj, e.SubTipoDoc }).HasName("pk_numeracion_documento");

            entity.ToTable("NumeracionDocumento");

            entity.Property(e => e.CodigoObj).HasMaxLength(20);
            entity.Property(e => e.SubTipoDoc)
                .HasMaxLength(2)
                .HasDefaultValueSql("('--')");
            entity.Property(e => e.DocAlias).HasMaxLength(20);
            entity.Property(e => e.SerieDfct).HasDefaultValueSql("((0))");
        });

        modelBuilder.Entity<NumeracionDocumentoDet>(entity =>
        {
            entity.HasKey(e => e.Serie).HasName("pk_numeracion_documento_det");

            entity.ToTable("NumeracionDocumentoDet");

            entity.Property(e => e.Serie).ValueGeneratedNever();
            entity.Property(e => e.Bloqueado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodigoObj).HasMaxLength(20);
            entity.Property(e => e.Comentario).HasMaxLength(5);
            entity.Property(e => e.FinCadena).HasMaxLength(20);
            entity.Property(e => e.IniCadena).HasMaxLength(20);
            entity.Property(e => e.Manual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.NombreSerie).HasMaxLength(8);
            entity.Property(e => e.SubTipoDoc)
                .HasMaxLength(2)
                .HasDefaultValueSql("('--')");
            entity.Property(e => e.TipoSerie)
                .HasMaxLength(1)
                .HasDefaultValueSql("('D')");
        });

        modelBuilder.Entity<Pai>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("pk_pais_codigo");

            entity.Property(e => e.Codigo).HasMaxLength(3);
            entity.Property(e => e.Iso2codigo)
                .HasMaxLength(2)
                .HasColumnName("ISO2Codigo");
            entity.Property(e => e.Iso3codigo)
                .HasMaxLength(3)
                .HasColumnName("ISO3Codigo");
            entity.Property(e => e.Isonumerico)
                .HasMaxLength(3)
                .HasColumnName("ISONumerico");
            entity.Property(e => e.Nombre).HasMaxLength(100);
        });

        modelBuilder.Entity<SocioNegocio>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("pk_socio_negocio");

            entity.ToTable("SocioNegocio");

            entity.Property(e => e.Codigo).HasMaxLength(15);
            entity.Property(e => e.Activo).HasMaxLength(1);
            entity.Property(e => e.Cui).HasMaxLength(15);
            entity.Property(e => e.Descuento).HasColumnType("decimal(21, 6)");
            entity.Property(e => e.Email).HasMaxLength(75);
            entity.Property(e => e.GrupoSn).HasColumnName("GrupoSN");
            entity.Property(e => e.Nit).HasMaxLength(15);
            entity.Property(e => e.Nombre).HasMaxLength(200);
            entity.Property(e => e.PersContacto).HasMaxLength(75);
            entity.Property(e => e.Tel1).HasMaxLength(20);
            entity.Property(e => e.Tel2).HasMaxLength(20);
            entity.Property(e => e.TipoSn)
                .HasMaxLength(1)
                .HasColumnName("TipoSN");

            entity.HasOne(d => d.GrupoSnNavigation).WithMany(p => p.SocioNegocios)
                .HasForeignKey(d => d.GrupoSn)
                .HasConstraintName("fk_sn_grupo");

            entity.HasOne(d => d.NumLstPrecioNavigation).WithMany(p => p.SocioNegocios)
                .HasForeignKey(d => d.NumLstPrecio)
                .HasConstraintName("fk_sn_num_lst_precio");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_usuario");

            entity.ToTable("Usuario");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("ID");
            entity.Property(e => e.Bloqueado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Codigo).HasMaxLength(25);
            entity.Property(e => e.Eliminado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Email).HasMaxLength(75);
            entity.Property(e => e.LlaveInterna).ValueGeneratedOnAdd();
            entity.Property(e => e.Nombre).HasMaxLength(150);
            entity.Property(e => e.Password).HasMaxLength(254);
            entity.Property(e => e.SuperUsuario)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.UltimaContra).HasMaxLength(254);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
