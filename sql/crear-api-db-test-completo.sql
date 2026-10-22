CREATE DATABASE API_DB_TEST;
GO

USE API_DB_TEST;
GO

--------------------------------------------------------------------
CREATE TABLE Pais(
	Codigo NVARCHAR(3) NOT NULL,
	Nombre NVARCHAR(100),
	ISO2Codigo NVARCHAR(2),
	ISO3Codigo NVARCHAR(3),
	ISONumerico NVARCHAR(3),
	CONSTRAINT pk_pais_codigo PRIMARY KEY(Codigo)
);

INSERT INTO Pais(Codigo, Nombre, ISO2Codigo, ISO3Codigo, ISONumerico)
VALUES ('GT', 'Guatemala', 'GT', 'GTM', '320');
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE Departamento(
	Codigo NVARCHAR(3) NOT NULL,
	Pais NVARCHAR(3),
	Nombre NVARCHAR(100),
	CONSTRAINT pk_departamento_codigo PRIMARY KEY(Codigo, Pais),
	CONSTRAINT fk_departamento_cod_pais FOREIGN KEY(Pais) REFERENCES Pais(Codigo)
);

INSERT INTO Departamento (Codigo, Pais, Nombre)
VALUES
(1, 'GT', 'Alta Verapaz'),
(2, 'GT', 'Baja Verapaz'),
(3, 'GT', 'Chimaltenango'),
(4, 'GT', 'Chiquimula'),
(5, 'GT', 'El Progreso'),
(6, 'GT', 'Escuintla'),
(7, 'GT', 'Guatemala'),
(8, 'GT', 'Huehuetenango'),
(9, 'GT', 'Izabal'),
(10, 'GT', 'Jalapa'),
(11, 'GT', 'Jutiapa'),
(12, 'GT', 'Petén'),
(13, 'GT', 'Quetzaltenango'),
(14, 'GT', 'Quiché'),
(15, 'GT', 'Retalhuleu'),
(16, 'GT', 'Sacatepéquez'),
(17, 'GT', 'San Marcos'),
(18, 'GT', 'Santa Rosa'),
(19, 'GT', 'Sololá'),
(20, 'GT', 'Suchitepéquez'),
(21, 'GT', 'Totonicapán'),
(22, 'GT', 'Zacapa');
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE Municipio (
    Codigo NVARCHAR(3) NOT NULL,
    Departamento NVARCHAR(3) NOT NULL,
    Pais NVARCHAR(3) NOT NULL,
    Nombre VARCHAR(100) NOT NULL,
    CONSTRAINT pk_municipio PRIMARY KEY (Codigo, Departamento, Pais),
    CONSTRAINT fk_municipio_departamento FOREIGN KEY(Departamento, Pais) REFERENCES Departamento(Codigo, Pais)
);


INSERT INTO Municipio (Codigo, Departamento, Pais, Nombre)
VALUES
(1, 1, 'GT', 'Cobán'),
(2, 1, 'GT', 'Santa Cruz Verapaz'),
(3, 1, 'GT', 'San Cristóbal Verapaz'),
(4, 1, 'GT', 'Tactic'),
(5, 1, 'GT', 'Tamahú'),
(6, 1, 'GT', 'Tucurú'),
(7, 1, 'GT', 'Panzós'),
(8, 1, 'GT', 'Senahú'),
(9, 1, 'GT', 'San Pedro Carchá'),
(10, 1, 'GT', 'San Juan Chamelco'),
(11, 1, 'GT', 'Lanquín'),
(12, 1, 'GT', 'Cahabón'),
(13, 1, 'GT', 'Chisec'),
(14, 1, 'GT', 'Chahal'),
(15, 1, 'GT', 'Fray Bartolomé de las Casas'),
(16, 1, 'GT', 'Santa Catalina La Tinta'),
(17, 1, 'GT', 'Raxruhá'),
(1, 2, 'GT', 'Salamá'),
(2, 2, 'GT', 'San Miguel Chicaj'),
(3, 2, 'GT', 'Rabinal'),
(4, 2, 'GT', 'Cubulco'),
(5, 2, 'GT', 'Granados'),
(6, 2, 'GT', 'Santa Cruz El Chol'),
(7, 2, 'GT', 'San Jerónimo'),
(8, 2, 'GT', 'Purulhá'),
(1, 3, 'GT', 'Chimaltenango'),
(2, 3, 'GT', 'San José Poaquil'),
(3, 3, 'GT', 'San Martín Jilotepeque'),
(4, 3, 'GT', 'San Juan Comalapa'),
(5, 3, 'GT', 'Santa Apolonia'),
(6, 3, 'GT', 'Tecpán Guatemala'),
(7, 3, 'GT', 'Patzún'),
(8, 3, 'GT', 'Pochuta'),
(9, 3, 'GT', 'Patzicía'),
(10, 3, 'GT', 'Santa Cruz Balanyá'),
(11, 3, 'GT', 'Acatenango'),
(12, 3, 'GT', 'Yepocapa'),
(13, 3, 'GT', 'San Andrés Itzapa'),
(14, 3, 'GT', 'Parramos'),
(15, 3, 'GT', 'Zaragoza'),
(16, 3, 'GT', 'El Tejar'),
(1, 4, 'GT', 'Chiquimula'),
(2, 4, 'GT', 'San José La Arada'),
(3, 4, 'GT', 'San Juan Ermita'),
(4, 4, 'GT', 'Jocotán'),
(5, 4, 'GT', 'Camotán'),
(6, 4, 'GT', 'Olopa'),
(7, 4, 'GT', 'Esquipulas'),
(8, 4, 'GT', 'Concepción Las Minas'),
(9, 4, 'GT', 'Quezaltepeque'),
(10, 4, 'GT', 'San Jacinto'),
(11, 4, 'GT', 'Ipala'),
(1, 5, 'GT', 'Guastatoya'),
(2, 5, 'GT', 'Morazán'),
(3, 5, 'GT', 'San Agustín Acasaguastlán'),
(4, 5, 'GT', 'San Cristóbal Acasaguastlán'),
(5, 5, 'GT', 'El Jícaro'),
(6, 5, 'GT', 'Sansare'),
(7, 5, 'GT', 'Sanarate'),
(8, 5, 'GT', 'San Antonio La Paz'),
(1, 6, 'GT', 'Escuintla'),
(2, 6, 'GT', 'Santa Lucía Cotzumalguapa'),
(3, 6, 'GT', 'La Democracia'),
(4, 6, 'GT', 'Siquinalá'),
(5, 6, 'GT', 'Masagua'),
(6, 6, 'GT', 'Tiquisate'),
(7, 6, 'GT', 'La Gomera'),
(8, 6, 'GT', 'Guanagazapa'),
(9, 6, 'GT', 'San José'),
(10, 6, 'GT', 'Iztapa'),
(11, 6, 'GT', 'Palín'),
(12, 6, 'GT', 'San Vicente Pacaya'),
(13, 6, 'GT', 'Nueva Concepción'),
(1, 7, 'GT', 'Guatemala'),
(2, 7, 'GT', 'Santa Catarina Pinula'),
(3, 7, 'GT', 'San José Pinula'),
(4, 7, 'GT', 'San José del Golfo'),
(5, 7, 'GT', 'Palencia'),
(6, 7, 'GT', 'Chinautla'),
(7, 7, 'GT', 'San Pedro Ayampuc'),
(8, 7, 'GT', 'Mixco'),
(9, 7, 'GT', 'San Pedro Sacatepéquez'),
(10, 7, 'GT', 'San Juan Sacatepéquez'),
(11, 7, 'GT', 'San Raymundo'),
(12, 7, 'GT', 'Chuarrancho'),
(13, 7, 'GT', 'Fraijanes'),
(14, 7, 'GT', 'Amatitlán'),
(15, 7, 'GT', 'Villa Nueva'),
(16, 7, 'GT', 'Villa Canales'),
(17, 7, 'GT', 'San Miguel Petapa'),
(1, 8, 'GT', 'Huehuetenango'),
(2, 8, 'GT', 'Chiantla'),
(3, 8, 'GT', 'Malacatancito'),
(4, 8, 'GT', 'Cuilco'),
(5, 8, 'GT', 'Nentón'),
(6, 8, 'GT', 'San Pedro Necta'),
(7, 8, 'GT', 'Jacaltenango'),
(8, 8, 'GT', 'Soloma'),
(9, 8, 'GT', 'Ixtahuacán'),
(10, 8, 'GT', 'Santa Bárbara'),
(11, 8, 'GT', 'La Libertad'),
(12, 8, 'GT', 'La Democracia'),
(13, 8, 'GT', 'San Miguel Acatán'),
(14, 8, 'GT', 'San Rafael La Independencia'),
(15, 8, 'GT', 'Todos Santos Cuchumatán'),
(16, 8, 'GT', 'San Juan Atitán'),
(17, 8, 'GT', 'Santa Eulalia'),
(18, 8, 'GT', 'San Mateo Ixtatán'),
(19, 8, 'GT', 'Colotenango'),
(20, 8, 'GT', 'San Sebastián Huehuetenango'),
(21, 8, 'GT', 'Tectitán'),
(22, 8, 'GT', 'Concepción Huista'),
(23, 8, 'GT', 'San Juan Ixcoy'),
(24, 8, 'GT', 'San Antonio Huista'),
(25, 8, 'GT', 'San Sebastián Coatán'),
(26, 8, 'GT', 'Barillas'),
(27, 8, 'GT', 'Aguacatán'),
(28, 8, 'GT', 'San Rafael Petzal'),
(29, 8, 'GT', 'San Gaspar Ixchil'),
(30, 8, 'GT', 'Santiago Chimaltenango'),
(31, 8, 'GT', 'Santa Ana Huista'),
(32, 8, 'GT', 'Unión Cantinil'),
(33, 8, 'GT', 'Petatán'),
(1, 9, 'GT', 'Puerto Barrios'),
(2, 9, 'GT', 'Livingston'),
(3, 9, 'GT', 'El Estor'),
(4, 9, 'GT', 'Morales'),
(5, 9, 'GT', 'Los Amates'),
(1, 10, 'GT', 'Jalapa'),
(2, 10, 'GT', 'San Pedro Pinula'),
(3, 10, 'GT', 'San Luis Jilotepeque'),
(4, 10, 'GT', 'San Manuel Chaparrón'),
(5, 10, 'GT', 'San Carlos Alzatate'),
(6, 10, 'GT', 'Monjas'),
(7, 10, 'GT', 'Mataquescuintla'),
(1, 11, 'GT', 'Jutiapa'),
(2, 11, 'GT', 'El Progreso'),
(3, 11, 'GT', 'Santa Catarina Mita'),
(4, 11, 'GT', 'Agua Blanca'),
(5, 11, 'GT', 'Asunción Mita'),
(6, 11, 'GT', 'Yupiltepeque'),
(7, 11, 'GT', 'Atescatempa'),
(8, 11, 'GT', 'Jerez'),
(9, 11, 'GT', 'El Adelanto'),
(10, 11, 'GT', 'Zapotitlán'),
(11, 11, 'GT', 'Comapa'),
(12, 11, 'GT', 'Jalpatagua'),
(13, 11, 'GT', 'Conguaco'),
(14, 11, 'GT', 'Moyuta'),
(15, 11, 'GT', 'Pasaco'),
(16, 11, 'GT', 'San José Acatempa'),
(17, 11, 'GT', 'Quesada'),
(1, 12, 'GT', 'Flores'),
(2, 12, 'GT', 'San José'),
(3, 12, 'GT', 'San Benito'),
(4, 12, 'GT', 'San Andrés'),
(5, 12, 'GT', 'La Libertad'),
(6, 12, 'GT', 'San Francisco'),
(7, 12, 'GT', 'Santa Ana'),
(8, 12, 'GT', 'Dolores'),
(9, 12, 'GT', 'San Luis'),
(10, 12, 'GT', 'Sayaxché'),
(11, 12, 'GT', 'Melchor de Mencos'),
(12, 12, 'GT', 'Poptún'),
(13, 12, 'GT', 'Las Cruces'),
(14, 12, 'GT', 'El Chal'),
(1, 13, 'GT', 'Quetzaltenango'),
(2, 13, 'GT', 'Salcajá'),
(3, 13, 'GT', 'Olintepeque'),
(4, 13, 'GT', 'San Carlos Sija'),
(5, 13, 'GT', 'Sibilia'),
(6, 13, 'GT', 'Cabricán'),
(7, 13, 'GT', 'Cajolá'),
(8, 13, 'GT', 'San Miguel Sigüilá'),
(9, 13, 'GT', 'San Juan Ostuncalco'),
(10, 13, 'GT', 'San Mateo'),
(11, 13, 'GT', 'Concepción Chiquirichapa'),
(12, 13, 'GT', 'San Martín Sacatepéquez'),
(13, 13, 'GT', 'Almolonga'),
(14, 13, 'GT', 'Cantel'),
(15, 13, 'GT', 'Huitán'),
(16, 13, 'GT', 'Zunil'),
(17, 13, 'GT', 'Colomba Costa Cuca'),
(18, 13, 'GT', 'San Francisco La Unión'),
(19, 13, 'GT', 'El Palmar'),
(20, 13, 'GT', 'Coatepeque'),
(21, 13, 'GT', 'Génova'),
(22, 13, 'GT', 'Flores Costa Cuca'),
(23, 13, 'GT', 'La Esperanza'),
(24, 13, 'GT', 'Palestina de Los Altos'),
(1, 14, 'GT', 'Santa Cruz del Quiché'),
(2, 14, 'GT', 'Chiché'),
(3, 14, 'GT', 'Chinique'),
(4, 14, 'GT', 'Zacualpa'),
(5, 14, 'GT', 'Chajul'),
(6, 14, 'GT', 'Santo Tomás Chichicastenango'),
(7, 14, 'GT', 'Patzité'),
(8, 14, 'GT', 'San Antonio Ilotenango'),
(9, 14, 'GT', 'San Pedro Jocopilas'),
(10, 14, 'GT', 'Cunén'),
(11, 14, 'GT', 'San Juan Cotzal'),
(12, 14, 'GT', 'Joyabaj'),
(13, 14, 'GT', 'Nebaj'),
(14, 14, 'GT', 'San Andrés Sajcabajá'),
(15, 14, 'GT', 'Uspantán'),
(16, 14, 'GT', 'Sacapulas'),
(17, 14, 'GT', 'San Bartolomé Jocotenango'),
(18, 14, 'GT', 'Canillá'),
(19, 14, 'GT', 'Chicamán'),
(20, 14, 'GT', 'Ixcán'),
(21, 14, 'GT', 'Pachalum'),
(1, 15, 'GT', 'Retalhuleu'),
(2, 15, 'GT', 'San Sebastián'),
(3, 15, 'GT', 'Santa Cruz Muluá'),
(4, 15, 'GT', 'San Martín Zapotitlán'),
(5, 15, 'GT', 'San Felipe'),
(6, 15, 'GT', 'San Andrés Villa Seca'),
(7, 15, 'GT', 'Champerico'),
(8, 15, 'GT', 'Nuevo San Carlos'),
(9, 15, 'GT', 'El Asintal'),
(1, 16, 'GT', 'Antigua Guatemala'),
(2, 16, 'GT', 'Jocotenango'),
(3, 16, 'GT', 'Pastores'),
(4, 16, 'GT', 'Sumpango'),
(5, 16, 'GT', 'Santo Domingo Xenacoj'),
(6, 16, 'GT', 'Santiago Sacatepéquez'),
(7, 16, 'GT', 'San Bartolomé Milpas Altas'),
(8, 16, 'GT', 'San Lucas Sacatepéquez'),
(9, 16, 'GT', 'Santa Lucía Milpas Altas'),
(10, 16, 'GT', 'Magdalena Milpas Altas'),
(11, 16, 'GT', 'Santa María de Jesús'),
(12, 16, 'GT', 'Ciudad Vieja'),
(13, 16, 'GT', 'San Miguel Dueñas'),
(14, 16, 'GT', 'Alotenango'),
(15, 16, 'GT', 'San Antonio Aguas Calientes'),
(16, 16, 'GT', 'Santa Catarina Barahona'),
(1, 17, 'GT', 'San Marcos'),
(2, 17, 'GT', 'San Pedro Sacatepéquez'),
(3, 17, 'GT', 'San Antonio Sacatepéquez'),
(4, 17, 'GT', 'Comitancillo'),
(5, 17, 'GT', 'San Miguel Ixtahuacán'),
(6, 17, 'GT', 'Concepción Tutuapa'),
(7, 17, 'GT', 'Tacaná'),
(8, 17, 'GT', 'Sibinal'),
(9, 17, 'GT', 'Tajumulco'),
(10, 17, 'GT', 'Tejutla'),
(11, 17, 'GT', 'San Rafael Pie de la Cuesta'),
(12, 17, 'GT', 'Nuevo Progreso'),
(13, 17, 'GT', 'El Tumbador'),
(14, 17, 'GT', 'El Rodeo'),
(15, 17, 'GT', 'Malacatán'),
(16, 17, 'GT', 'Catarina'),
(17, 17, 'GT', 'Ayutla'),
(18, 17, 'GT', 'Ocós'),
(19, 17, 'GT', 'San Pablo'),
(20, 17, 'GT', 'El Quetzal'),
(21, 17, 'GT', 'La Reforma'),
(22, 17, 'GT', 'Pajapita'),
(23, 17, 'GT', 'Ixchiguán'),
(24, 17, 'GT', 'San José Ojetenam'),
(25, 17, 'GT', 'San Cristóbal Cucho'),
(26, 17, 'GT', 'Sipacapa'),
(27, 17, 'GT', 'Esquipulas Palo Gordo'),
(28, 17, 'GT', 'Río Blanco'),
(29, 17, 'GT', 'San Lorenzo'),
(30, 17, 'GT', 'La Blanca'),
(1, 18, 'GT', 'Cuilapa'),
(2, 18, 'GT', 'Barberena'),
(3, 18, 'GT', 'Santa Rosa de Lima'),
(4, 18, 'GT', 'Casillas'),
(5, 18, 'GT', 'San Rafael Las Flores'),
(6, 18, 'GT', 'Oratorio'),
(7, 18, 'GT', 'San Juan Tecuaco'),
(8, 18, 'GT', 'Chiquimulilla'),
(9, 18, 'GT', 'Taxisco'),
(10, 18, 'GT', 'Santa María Ixhuatán'),
(11, 18, 'GT', 'Guazacapán'),
(12, 18, 'GT', 'Santa Cruz Naranjo'),
(13, 18, 'GT', 'Pueblo Nuevo Viñas'),
(14, 18, 'GT', 'Nueva Santa Rosa'),
(1, 19, 'GT', 'Sololá'),
(2, 19, 'GT', 'San José Chacayá'),
(3, 19, 'GT', 'Santa María Visitación'),
(4, 19, 'GT', 'Santa Lucía Utatlán'),
(5, 19, 'GT', 'Nahualá'),
(6, 19, 'GT', 'Santa Catarina Ixtahuacán'),
(7, 19, 'GT', 'Santa Clara La Laguna'),
(8, 19, 'GT', 'Concepción'),
(9, 19, 'GT', 'San Andrés Semetabaj'),
(10, 19, 'GT', 'Panajachel'),
(11, 19, 'GT', 'Santa Catarina Palopó'),
(12, 19, 'GT', 'San Antonio Palopó'),
(13, 19, 'GT', 'San Lucas Tolimán'),
(14, 19, 'GT', 'Santa Cruz La Laguna'),
(15, 19, 'GT', 'San Pablo La Laguna'),
(16, 19, 'GT', 'San Marcos La Laguna'),
(17, 19, 'GT', 'San Juan La Laguna'),
(18, 19, 'GT', 'San Pedro La Laguna'),
(19, 19, 'GT', 'Santiago Atitlán'),
(1, 20, 'GT', 'Mazatenango'),
(2, 20, 'GT', 'Cuyotenango'),
(3, 20, 'GT', 'San Francisco Zapotitlán'),
(4, 20, 'GT', 'San Bernardino'),
(5, 20, 'GT', 'San José El Ídolo'),
(6, 20, 'GT', 'Santo Domingo Suchitepéquez'),
(7, 20, 'GT', 'San Lorenzo'),
(8, 20, 'GT', 'Samayac'),
(9, 20, 'GT', 'San Pablo Jocopilas'),
(10, 20, 'GT', 'San Antonio Suchitepéquez'),
(11, 20, 'GT', 'San Miguel Panán'),
(12, 20, 'GT', 'San Gabriel'),
(13, 20, 'GT', 'Chicacao'),
(14, 20, 'GT', 'Patulul'),
(15, 20, 'GT', 'Santa Bárbara'),
(16, 20, 'GT', 'San Juan Bautista'),
(17, 20, 'GT', 'Santo Tomás La Unión'),
(18, 20, 'GT', 'Zunilito'),
(19, 20, 'GT', 'Pueblo Nuevo'),
(20, 20, 'GT', 'Río Bravo'),
(21, 20, 'GT', 'San José La Máquina'),
(1, 21, 'GT', 'Totonicapán'),
(2, 21, 'GT', 'San Cristóbal Totonicapán'),
(3, 21, 'GT', 'San Francisco El Alto'),
(4, 21, 'GT', 'San Andrés Xecul'),
(5, 21, 'GT', 'Momostenango'),
(6, 21, 'GT', 'Santa María Chiquimula'),
(7, 21, 'GT', 'Santa Lucía La Reforma'),
(8, 21, 'GT', 'San Bartolo Aguas Calientes'),
(1, 22, 'GT', 'Zacapa'),
(2, 22, 'GT', 'Estanzuela'),
(3, 22, 'GT', 'Río Hondo'),
(4, 22, 'GT', 'Gualán'),
(5, 22, 'GT', 'Teculután'),
(6, 22, 'GT', 'Usumatlán'),
(7, 22, 'GT', 'Cabañas'),
(8, 22, 'GT', 'San Diego'),
(9, 22, 'GT', 'La Unión'),
(10, 22, 'GT', 'Huité'),
(11, 22, 'GT', 'San Jorge');
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE GrupoArticulo(
	Codigo SMALLINT NOT NULL IDENTITY(1,1),
	Nombre NVARCHAR(100),
	Bloqueado NVARCHAR(1) CHECK(Bloqueado IN ('S', 'N')) DEFAULT 'N',
	CONSTRAINT pk_grp_art PRIMARY KEY(Codigo)
);

SET IDENTITY_INSERT GrupoArticulo ON;

INSERT INTO GrupoArticulo(Codigo, Nombre, Bloqueado)
VALUES(100, 'Artículos', 'N');

SET IDENTITY_INSERT GrupoArticulo OFF;

--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE FabricanteArticulo(
	Entry INT NOT NULL IDENTITY(1,1),
	Nombre NVARCHAR(30) NOT NULL,
	CONSTRAINT pk_fabricante_art PRIMARY KEY(Entry)
);

SET IDENTITY_INSERT FabricanteArticulo ON;

INSERT INTO FabricanteArticulo(Entry, Nombre)
VALUES(-1, '- Ningún fabricante -');

SET IDENTITY_INSERT FabricanteArticulo OFF;
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE UnidadMedidaArticulo(
	Entry INT NOT NULL IDENTITY(1,1),
	Codigo NVARCHAR(20) NOT NULL,
	Nombre NVARCHAR(100),
	Largo DECIMAL(21,6),
	Ancho DECIMAL(21,6),
	Altura DECIMAL(21,6),
	Volumen DECIMAL(21,6),
	Peso DECIMAL(21,6),
	Bloqueado NVARCHAR(1) CHECK(Bloqueado IN ('S', 'N')) DEFAULT 'N',
	CONSTRAINT pk_medida_art PRIMARY KEY(Entry)
);

SET IDENTITY_INSERT UnidadMedidaArticulo ON;

INSERT INTO UnidadMedidaArticulo(Entry, Codigo, Nombre, Bloqueado)
VALUES(-1, 'Manual', 'Manual', 'S');

SET IDENTITY_INSERT UnidadMedidaArticulo OFF;

--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE GrupoUnidadMedidaArticulo(
	Entry INT NOT NULL IDENTITY(1,1),
	Codigo NVARCHAR(20),
	Nombre NVARCHAR(100),
	BaseMedida INT NOT NULL,
	Bloqueado NVARCHAR(1) CHECK(Bloqueado IN ('S', 'N')) DEFAULT 'N',
	CONSTRAINT pk_grp_medida_art PRIMARY KEY(Entry),
	CONSTRAINT fk_medida_art FOREIGN KEY(BaseMedida) REFERENCES UnidadMedidaArticulo(Entry)
);

SET IDENTITY_INSERT GrupoUnidadMedidaArticulo ON;

INSERT INTO GrupoUnidadMedidaArticulo(Entry, Codigo, Nombre, BaseMedida, Bloqueado)
VALUES(-1, 'Manual', 'Manual', -1, 'S');

SET IDENTITY_INSERT GrupoUnidadMedidaArticulo OFF;
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE GrupoUnidadMedidaDetArticulo(
	GrpMedidaEntry INT NOT NULL,
	MedidaEntry INT NOT NULL,
	CantAlternativa DECIMAL(19),
	CantBase DECIMAL(19),
	NumLinea INT,
	PesoFactor	INT, -- REVISAR
	UdfFactor INT, -- REVISAR
	Activo NVARCHAR(1) CHECK (Activo IN ('S', 'N')),
	CONSTRAINT pk_grp_medida_det_art PRIMARY KEY(GrpMedidaEntry, NumLinea),
	CONSTRAINT fk_grp_medida_art FOREIGN KEY(MedidaEntry) REFERENCES UnidadMedidaArticulo(Entry)
);

INSERT INTO GrupoUnidadMedidaDetArticulo(GrpMedidaEntry, MedidaEntry, CantAlternativa, CantBase, NumLinea, UdfFactor)
VALUES(-1, -1, 1, 1, 1, -1);
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE Almacen(
	Codigo NVARCHAR(8) NOT NULL,
	Nombre NVARCHAR(100),
	Activo NVARCHAR(1) NOT NULL CHECK (Activo IN ('S', 'N')) DEFAULT 'S',
	Calle NVARCHAR(100),
	CodigoPostal NVARCHAR(20),
	Pais NVARCHAR(3),
	Municipio NVARCHAR(3),
	Departamento NVARCHAR(3),
	Bloqueado NVARCHAR(1) CHECK(Bloqueado IN ('S', 'N')) DEFAULT 'N',
	CONSTRAINT pk_almacen PRIMARY KEY(Codigo),
	CONSTRAINT fk_almacen_pais FOREIGN KEY(Pais) REFERENCES Pais(Codigo),
	CONSTRAINT fk_almacen_departamento FOREIGN KEY(Departamento, Pais) REFERENCES Departamento(Codigo, Pais),
	CONSTRAINT fk_almacen_municipio FOREIGN KEY(Municipio, Departamento, Pais) REFERENCES Municipio(Codigo, Departamento, Pais)
);

INSERT INTO Almacen(Codigo, Nombre)
VALUES('01', 'Almacén general');

--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE NumeracionDocumento(
	CodigoObj NVARCHAR(20) NOT NULL,
	SerieDfct INT DEFAULT 0,
	DocAlias NVARCHAR(20),
	SubTipoDoc NVARCHAR(2) CHECK (SubTipoDoc IN ('--', 'ND', 'DN', 'C', 'P')) DEFAULT '--' NOT NULL, -- ND = Nota Débito Clientes, DN = Nota Débito Proveedores, C = Clientes, P = Proveedores
	CONSTRAINT pk_numeracion_documento PRIMARY KEY(CodigoObj, SubTipoDoc)
);

INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(1, 1, 'Socio de negocio', 'C');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(1, 2, 'Socio de negocio', 'P');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(2, 3, 'Articulos', '--');
---------------------------------------------------------------------------
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(3, 5, 'Cotizacion', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(4, 7, 'Pedido', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(5,9, 'Entrega', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(6, 11, 'Factura', '--');
---------------------------------------------------------------------------
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(10, 21, 'Oferta Compra', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(11, 23, 'Pedido Compra', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(12, 25, 'Entrega Compra', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(13, 27, 'Factura Compra', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(59, 29, 'Entrada Mercancia', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(60, 31, 'Salida Mercancia', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(6, 33, 'Nota Debito Cliente', 'ND');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(13, 35, 'Nota Debito Prov.', 'DN');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(8, 37, 'Nota Credito Cliente', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(15, 39, 'Nota Credito Prov.', '--');
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE NumeracionDocumentoDet(
	CodigoObj NVARCHAR(20) NOT NULL,
	Serie INT DEFAULT 0 NOT NULL,
	NombreSerie NVARCHAR(8) NOT NULL,
	IniNumero INT,
	SigNumero INT,
	FinNumero INT,
	IniCadena NVARCHAR(20),
	FinCadena NVARCHAR(20),
	Comentario NVARCHAR(5),
	Bloqueado NVARCHAR(1) CHECK (Bloqueado IN ('S', 'N')) DEFAULT 'N',
	CantDigitos INT,
	SubTipoDoc NVARCHAR(2) CHECK (SubTipoDoc IN ('--', 'ND', 'DN', 'C', 'P')) DEFAULT '--' NOT NULL, -- ND = Nota Débito Clientes, DN = Nota Débito Proveedores, C = Clientes, P = Proveedores
	TipoSerie NVARCHAR(2) CHECK (TipoSerie IN ('D', 'SN', 'A')) DEFAULT 'D' NOT NULL, -- D = Documento, SN = Socio Negocio, A = Articulo
	Manual NVARCHAR(1) CHECK (Manual IN ('S', 'N')) DEFAULT 'N',
	CONSTRAINT pk_numeracion_documento_det PRIMARY KEY(Serie)
);

INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(1, 1,'Manual', 'S', 'C', 'SN', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(1, 2,'Manual', 'S', 'P', 'SN', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(2, 3,'Manual', 'S', '--', 'A', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(3, 4,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(3, 5,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(4, 6,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(4, 7,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(5, 8,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(5, 9,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(6, 10,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(6, 11,'Primario', 'N', '--', 'D', 1, 1);
------------------------------------------------------------------------------------------------
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(10, 20,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(10, 21,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(11, 22,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(11, 23,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(12, 24,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(12, 25,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(13, 26,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(13, 27,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(59, 28,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(59, 29,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(60, 30,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(60, 31,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(6, 32,'Manual', 'N', 'ND', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(6, 33,'Primario', 'N', 'ND', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(13, 34,'Manual', 'N', 'DN', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(13, 35,'Primario', 'N', 'DN', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(8, 36,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(8, 37,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(15, 38,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(15, 39,'Primario', 'N', '--', 'D', 1, 1);

--INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
--VALUES(3, 5,'Primario', 'N', 'ND', 'D', 'N');
--------------------------------------------------------------------
--------------------------------------------------------------------
-- Articulo incluye las columnas de valuacion de inventario (MetodoValuacion,
-- CostoPromedio, CostoEstandar, ValorInventario) que antes se agregaban con
-- sql/2026-08-30-inventario-nucleo.sql.
CREATE TABLE Articulo(
	Codigo NVARCHAR(15),
	Nombre NVARCHAR(200),
	CodigoGrupo SMALLINT,
	CodigoGrpUnidadMedida INT,
	FabricanteEntry INT, -- FK
	Serie INT NOT NULL,
	Activo NVARCHAR(1) CHECK(Activo IN ('S', 'N')) DEFAULT 'S',
	ArticuloCompra NVARCHAR(1) CHECK (ArticuloCompra IN ('S', 'N')) DEFAULT 'S',
	ArticuloVenta NVARCHAR(1) CHECK (ArticuloVenta IN ('S', 'N')) DEFAULT 'S',
	ArticuloInventario NVARCHAR(1) CHECK (ArticuloInventario IN ('S', 'N')) DEFAULT 'S',
	PrecioUnitario DECIMAL(19),
	CantDisponible DECIMAL(21, 6),
	CantConfirmada DECIMAL(21, 6),
	CantPedida DECIMAL(21, 6),
	AlmacenDefecto NVARCHAR(8),
	NoApliDesc NVARCHAR(1) CHECK (NoApliDesc IN ('S', 'N')) DEFAULT 'N',
	GestNoSerie NVARCHAR(1) CHECK (GestNoSerie IN ('S', 'N')) DEFAULT 'N',
	GestLote NVARCHAR(1) CHECK (GestLote  IN ('S', 'N')) DEFAULT 'N',
	GestPorAlmacen NVARCHAR(1) CHECK (GestPorAlmacen IN ('S', 'N')) DEFAULT 'N',
	Minimo DECIMAL(21, 6),
	Maximo DECIMAL(21, 6),
	Comentarios NVARCHAR(1000),
	MetodoValuacion NVARCHAR(1) NOT NULL CONSTRAINT DF_Articulo_MetodoValuacion DEFAULT ('P') CONSTRAINT CK_Articulo_MetodoValuacion CHECK (MetodoValuacion IN ('P','E')), -- P = Promedio, E = Estandar
	CostoPromedio DECIMAL(19,6) NOT NULL CONSTRAINT DF_Articulo_CostoPromedio DEFAULT (0),
	CostoEstandar DECIMAL(19,6) NOT NULL CONSTRAINT DF_Articulo_CostoEstandar DEFAULT (0),
	ValorInventario DECIMAL(19,6) NOT NULL CONSTRAINT DF_Articulo_ValorInventario DEFAULT (0),
	CONSTRAINT pk_articulo PRIMARY KEY(Codigo),
	CONSTRAINT fk_articulo_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_articulo_grupo FOREIGN KEY(CodigoGrupo) REFERENCES GrupoArticulo(Codigo),
	CONSTRAINT fk_articulo_grp_medida FOREIGN KEY(CodigoGrpUnidadMedida) REFERENCES GrupoUnidadMedidaArticulo(Entry),
	CONSTRAINT fk_articulo_fabricante FOREIGN KEY(FabricanteEntry) REFERENCES FabricanteArticulo(Entry),
	CONSTRAINT fk_articulo_almc_defecto FOREIGN KEY(AlmacenDefecto) REFERENCES Almacen(Codigo)
);
--------------------------------------------------------------------
--------------------------------------------------------------------
-- INV-1: nucleo de inventario multi-almacen (equivale a sql/2026-08-30-inventario-nucleo.sql).

-- ExistenciaArticulo: cantidad por (articulo, almacen)
CREATE TABLE ExistenciaArticulo (
	CodArticulo        NVARCHAR(15)  NOT NULL,
	CodAlmacen         NVARCHAR(8)   NOT NULL,
	Disponible         DECIMAL(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Disponible   DEFAULT (0),
	Comprometido       DECIMAL(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Comprometido DEFAULT (0),
	Pedido             DECIMAL(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Pedido       DEFAULT (0),
	FechaActualizacion DATETIME      NOT NULL CONSTRAINT DF_ExistenciaArticulo_Fecha        DEFAULT (GETDATE()),
	RowVersion         ROWVERSION    NOT NULL,
	CONSTRAINT pk_existencia_articulo PRIMARY KEY (CodArticulo, CodAlmacen),
	CONSTRAINT fk_existencia_articulo FOREIGN KEY (CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_existencia_almacen  FOREIGN KEY (CodAlmacen)  REFERENCES Almacen(Codigo)
);

-- MovimientoInventario: kardex append-only
CREATE TABLE MovimientoInventario (
	Entry              INT IDENTITY(1,1) NOT NULL,
	TipoDoc            NVARCHAR(20)  NOT NULL,
	DocEntry           INT           NOT NULL,
	DocLinea           INT           NOT NULL,
	CodArticulo        NVARCHAR(15)  NOT NULL,
	CodAlmacen         NVARCHAR(8)   NOT NULL,
	Fecha              DATETIME      NOT NULL,
	CantidadEntra      DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_CantEntra DEFAULT (0),
	CantidadSale       DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_CantSale  DEFAULT (0),
	PrecioUnitario     DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_Precio    DEFAULT (0),
	CostoUnitario      DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_Costo     DEFAULT (0),
	ValorMovimiento    DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_Valor     DEFAULT (0),
	VariacionPrecio    DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_Variacion DEFAULT (0),
	SaldoCantidad      DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoCant DEFAULT (0),
	SaldoCostoPromedio DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoCP   DEFAULT (0),
	SaldoValor         DECIMAL(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoVal  DEFAULT (0),
	MovReversaDe       INT           NULL,
	CONSTRAINT pk_movimiento_inventario PRIMARY KEY (Entry),
	CONSTRAINT fk_movimiento_articulo FOREIGN KEY (CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_movimiento_almacen  FOREIGN KEY (CodAlmacen)  REFERENCES Almacen(Codigo),
	CONSTRAINT fk_movimiento_reversa  FOREIGN KEY (MovReversaDe) REFERENCES MovimientoInventario(Entry)
);
CREATE INDEX ix_movimiento_articulo_fecha ON MovimientoInventario (CodArticulo, Fecha, Entry);
CREATE INDEX ix_movimiento_origen         ON MovimientoInventario (TipoDoc, DocEntry);
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE GrupoSN(
	Entry SMALLINT,
	Nombre NVARCHAR(200),
	TipoGrupo NVARCHAR(1) CHECK (TipoGrupo IN ('C', 'P')), -- C -> Grupo Clientes, P -> Grupo Proveedores
	Bloqueado NVARCHAR(1) CHECK (Bloqueado IN ('S', 'N')) DEFAULT 'N',
	CONSTRAINT pk_grupo_sn PRIMARY KEY(Entry) -- Revisar si es necesario otra llave primaria
);

INSERT INTO GrupoSN(Entry, Nombre, TipoGrupo)
VALUES(101, 'Clientes', 'C'),
(102, 'Proveedores', 'P');
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE ListadoPrecio(
	Entry INT NOT NULL,
	Nombre NVARCHAR(32),
	Base INT,
	Factor DECIMAL(21, 6),
	MetodoRedondeo SMALLINT CHECK (MetodoRedondeo IN (0, 1, 2, 3, 4, 5)) DEFAULT 0, -- 0=Sin redondeo, 1=Redondear al importe decimal completo, 2=Redondear al importe total, 3=Redondear al importe a decenas completas, 4=Final fijo, 5=Intervalo fijo
	ReglaRedondeo NVARCHAR(1) CHECK (ReglaRedondeo IN ('R', 'C', 'F')) DEFAULT 'R', -- R=Redondear al más cercano, C=Redondear hacia arriba, F=Redondear hacia abajo
	ExtMonto DECIMAL(21, 6),
	RndFrmtInt NVARCHAR(10),
	RndFrmtDec NVARCHAR(10),
	CONSTRAINT pk_listado_precio PRIMARY KEY(Entry)
);
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE SocioNegocio(
	Codigo NVARCHAR(15),
	Nombre NVARCHAR(200),
	TipoSN NVARCHAR(1) CHECK (TipoSN IN ('C', 'P')), -- C -> Cliente, P -> Proveedor
	Serie INT NOT NULL,
	GrupoSN SMALLINT,
	Cui NVARCHAR(15),
	Nit NVARCHAR(15),
	PersContacto NVARCHAR(75),
	Tel1 NVARCHAR(20),
	Tel2 NVARCHAR(20),
	Descuento DECIMAL(21, 6),
	NumLstPrecio INT,
	Email NVARCHAR(75),
	Activo NVARCHAR(1) CHECK (Activo IN ('S', 'N')),
	CONSTRAINT pk_socio_negocio PRIMARY KEY(Codigo),
	CONSTRAINT fk_socio_negocio_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_sn_grupo FOREIGN KEY(GrupoSN) REFERENCES GrupoSN(Entry),
	CONSTRAINT fk_sn_num_lst_precio FOREIGN KEY(NumLstPrecio) REFERENCES ListadoPrecio(Entry)
);
--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE DireccionSocioNegocio(
	Direccion NVARCHAR(50) NOT NULL,
	CodigoSN NVARCHAR(15) NOT NULL,
	Calle NVARCHAR(100),
	Bloque NVARCHAR(100),
	CodigoPostal NVARCHAR(20),
	Pais NVARCHAR(3), -- crear tabla de paises
	Municipio NVARCHAR(3),
	Departamento NVARCHAR(3),
	NumLinea INT,
	TipoDireccion NVARCHAR(3) CHECK (TipoDireccion IN ('FAC','ENV')) NOT NULL, -- FAC -> Facturación, ENV -> Envio
	CONSTRAINT pk_direccion_sn PRIMARY KEY(Direccion, CodigoSN),
	CONSTRAINT fk_dire_sn_cod_sn FOREIGN KEY(CodigoSN) REFERENCES SocioNegocio(Codigo),
	CONSTRAINT fk_dire_sn_pais FOREIGN KEY(Pais) REFERENCES Pais(Codigo),
	CONSTRAINT fk_dire_sn_departamento FOREIGN KEY(Departamento, Pais) REFERENCES Departamento(Codigo, Pais),
	CONSTRAINT fk_dire_sn_municipio FOREIGN KEY(Municipio, Departamento, Pais) REFERENCES Municipio(Codigo, Departamento, Pais)
);

--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE Usuario(
	ID INT NOT NULL,
	Password NVARCHAR(254),
	LlaveInterna INT NOT NULL IDENTITY(1,1),
	Codigo NVARCHAR(25) NOT NULL,
	Nombre NVARCHAR(150),
	Eliminado NVARCHAR(1) CHECK (Eliminado IN ('S', 'N')) DEFAULT 'N',
	SuperUsuario NVARCHAR(1) CHECK (SuperUsuario IN ('S', 'N')) DEFAULT 'N',
	Email NVARCHAR(75),
	Bloqueado NVARCHAR(1) CHECK (Bloqueado IN ('S', 'N')) DEFAULT 'N',
	UltimaContra NVARCHAR(254),
	CONSTRAINT pk_usuario PRIMARY KEY(ID)
);

-- El Codigo de usuario es unico (incluye usuarios eliminados logicamente).
-- Equivale a sql/2026-10-03-usuario-codigo-unico.sql.
CREATE UNIQUE INDEX UX_Usuario_Codigo ON Usuario (Codigo);

INSERT INTO Usuario (ID, Password, Codigo, Nombre, Email, SuperUsuario, Bloqueado, Eliminado)
VALUES (
    1,
    'AQAAAAIAAYagAAAAEPuIgGk2PJKnGc+7FOrn7jxlTtpPQi7AZWNwncHn3RyhYfWF69s5RIJ5/tqsojOpSQ==',
    'admin',
    'Administrador',
    'admin@example.com',
    'S',
    'N',
    'N'
); --Password = Admin123!

--------------------------------------------------------------------
--------------------------------------------------------------------
CREATE TABLE Moneda(
	Codigo NVARCHAR(3) NOT NULL,
	Nombre NVARCHAR(20),
	NombreImpresion NVARCHAR(3),
	Centena NVARCHAR(20),
	CodigoISO NVARCHAR(3) CHECK (CodigoISO IN ('QTZ')) DEFAULT 'QTZ',
	TipoReondeo SMALLINT CHECK (TipoReondeo IN (0)) DEFAULT 0, -- 0 = No Redondear
	CONSTRAINT pk_moneda PRIMARY KEY(Codigo)
 );

--
INSERT INTO Moneda(Codigo, Nombre, CodigoISO, TipoReondeo)
VALUES ('QTZ', 'Quetzales', 'QTZ', 0);

CREATE TABLE Impuesto(
 	Codigo NVARCHAR(8) NOT NULL,
 	Nombre NVARCHAR(100) NOT NULL,
 	Tasa DECIMAL(21,6),
	CONSTRAINT pk_impuesto PRIMARY KEY(Codigo)
);

INSERT INTO Impuesto(Codigo, Nombre, Tasa)
VALUES ('IVA', 'Impuesto al Valor Agreado', 12);

INSERT INTO Impuesto(Codigo, Nombre, Tasa)
VALUES ('EXE', 'Exento', 0);

CREATE TABLE PedidoCompra(
	Entry INT NOT NULL IDENTITY(1,1),
	NumDoc INT NOT NULL,
	Serie INT NOT NULL, -- FK
	Cancelado NVARCHAR(1) CHECK (Cancelado IN ('S', 'N')) DEFAULT 'N',
	NumManual NVARCHAR(1) CHECK (NumManual IN ('S', 'N')) DEFAULT 'N',
	Imprimido NVARCHAR(1) CHECK (Imprimido IN ('S', 'N')) DEFAULT 'N',
	EstadoDoc NVARCHAR(1) CHECK (EstadoDoc IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	EstadoInv NVARCHAR(1) CHECK (EstadoInv IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	TipoObjeto NVARCHAR(11) CHECK (TipoObjeto IN ('11')) DEFAULT '11', -- 4 = Pedido Compra
	FechaDoc DATETIME NOT NULL,
	FechaEmision DATETIME NOT NULL,
	FechaCancelado DATETIME,
	CodigoSN NVARCHAR(15)NOT NULL , -- FK
	NombreSN NVARCHAR(200),
	Direccion NVARCHAR(254),
	MonedaDoc NVARCHAR(3) NOT NULL, -- FK
	BaseTipo INT CHECK(BaseTipo IN (-1)) DEFAULT -1,
	BaseEntry INT,
	PrctjeImpuesto DECIMAL(19, 6),
	TotalImp DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	TotalDesc DECIMAL (19, 6),
	TotalBruto DECIMAL(19, 6),
	TotalDoc DECIMAL(19, 6),
	Comentario NVARCHAR(254),
	CONSTRAINT pk_pedido_compra PRIMARY KEY(Entry),
	CONSTRAINT fk_pedido_compra_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_pedido_compra_sn FOREIGN KEY(CodigoSN) REFERENCES SocioNegocio(Codigo),
	CONSTRAINT fk_pedido_compra_moneda FOREIGN KEY(MonedaDoc) REFERENCES Moneda(Codigo)
);


CREATE TABLE PedidoCompraDetalle(
	Entry INT,
	NoLinea INT,
	TipoDocDestino INT CHECK(TipoDocDestino IN (-1, 12, 13)) DEFAULT -1,
	DocDestinoEntry INT,
	BaseRef INT,
	BaseTipo INT CHECK(BaseTipo IN (-1, 10)) DEFAULT -1,
	BaseEntry INT,
	BaseLinea INT,
	EstadoLinea NVARCHAR(1) CHECK (EstadoLinea IN ('A', 'C')) DEFAULT 'A',
	CodArticulo NVARCHAR(15) NOT NULL, -- FK
	Descripcion NVARCHAR(200),
	Cantidad DECIMAL(19, 6),
	Precio DECIMAL(19, 6),
	PrecioBruto DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	CodigoImpuesto NVARCHAR(8) NOT NULL,
	Impuesto DECIMAL(19, 6),
	TotalLinea DECIMAL(19, 6),
	TipoObjeto NVARCHAR(20) CHECK(TipoObjeto IN ('11')) DEFAULT '11',
	CodAlmacen NVARCHAR(8) NOT NULL, -- FK
	CONSTRAINT pk_pedido_compra_det PRIMARY KEY(Entry, NoLinea),
	CONSTRAINT fk_pedido_compra_det_cod_art FOREIGN KEY(CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_pedido_compra_det_almacen FOREIGN KEY(CodAlmacen) REFERENCES Almacen(Codigo),
	CONSTRAINT fk_pedido_compra_det_cod_impuesto FOREIGN KEY(CodigoImpuesto) REFERENCES Impuesto(Codigo)
);


CREATE TABLE EntregaCompra(
	Entry INT NOT NULL IDENTITY(1,1),
	NumDoc INT NOT NULL,
	Serie INT NOT NULL, -- FK
	Cancelado NVARCHAR(1) CHECK (Cancelado IN ('S', 'N')) DEFAULT 'N',
	NumManual NVARCHAR(1) CHECK (NumManual IN ('S', 'N')) DEFAULT 'N',
	Imprimido NVARCHAR(1) CHECK (Imprimido IN ('S', 'N')) DEFAULT 'N',
	EstadoDoc NVARCHAR(1) CHECK (EstadoDoc IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	EstadoInv NVARCHAR(1) CHECK (EstadoInv IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	TipoObjeto NVARCHAR(11) CHECK (TipoObjeto IN ('12')) DEFAULT '12', -- 5 = Entrega Compra
	FechaDoc DATETIME NOT NULL,
	FechaEmision DATETIME NOT NULL,
	FechaCancelado DATETIME,
	CodigoSN NVARCHAR(15) NOT NULL, -- FK
	NombreSN NVARCHAR(200),
	Direccion NVARCHAR(254),
	MonedaDoc NVARCHAR(3) NOT NULL, -- FK
	BaseTipo INT CHECK(BaseTipo IN (-1)) DEFAULT -1,
	BaseEntry INT,
	PrctjeImpuesto DECIMAL(19, 6),
	TotalImp DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	TotalDesc DECIMAL (19, 6),
	TotalBruto DECIMAL(19, 6),
	TotalDoc DECIMAL(19, 6),
	Comentario NVARCHAR(254),
	CONSTRAINT pk_entrega_compra PRIMARY KEY(Entry),
	CONSTRAINT fk_entrega_compra_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_entrega_compra_sn FOREIGN KEY(CodigoSN) REFERENCES SocioNegocio(Codigo),
	CONSTRAINT fk_entrega_compra_moneda FOREIGN KEY(MonedaDoc) REFERENCES Moneda(Codigo)
);

CREATE TABLE EntregaCompraDetalle(
	Entry INT,
	NoLinea INT,
	TipoDocDestino INT CHECK(TipoDocDestino IN (-1, 13)) DEFAULT -1, -- falta agregar devoluciones
	DocDestinoEntry INT,
	BaseRef INT,
	BaseTipo INT CHECK(BaseTipo IN (-1, 10, 11)) DEFAULT -1,
	BaseEntry INT,
	BaseLinea INT,
	EstadoLinea NVARCHAR(1) CHECK (EstadoLinea IN ('A', 'C')) DEFAULT 'A',
	CodArticulo NVARCHAR(15) NOT NULL, -- FK
	Descripcion NVARCHAR(200),
	Cantidad DECIMAL(19, 6),
	Precio DECIMAL(19, 6),
	PrecioBruto DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	CodigoImpuesto NVARCHAR(8) NOT NULL,
	Impuesto DECIMAL(19, 6),
	TotalLinea DECIMAL(19, 6),
	TipoObjeto NVARCHAR(20) CHECK(TipoObjeto IN ('12')) DEFAULT '12',
	CodAlmacen NVARCHAR(8) NOT NULL, -- FK
	CONSTRAINT pk_entrega_compra_det PRIMARY KEY(Entry, NoLinea),
	CONSTRAINT fk_entrega_compra_det_cod_art FOREIGN KEY(CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_entrega_compra_det_almacen FOREIGN KEY(CodAlmacen) REFERENCES Almacen(Codigo),
	CONSTRAINT fk_entrega_compra_det_cod_impuesto FOREIGN KEY(CodigoImpuesto) REFERENCES Impuesto(Codigo)
);



CREATE TABLE FacturaCompra(
	Entry INT NOT NULL IDENTITY(1,1),
	NumDoc INT NOT NULL,
	Serie INT NOT NULL, -- FK
	Cancelado NVARCHAR(1) CHECK (Cancelado IN ('S', 'N')) DEFAULT 'N',
	NumManual NVARCHAR(1) CHECK (NumManual IN ('S', 'N')) DEFAULT 'N',
	Imprimido NVARCHAR(1) CHECK (Imprimido IN ('S', 'N')) DEFAULT 'N',
	EstadoDoc NVARCHAR(1) CHECK (EstadoDoc IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	EstadoInv NVARCHAR(1) CHECK (EstadoInv IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	TipoObjeto NVARCHAR(11) CHECK (TipoObjeto IN ('13')) DEFAULT '13', -- 13 = Factura Compra
	DocSubType NVARCHAR(2) NOT NULL CONSTRAINT DF_FacturaCompra_DocSubType DEFAULT ('--') CONSTRAINT CK_FacturaCompra_DocSubType CHECK (DocSubType IN ('--', 'DN')), -- -- = factura, DN = nota de debito de proveedor
	DocType NVARCHAR(1) NOT NULL CONSTRAINT DF_FacturaCompra_DocType DEFAULT ('I') CONSTRAINT CK_FacturaCompra_DocType CHECK (DocType IN ('I', 'S')), -- I = articulos (mueve inventario), S = concepto (la factura normal es siempre I)
	FechaDoc DATETIME NOT NULL,
	FechaEmision DATETIME NOT NULL,
	FechaCancelado DATETIME,
	CodigoSN NVARCHAR(15) NOT NULL, -- FK
	NombreSN NVARCHAR(200),
	Direccion NVARCHAR(254),
	MonedaDoc NVARCHAR(3) NOT NULL, -- FK
	BaseTipo INT DEFAULT -1,
	BaseEntry INT,
	PrctjeImpuesto DECIMAL(19, 6),
	TotalImp DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	TotalDesc DECIMAL (19, 6),
	TotalBruto DECIMAL(19, 6),
	TotalDoc DECIMAL(19, 6),
	Comentario NVARCHAR(254),
	CONSTRAINT CK_FacturaCompra_DocType_Subtipo CHECK (DocSubType <> '--' OR DocType = 'I'),
	CONSTRAINT CK_FacturaCompra_Base CHECK ((DocSubType = '--' AND BaseTipo = -1) OR (DocSubType = 'DN' AND BaseTipo IN (-1, 13))),
	CONSTRAINT pk_factura_compra PRIMARY KEY(Entry),
	CONSTRAINT fk_factura_compra_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_factura_compra_sn FOREIGN KEY(CodigoSN) REFERENCES SocioNegocio(Codigo),
	CONSTRAINT fk_factura_compra_moneda FOREIGN KEY(MonedaDoc) REFERENCES Moneda(Codigo)
);
CREATE NONCLUSTERED INDEX IX_FacturaCompra_DocSubType ON FacturaCompra (DocSubType, CodigoSN);


CREATE TABLE FacturaCompraDetalle(
	Entry INT,
	NoLinea INT,
	TipoDocDestino INT CHECK(TipoDocDestino IN (-1)) DEFAULT -1, -- Revisar y agregar la doc. devolucion
	DocDestinoEntry INT,
	BaseRef INT,
	BaseTipo INT CHECK(BaseTipo IN (-1, 10, 11, 12)) DEFAULT -1,
	BaseEntry INT,
	BaseLinea INT,
	EstadoLinea NVARCHAR(1) CHECK (EstadoLinea IN ('A', 'C')) DEFAULT 'A',
	CodArticulo NVARCHAR(15), -- FK (NULL en notas de debito por concepto)
	Descripcion NVARCHAR(200),
	Cantidad DECIMAL(19, 6),
	Precio DECIMAL(19, 6),
	PrecioBruto DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	CodigoImpuesto NVARCHAR(8) NOT NULL,
	Impuesto DECIMAL(19, 6),
	TotalLinea DECIMAL(19, 6),
	TipoObjeto NVARCHAR(20) CHECK(TipoObjeto IN ('13')) DEFAULT '13',
	CodAlmacen NVARCHAR(8), -- FK (NULL en notas de debito por concepto)
	CONSTRAINT pk_factura_compra_det PRIMARY KEY(Entry, NoLinea),
	CONSTRAINT fk_factura_compra_det_cod_art FOREIGN KEY(CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_factura_compra_det_almacen FOREIGN KEY(CodAlmacen) REFERENCES Almacen(Codigo),
	CONSTRAINT fk_factura_compra_det_cod_impuesto FOREIGN KEY(CodigoImpuesto) REFERENCES Impuesto(Codigo)
);

/*
FechaDoc NOT NULL
FechaEmision NOT NULL
CodigoSN NOT NULL
MonedaDoc NOT NULL
BaseTipo se agrega CHECK(BaseTipo IN (-1))
 */
CREATE TABLE Cotizacion(
	Entry INT NOT NULL IDENTITY(1,1),
	NumDoc INT NOT NULL,
	Serie INT NOT NULL, -- FK
	Cancelado NVARCHAR(1) CHECK (Cancelado IN ('S', 'N')) DEFAULT 'N',
	NumManual NVARCHAR(1) CHECK (NumManual IN ('S', 'N')) DEFAULT 'N',
	Imprimido NVARCHAR(1) CHECK (Imprimido IN ('S', 'N')) DEFAULT 'N',
	EstadoDoc NVARCHAR(1) CHECK (EstadoDoc IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	EstadoInv NVARCHAR(1) CHECK (EstadoInv IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	TipoObjeto NVARCHAR(11) CHECK (TipoObjeto IN ('3')) DEFAULT '3', -- 3 = Cotización
	FechaDoc DATETIME NOT NULL,
	FechaEmision DATETIME NOT NULL,
	FechaCancelado DATETIME,
	CodigoSN NVARCHAR(15) NOT NULL, -- FK
	NombreSN NVARCHAR(200),
	Direccion NVARCHAR(254),
	MonedaDoc NVARCHAR(3) NOT NULL, -- FK
	BaseTipo INT CHECK(BaseTipo IN (-1)) DEFAULT -1,
	BaseEntry INT,
	PrctjeImpuesto DECIMAL(19, 6),
	TotalImp DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	TotalDesc DECIMAL (19, 6),
	TotalBruto DECIMAL(19, 6),
	TotalDoc DECIMAL(19, 6),
	Comentario NVARCHAR(254),
	CONSTRAINT pk_cotizacion PRIMARY KEY(Entry),
	CONSTRAINT fk_cotizacion_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_cotizacion_sn FOREIGN KEY(CodigoSN) REFERENCES SocioNegocio(Codigo),
	CONSTRAINT fk_cotizacion_moneda FOREIGN KEY(MonedaDoc) REFERENCES Moneda(Codigo)
);

-- se agrega llave foranea para CodigoImpuesto
-- TipoDocDestino se agrega CHECK(TipoDocDestino IN (-1, 4, 6)) -1 =, 4 = Pedido, 6 = Factura
-- BaseTipo se agrega CHECK(BaseTipo IN (-1))
-- CodArticulo NOT NULL
-- CodAlmacen NOT NULL
-- TipoObjeto se agrega  CHECK(TipoObjeto IN ('3')) 3 = Cotizacion
CREATE TABLE CotizacionDetalle(
	Entry INT,
	NoLinea INT,
	TipoDocDestino INT CHECK(TipoDocDestino IN (-1, 4, 6)) DEFAULT -1,
	DocDestinoEntry INT,
	BaseRef INT,
	BaseTipo INT CHECK(BaseTipo IN (-1)) DEFAULT -1,
	BaseEntry INT,
	BaseLinea INT,
	EstadoLinea NVARCHAR(1) CHECK (EstadoLinea IN ('A', 'C')) DEFAULT 'A', -- A = Activo, C = Cerrado
	CodArticulo NVARCHAR(15) NOT NULL, -- FK
	Descripcion NVARCHAR(200),
	Cantidad DECIMAL(19, 6),
	Precio DECIMAL(19, 6),
	PrecioBruto DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	CodigoImpuesto NVARCHAR(8) NOT NULL,
	Impuesto DECIMAL(19, 6),
	TotalLinea DECIMAL(19, 6),
	TipoObjeto NVARCHAR(20) CHECK(TipoObjeto IN ('3')) DEFAULT '3',
	CodAlmacen NVARCHAR(8) NOT NULL, -- FK
	CONSTRAINT pk_cotizacion_det PRIMARY KEY(Entry, NoLinea),
	CONSTRAINT fk_cot_det_cod_art FOREIGN KEY(CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_cot_det_almacen FOREIGN KEY(CodAlmacen) REFERENCES Almacen(Codigo),
	CONSTRAINT fk_cot_det_cod_impuesto FOREIGN KEY(CodigoImpuesto) REFERENCES Impuesto(Codigo)
);

/*
FechaDoc NOT NULL
FechaEmision NOT NULL
CodigoSN NOT NULL
MonedaDoc NOT NULL
BaseTipo se agrega CHECK(BaseTipo IN (-1))
 */
CREATE TABLE Pedido(
	Entry INT NOT NULL IDENTITY(1,1),
	NumDoc INT NOT NULL,
	Serie INT NOT NULL, -- FK
	Cancelado NVARCHAR(1) CHECK (Cancelado IN ('S', 'N')) DEFAULT 'N',
	NumManual NVARCHAR(1) CHECK (NumManual IN ('S', 'N')) DEFAULT 'N',
	Imprimido NVARCHAR(1) CHECK (Imprimido IN ('S', 'N')) DEFAULT 'N',
	EstadoDoc NVARCHAR(1) CHECK (EstadoDoc IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	EstadoInv NVARCHAR(1) CHECK (EstadoInv IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	TipoObjeto NVARCHAR(11) CHECK (TipoObjeto IN ('4')) DEFAULT '4', -- 4 = Pedido
	FechaDoc DATETIME NOT NULL,
	FechaEmision DATETIME NOT NULL,
	FechaCancelado DATETIME,
	CodigoSN NVARCHAR(15)NOT NULL , -- FK
	NombreSN NVARCHAR(200),
	Direccion NVARCHAR(254),
	MonedaDoc NVARCHAR(3) NOT NULL, -- FK
	BaseTipo INT CHECK(BaseTipo IN (-1)) DEFAULT -1,
	BaseEntry INT,
	PrctjeImpuesto DECIMAL(19, 6),
	TotalImp DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	TotalDesc DECIMAL (19, 6),
	TotalBruto DECIMAL(19, 6),
	TotalDoc DECIMAL(19, 6),
	Comentario NVARCHAR(254),
	CONSTRAINT pk_pedido PRIMARY KEY(Entry),
	CONSTRAINT fk_pedido_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_pedido_sn FOREIGN KEY(CodigoSN) REFERENCES SocioNegocio(Codigo),
	CONSTRAINT fk_pedido_moneda FOREIGN KEY(MonedaDoc) REFERENCES Moneda(Codigo)
);

-- se agrega llaver foranea para CodigoImpuesto
-- TipoDocDestino se agrega CHECK(TipoDocDestino IN (-1, 5, 6)) -1 =,5 = Entrega, 6 = Factura
-- BaseTipo se agrega CHECK(BaseTipo IN (-1, 3)) -1 =, 3 = Cotizacion
-- CodArticulo NOT NULL
-- CodAlmacen NOT NULL
-- TipoObjeto se agrega CHECK(TipoObjeto IN ('4')) 4 = Pedido
CREATE TABLE PedidoDetalle(
	Entry INT,
	NoLinea INT,
	TipoDocDestino INT CHECK(TipoDocDestino IN (-1, 5, 6)) DEFAULT -1,
	DocDestinoEntry INT,
	BaseRef INT,
	BaseTipo INT CHECK(BaseTipo IN (-1, 3)) DEFAULT -1,
	BaseEntry INT,
	BaseLinea INT,
	EstadoLinea NVARCHAR(1) CHECK (EstadoLinea IN ('A', 'C')) DEFAULT 'A',
	CodArticulo NVARCHAR(15) NOT NULL, -- FK
	Descripcion NVARCHAR(200),
	Cantidad DECIMAL(19, 6),
	Precio DECIMAL(19, 6),
	PrecioBruto DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	CodigoImpuesto NVARCHAR(8) NOT NULL,
	Impuesto DECIMAL(19, 6),
	TotalLinea DECIMAL(19, 6),
	TipoObjeto NVARCHAR(20) CHECK(TipoObjeto IN ('4')) DEFAULT '4',
	CodAlmacen NVARCHAR(8) NOT NULL, -- FK
	CONSTRAINT pk_pedido_det PRIMARY KEY(Entry, NoLinea),
	CONSTRAINT fk_pedido_det_cod_art FOREIGN KEY(CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_pedido_det_almacen FOREIGN KEY(CodAlmacen) REFERENCES Almacen(Codigo),
	CONSTRAINT fk_pedido_det_cod_impuesto FOREIGN KEY(CodigoImpuesto) REFERENCES Impuesto(Codigo)
);

/*
FechaDoc NOT NULL
FechaEmision NOT NULL
CodigoSN NOT NULL
MonedaDoc NOT NULL
BaseTipo se agrega CHECK(BaseTipo IN (-1))
 */
CREATE TABLE Entrega(
	Entry INT NOT NULL IDENTITY(1,1),
	NumDoc INT NOT NULL,
	Serie INT NOT NULL, -- FK
	Cancelado NVARCHAR(1) CHECK (Cancelado IN ('S', 'N')) DEFAULT 'N',
	NumManual NVARCHAR(1) CHECK (NumManual IN ('S', 'N')) DEFAULT 'N',
	Imprimido NVARCHAR(1) CHECK (Imprimido IN ('S', 'N')) DEFAULT 'N',
	EstadoDoc NVARCHAR(1) CHECK (EstadoDoc IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	EstadoInv NVARCHAR(1) CHECK (EstadoInv IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	TipoObjeto NVARCHAR(11) CHECK (TipoObjeto IN ('5')) DEFAULT '5', -- 5 = Entrega
	FechaDoc DATETIME NOT NULL,
	FechaEmision DATETIME NOT NULL,
	FechaCancelado DATETIME,
	CodigoSN NVARCHAR(15) NOT NULL, -- FK
	NombreSN NVARCHAR(200),
	Direccion NVARCHAR(254),
	MonedaDoc NVARCHAR(3) NOT NULL, -- FK
	BaseTipo INT CHECK(BaseTipo IN (-1)) DEFAULT -1,
	BaseEntry INT,
	PrctjeImpuesto DECIMAL(19, 6),
	TotalImp DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	TotalDesc DECIMAL (19, 6),
	TotalBruto DECIMAL(19, 6),
	TotalDoc DECIMAL(19, 6),
	Comentario NVARCHAR(254),
	CONSTRAINT pk_entrega PRIMARY KEY(Entry),
	CONSTRAINT fk_entrega_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_entrega_sn FOREIGN KEY(CodigoSN) REFERENCES SocioNegocio(Codigo),
	CONSTRAINT fk_entrega_moneda FOREIGN KEY(MonedaDoc) REFERENCES Moneda(Codigo)
);

-- se agrega llave foranea para CodigoImpuesto
-- TipoDocDestino se agrega CHECK(TipoDocDestino IN (-1, 6)) -1 =, 6 = Factura
-- BaseTipo CHECK(BaseTipo IN (-1, 3, 4)) -1 =, 3 = Cotizacion, 4 = Pedido
-- CodArticulo NOT NULL
-- CodAlmacen NOT NULL
-- TipoObjeto se agrega CHECK(TipoObjeto IN ('5')) 5 = Entrega
CREATE TABLE EntregaDetalle(
	Entry INT,
	NoLinea INT,
	TipoDocDestino INT CHECK(TipoDocDestino IN (-1, 6)) DEFAULT -1, -- falta agregar devoluciones
	DocDestinoEntry INT,
	BaseRef INT,
	BaseTipo INT CHECK(BaseTipo IN (-1, 3, 4)) DEFAULT -1,
	BaseEntry INT,
	BaseLinea INT,
	EstadoLinea NVARCHAR(1) CHECK (EstadoLinea IN ('A', 'C')) DEFAULT 'A',
	CodArticulo NVARCHAR(15) NOT NULL, -- FK
	Descripcion NVARCHAR(200),
	Cantidad DECIMAL(19, 6),
	Precio DECIMAL(19, 6),
	PrecioBruto DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	CodigoImpuesto NVARCHAR(8) NOT NULL,
	Impuesto DECIMAL(19, 6),
	TotalLinea DECIMAL(19, 6),
	TipoObjeto NVARCHAR(20) CHECK(TipoObjeto IN ('5')) DEFAULT '5',
	CodAlmacen NVARCHAR(8) NOT NULL, -- FK
	CONSTRAINT pk_entrega_det PRIMARY KEY(Entry, NoLinea),
	CONSTRAINT fk_entrega_det_cod_art FOREIGN KEY(CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_entrega_det_almacen FOREIGN KEY(CodAlmacen) REFERENCES Almacen(Codigo),
	CONSTRAINT fk_entrega_det_cod_impuesto FOREIGN KEY(CodigoImpuesto) REFERENCES Impuesto(Codigo)
);

/*
FechaDoc NOT NULL
FechaEmision NOT NULL
CodigoSN NOT NULL
MonedaDoc NOT NULL
BaseTipo: CK_Factura_Base
 */
CREATE TABLE Factura(
	Entry INT NOT NULL IDENTITY(1,1),
	NumDoc INT NOT NULL,
	Serie INT NOT NULL, -- FK
	Cancelado NVARCHAR(1) CHECK (Cancelado IN ('S', 'N')) DEFAULT 'N',
	NumManual NVARCHAR(1) CHECK (NumManual IN ('S', 'N')) DEFAULT 'N',
	Imprimido NVARCHAR(1) CHECK (Imprimido IN ('S', 'N')) DEFAULT 'N',
	EstadoDoc NVARCHAR(1) CHECK (EstadoDoc IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	EstadoInv NVARCHAR(1) CHECK (EstadoInv IN ('A', 'C')) DEFAULT 'A', -- A = Abierto, C = Cerrado
	TipoObjeto NVARCHAR(11) CHECK (TipoObjeto IN ('6')) DEFAULT '6', -- 6 = Factura
	DocSubType NVARCHAR(2) NOT NULL CONSTRAINT DF_Factura_DocSubType DEFAULT ('--') CONSTRAINT CK_Factura_DocSubType CHECK (DocSubType IN ('--', 'ND')), -- -- = factura, ND = nota de debito de cliente
	DocType NVARCHAR(1) NOT NULL CONSTRAINT DF_Factura_DocType DEFAULT ('I') CONSTRAINT CK_Factura_DocType CHECK (DocType IN ('I', 'S')), -- I = articulos (mueve inventario), S = concepto (la factura normal es siempre I)
	FechaDoc DATETIME NOT NULL,
	FechaEmision DATETIME NOT NULL,
	FechaCancelado DATETIME,
	CodigoSN NVARCHAR(15) NOT NULL, -- FK
	NombreSN NVARCHAR(200),
	Direccion NVARCHAR(254),
	MonedaDoc NVARCHAR(3) NOT NULL, -- FK
	BaseTipo INT DEFAULT -1,
	BaseEntry INT,
	PrctjeImpuesto DECIMAL(19, 6),
	TotalImp DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	TotalDesc DECIMAL (19, 6),
	TotalBruto DECIMAL(19, 6),
	TotalDoc DECIMAL(19, 6),
	Comentario NVARCHAR(254),
	CONSTRAINT CK_Factura_DocType_Subtipo CHECK (DocSubType <> '--' OR DocType = 'I'),
	CONSTRAINT CK_Factura_Base CHECK ((DocSubType = '--' AND BaseTipo = -1) OR (DocSubType = 'ND' AND BaseTipo IN (-1, 6))),
	CONSTRAINT pk_factura PRIMARY KEY(Entry),
	CONSTRAINT fk_factura_serie FOREIGN KEY(Serie) REFERENCES NumeracionDocumentoDet(Serie),
	CONSTRAINT fk_factura_sn FOREIGN KEY(CodigoSN) REFERENCES SocioNegocio(Codigo),
	CONSTRAINT fk_factura_moneda FOREIGN KEY(MonedaDoc) REFERENCES Moneda(Codigo)
);
CREATE NONCLUSTERED INDEX IX_Factura_DocSubType ON Factura (DocSubType, CodigoSN);

-- se agrega llave foranea para CodigoImpuesto
-- TipoDocDestino se agrega CHECK(TipoDocDestino IN (-1)) -1 =
-- BaseTipo CHECK(BaseTipo IN (-1, 3, 4, 5)) -1 =, 3 = Cotizacion, 4 = Pedido, 5 = Entrega
-- CodArticulo y CodAlmacen: NOT NULL en facturas (lo exige FacturaDomain); NULL solo en notas de debito por concepto
-- TipoObjeto se agrega CHECK(TipoObjeto IN ('6')) 6 = Factura
CREATE TABLE FacturaDetalle(
	Entry INT,
	NoLinea INT,
	TipoDocDestino INT CHECK(TipoDocDestino IN (-1)) DEFAULT -1, -- Revisar y agregar la doc. devolucion
	DocDestinoEntry INT,
	BaseRef INT,
	BaseTipo INT CHECK(BaseTipo IN (-1, 3, 4, 5)) DEFAULT -1,
	BaseEntry INT,
	BaseLinea INT,
	EstadoLinea NVARCHAR(1) CHECK (EstadoLinea IN ('A', 'C')) DEFAULT 'A',
	CodArticulo NVARCHAR(15), -- FK (NULL en notas de debito por concepto)
	Descripcion NVARCHAR(200),
	Cantidad DECIMAL(19, 6),
	Precio DECIMAL(19, 6),
	PrecioBruto DECIMAL(19, 6),
	PrctjeDesc DECIMAL(19, 6),
	CodigoImpuesto NVARCHAR(8) NOT NULL,
	Impuesto DECIMAL(19, 6),
	TotalLinea DECIMAL(19, 6),
	TipoObjeto NVARCHAR(20) CHECK(TipoObjeto IN ('6')) DEFAULT '6',
	CodAlmacen NVARCHAR(8), -- FK (NULL en notas de debito por concepto)
	CONSTRAINT pk_factura_det PRIMARY KEY(Entry, NoLinea),
	CONSTRAINT fk_factura_det_cod_art FOREIGN KEY(CodArticulo) REFERENCES Articulo(Codigo),
	CONSTRAINT fk_factura_det_almacen FOREIGN KEY(CodAlmacen) REFERENCES Almacen(Codigo),
	CONSTRAINT fk_factura_det_cod_impuesto FOREIGN KEY(CodigoImpuesto) REFERENCES Impuesto(Codigo)
);

-- ===== NotaCredito (cliente, ObjType 8) =====
CREATE TABLE dbo.NotaCredito (
    Entry          int identity(1,1) NOT NULL,
    NumDoc         int           NOT NULL,
    Serie          int           NOT NULL,
    Cancelado      nvarchar(1)   NULL CONSTRAINT DF_NotaCredito_Cancelado DEFAULT ('N') CONSTRAINT CK_NotaCredito_Cancelado CHECK (Cancelado IN ('S','N')),
    NumManual      nvarchar(1)   NULL CONSTRAINT DF_NotaCredito_NumManual DEFAULT ('N') CONSTRAINT CK_NotaCredito_NumManual CHECK (NumManual IN ('S','N')),
    Imprimido      nvarchar(1)   NULL CONSTRAINT DF_NotaCredito_Imprimido DEFAULT ('N') CONSTRAINT CK_NotaCredito_Imprimido CHECK (Imprimido IN ('S','N')),
    EstadoDoc      nvarchar(1)   NULL CONSTRAINT DF_NotaCredito_EstadoDoc DEFAULT ('A') CONSTRAINT CK_NotaCredito_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
    TipoObjeto     nvarchar(11)  NULL CONSTRAINT DF_NotaCredito_TipoObjeto DEFAULT ('8') CONSTRAINT CK_NotaCredito_TipoObjeto CHECK (TipoObjeto IN ('8')),
    FechaDoc       datetime      NOT NULL,
    FechaEmision   datetime      NOT NULL,
    FechaCancelado datetime      NULL,
    CodigoSN       nvarchar(15)  NOT NULL,
    NombreSN       nvarchar(200) NULL,
    Direccion      nvarchar(254) NULL,
    MonedaDoc      nvarchar(3)   NOT NULL,
    BaseTipo       int           NULL CONSTRAINT DF_NotaCredito_BaseTipo DEFAULT (-1) CONSTRAINT CK_NotaCredito_BaseTipo CHECK (BaseTipo IN (-1, 6)),
    BaseEntry      int           NULL,
    PrctjeImpuesto decimal(19,6) NULL,
    TotalImp       decimal(19,6) NULL,
    PrctjeDesc     decimal(19,6) NULL,
    TotalDesc      decimal(19,6) NULL,
    TotalBruto     decimal(19,6) NULL,
    TotalDoc       decimal(19,6) NULL,
    Comentario     nvarchar(254) NULL,
    CONSTRAINT pk_nota_credito PRIMARY KEY (Entry),
    CONSTRAINT fk_nota_credito_serie  FOREIGN KEY (Serie)     REFERENCES dbo.NumeracionDocumentoDet(Serie),
    CONSTRAINT fk_nota_credito_sn     FOREIGN KEY (CodigoSN)  REFERENCES dbo.SocioNegocio(Codigo),
    CONSTRAINT fk_nota_credito_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda(Codigo)
);

CREATE NONCLUSTERED INDEX IX_NotaCredito_Base ON dbo.NotaCredito (BaseTipo, BaseEntry) INCLUDE (Cancelado, TotalDoc);

CREATE TABLE dbo.NotaCreditoDetalle (
    Entry          int           NOT NULL,
    NoLinea        int           NOT NULL,
    Descripcion    nvarchar(200) NOT NULL,
    Cantidad       decimal(19,6) NULL,
    Precio         decimal(19,6) NULL,
    PrecioBruto    decimal(19,6) NULL,
    PrctjeDesc     decimal(19,6) NULL,
    CodigoImpuesto nvarchar(8)   NOT NULL,
    Impuesto       decimal(19,6) NULL,
    TotalLinea     decimal(19,6) NULL,
    TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_NotaCreditoDet_TipoObjeto DEFAULT ('8') CONSTRAINT CK_NotaCreditoDet_TipoObjeto CHECK (TipoObjeto IN ('8')),
    CONSTRAINT pk_nota_credito_det PRIMARY KEY (Entry, NoLinea),
    CONSTRAINT fk_nota_credito_det_cod_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo)
);

-- ===== NotaCreditoCompra (proveedor, ObjType 15) =====
CREATE TABLE dbo.NotaCreditoCompra (
    Entry          int identity(1,1) NOT NULL,
    NumDoc         int           NOT NULL,
    Serie          int           NOT NULL,
    Cancelado      nvarchar(1)   NULL CONSTRAINT DF_NotaCreditoCompra_Cancelado DEFAULT ('N') CONSTRAINT CK_NotaCreditoCompra_Cancelado CHECK (Cancelado IN ('S','N')),
    NumManual      nvarchar(1)   NULL CONSTRAINT DF_NotaCreditoCompra_NumManual DEFAULT ('N') CONSTRAINT CK_NotaCreditoCompra_NumManual CHECK (NumManual IN ('S','N')),
    Imprimido      nvarchar(1)   NULL CONSTRAINT DF_NotaCreditoCompra_Imprimido DEFAULT ('N') CONSTRAINT CK_NotaCreditoCompra_Imprimido CHECK (Imprimido IN ('S','N')),
    EstadoDoc      nvarchar(1)   NULL CONSTRAINT DF_NotaCreditoCompra_EstadoDoc DEFAULT ('A') CONSTRAINT CK_NotaCreditoCompra_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
    TipoObjeto     nvarchar(11)  NULL CONSTRAINT DF_NotaCreditoCompra_TipoObjeto DEFAULT ('15') CONSTRAINT CK_NotaCreditoCompra_TipoObjeto CHECK (TipoObjeto IN ('15')),
    FechaDoc       datetime      NOT NULL,
    FechaEmision   datetime      NOT NULL,
    FechaCancelado datetime      NULL,
    CodigoSN       nvarchar(15)  NOT NULL,
    NombreSN       nvarchar(200) NULL,
    Direccion      nvarchar(254) NULL,
    MonedaDoc      nvarchar(3)   NOT NULL,
    BaseTipo       int           NULL CONSTRAINT DF_NotaCreditoCompra_BaseTipo DEFAULT (-1) CONSTRAINT CK_NotaCreditoCompra_BaseTipo CHECK (BaseTipo IN (-1, 13)),
    BaseEntry      int           NULL,
    PrctjeImpuesto decimal(19,6) NULL,
    TotalImp       decimal(19,6) NULL,
    PrctjeDesc     decimal(19,6) NULL,
    TotalDesc      decimal(19,6) NULL,
    TotalBruto     decimal(19,6) NULL,
    TotalDoc       decimal(19,6) NULL,
    Comentario     nvarchar(254) NULL,
    CONSTRAINT pk_nota_credito_compra PRIMARY KEY (Entry),
    CONSTRAINT fk_nota_credito_compra_serie  FOREIGN KEY (Serie)     REFERENCES dbo.NumeracionDocumentoDet(Serie),
    CONSTRAINT fk_nota_credito_compra_sn     FOREIGN KEY (CodigoSN)  REFERENCES dbo.SocioNegocio(Codigo),
    CONSTRAINT fk_nota_credito_compra_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda(Codigo)
);

CREATE NONCLUSTERED INDEX IX_NotaCreditoCompra_Base ON dbo.NotaCreditoCompra (BaseTipo, BaseEntry) INCLUDE (Cancelado, TotalDoc);

CREATE TABLE dbo.NotaCreditoCompraDetalle (
    Entry          int           NOT NULL,
    NoLinea        int           NOT NULL,
    Descripcion    nvarchar(200) NOT NULL,
    Cantidad       decimal(19,6) NULL,
    Precio         decimal(19,6) NULL,
    PrecioBruto    decimal(19,6) NULL,
    PrctjeDesc     decimal(19,6) NULL,
    CodigoImpuesto nvarchar(8)   NOT NULL,
    Impuesto       decimal(19,6) NULL,
    TotalLinea     decimal(19,6) NULL,
    TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_NotaCreditoCompraDet_TipoObjeto DEFAULT ('15') CONSTRAINT CK_NotaCreditoCompraDet_TipoObjeto CHECK (TipoObjeto IN ('15')),
    CONSTRAINT pk_nota_credito_compra_det PRIMARY KEY (Entry, NoLinea),
    CONSTRAINT fk_nota_credito_compra_det_cod_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo)
);

-------------------------------------------------------------------------------------------------

-- INV-4: Entrada y Salida de Mercancias (ajustes de inventario sin socio de negocio).
-- Idempotente: cada objeto se crea solo si no existe.
-- Referencia: OIGN/IGN1 y OIGE/IGE1 de SAP B1, recortado a lo que el proyecto usa.
SET NOCOUNT ON;

-- ===== EntradaMercancia (ObjType 59) =====
IF OBJECT_ID('dbo.EntradaMercancia', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntradaMercancia (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL CONSTRAINT DF_EntradaMerc_NumDoc     DEFAULT (0),
        Serie          int           NOT NULL,
        NumManual      nvarchar(1)   NOT NULL CONSTRAINT DF_EntradaMerc_NumManual  DEFAULT ('N') CONSTRAINT CK_EntradaMerc_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      nvarchar(1)   NOT NULL CONSTRAINT DF_EntradaMerc_Imprimido  DEFAULT ('N'),
        EstadoDoc      nvarchar(1)   NOT NULL CONSTRAINT DF_EntradaMerc_EstadoDoc  DEFAULT ('A') CONSTRAINT CK_EntradaMerc_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        EstadoInv      nvarchar(1)   NOT NULL CONSTRAINT DF_EntradaMerc_EstadoInv  DEFAULT ('A') CONSTRAINT CK_EntradaMerc_EstadoInv CHECK (EstadoInv IN ('A','C')),
        Cancelado      nvarchar(1)   NOT NULL CONSTRAINT DF_EntradaMerc_Cancelado  DEFAULT ('N') CONSTRAINT CK_EntradaMerc_Cancelado CHECK (Cancelado IN ('S','N')),
        TipoObjeto     nvarchar(11)  NOT NULL CONSTRAINT DF_EntradaMerc_TipoObjeto DEFAULT ('59'),
        FechaDoc       datetime      NULL,
        FechaContab    datetime      NULL,
        FechaCancelado datetime      NULL,
        Referencia     nvarchar(100) NULL,
        Comentario     nvarchar(254) NULL,
        TotalDoc       decimal(19,6) NOT NULL CONSTRAINT DF_EntradaMerc_TotalDoc   DEFAULT (0),
        CONSTRAINT pk_entrada_mercancia PRIMARY KEY (Entry),
        CONSTRAINT fk_entrada_mercancia_serie FOREIGN KEY (Serie) REFERENCES dbo.NumeracionDocumentoDet(Serie)
    );
END

-- ===== EntradaMercanciaDetalle =====
IF OBJECT_ID('dbo.EntradaMercanciaDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntradaMercanciaDetalle (
        Entry         int           NOT NULL,
        NoLinea       int           NOT NULL,
        CodArticulo   nvarchar(15)  NULL,
        Descripcion   nvarchar(254) NULL,
        Cantidad      decimal(19,6) NULL,
        CostoUnitario decimal(19,6) NOT NULL CONSTRAINT DF_EntradaMercDet_Costo DEFAULT (0),
        TotalLinea    decimal(19,6) NULL,
        CodAlmacen    nvarchar(8)   NULL,
        CONSTRAINT pk_entrada_mercancia_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_entrada_mercancia_det_art FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_entrada_mercancia_det_alm FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo)
    );
END



-- ===== SalidaMercancia (ObjType 60) — misma estructura, default TipoObjeto '60' =====
IF OBJECT_ID('dbo.SalidaMercancia', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalidaMercancia (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL CONSTRAINT DF_SalidaMerc_NumDoc     DEFAULT (0),
        Serie          int           NOT NULL,
        NumManual      nvarchar(1)   NOT NULL CONSTRAINT DF_SalidaMerc_NumManual  DEFAULT ('N') CONSTRAINT CK_SalidaMerc_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      nvarchar(1)   NOT NULL CONSTRAINT DF_SalidaMerc_Imprimido  DEFAULT ('N'),
        EstadoDoc      nvarchar(1)   NOT NULL CONSTRAINT DF_SalidaMerc_EstadoDoc  DEFAULT ('A') CONSTRAINT CK_SalidaMerc_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        EstadoInv      nvarchar(1)   NOT NULL CONSTRAINT DF_SalidaMerc_EstadoInv  DEFAULT ('A') CONSTRAINT CK_SalidaMerc_EstadoInv CHECK (EstadoInv IN ('A','C')),
        Cancelado      nvarchar(1)   NOT NULL CONSTRAINT DF_SalidaMerc_Cancelado  DEFAULT ('N') CONSTRAINT CK_SalidaMerc_Cancelado CHECK (Cancelado IN ('S','N')),
        TipoObjeto     nvarchar(11)  NOT NULL CONSTRAINT DF_SalidaMerc_TipoObjeto DEFAULT ('60'),
        FechaDoc       datetime      NULL,
        FechaContab    datetime      NULL,
        FechaCancelado datetime      NULL,
        Referencia     nvarchar(100) NULL,
        Comentario     nvarchar(254) NULL,
        TotalDoc       decimal(19,6) NOT NULL CONSTRAINT DF_SalidaMerc_TotalDoc   DEFAULT (0),
        CONSTRAINT pk_salida_mercancia PRIMARY KEY (Entry),
        CONSTRAINT fk_salida_mercancia_serie FOREIGN KEY (Serie) REFERENCES dbo.NumeracionDocumentoDet(Serie)
    );
END


-- ===== SalidaMercanciaDetalle =====
IF OBJECT_ID('dbo.SalidaMercanciaDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalidaMercanciaDetalle (
        Entry         int           NOT NULL,
        NoLinea       int           NOT NULL,
        CodArticulo   nvarchar(15)  NULL,
        Descripcion   nvarchar(254) NULL,
        Cantidad      decimal(19,6) NULL,
        CostoUnitario decimal(19,6) NOT NULL CONSTRAINT DF_SalidaMercDet_Costo DEFAULT (0),
        TotalLinea    decimal(19,6) NULL,
        CodAlmacen    nvarchar(8)   NULL,
        CONSTRAINT pk_salida_mercancia_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_salida_mercancia_det_art FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_salida_mercancia_det_alm FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo)
    );
END

-- Correccion de DEFAULT de FacturaDetalle.TipoObjeto (debe ser '6'). Idempotente; en una base
-- nueva creada con este script ya viene en '6' y no hace nada.
DECLARE @constraint sysname;
SELECT @constraint = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.FacturaDetalle')
  AND c.name = 'TipoObjeto'
  AND dc.definition <> N'(''6'')';

IF @constraint IS NOT NULL
BEGIN
    DECLARE @sqlDrop nvarchar(400) = N'ALTER TABLE dbo.FacturaDetalle DROP CONSTRAINT ' + QUOTENAME(@constraint);
    EXEC(@sqlDrop);
    EXEC('ALTER TABLE dbo.FacturaDetalle ADD CONSTRAINT DF_FacturaDetalle_TipoObjeto DEFAULT (''6'') FOR TipoObjeto');
END

-------------------------------------------------------------------------------------------------
-- PERMISOS DE USUARIO: catalogo de modulos, matriz por usuario y plantillas reutilizables.
-- (equivale a sql/2026-10-01-permisos-usuario.sql)
-- Un usuario SIN filas en UsuarioPermiso conserva acceso total (salvo el modulo Usuarios, que es
-- solo de SuperUsuario). El SuperUsuario ignora esta matriz.
-------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Modulo')
BEGIN
    CREATE TABLE Modulo (
        Codigo nvarchar(40) NOT NULL PRIMARY KEY,
        Nombre nvarchar(100) NOT NULL,
        Grupo nvarchar(40) NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UsuarioPermiso')
BEGIN
    CREATE TABLE UsuarioPermiso (
        UsuarioId int NOT NULL,
        CodigoModulo nvarchar(40) NOT NULL,
        Ver bit NOT NULL DEFAULT 0,
        Agregar bit NOT NULL DEFAULT 0,
        Actualizar bit NOT NULL DEFAULT 0,
        Eliminar bit NOT NULL DEFAULT 0,
        CONSTRAINT pk_usuario_permiso PRIMARY KEY (UsuarioId, CodigoModulo),
        CONSTRAINT fk_usuario_permiso_usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(ID),
        CONSTRAINT fk_usuario_permiso_modulo FOREIGN KEY (CodigoModulo) REFERENCES Modulo(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Plantilla')
BEGIN
    CREATE TABLE Plantilla (
        Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Nombre nvarchar(100) NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PlantillaPermisoDetalle')
BEGIN
    CREATE TABLE PlantillaPermisoDetalle (
        PlantillaId int NOT NULL,
        CodigoModulo nvarchar(40) NOT NULL,
        Ver bit NOT NULL DEFAULT 0,
        Agregar bit NOT NULL DEFAULT 0,
        Actualizar bit NOT NULL DEFAULT 0,
        Eliminar bit NOT NULL DEFAULT 0,
        CONSTRAINT pk_plantilla_permiso_detalle PRIMARY KEY (PlantillaId, CodigoModulo),
        CONSTRAINT fk_plantilla_permiso_plantilla FOREIGN KEY (PlantillaId) REFERENCES Plantilla(Id),
        CONSTRAINT fk_plantilla_permiso_modulo FOREIGN KEY (CodigoModulo) REFERENCES Modulo(Codigo)
    );
END

MERGE Modulo AS target
USING (VALUES
    ('Articulos', 'Artículos', 'Inventario'),
    ('Fabricantes', 'Fabricantes', 'Inventario'),
    ('GruposArticulo', 'Grupos de artículo', 'Inventario'),
    ('UnidadesMedida', 'Unidades de medida', 'Inventario'),
    ('GruposUnidadMedida', 'Grupo unidades de medida', 'Inventario'),
    ('Almacenes', 'Almacenes', 'Inventario'),
    ('Existencias', 'Existencias', 'Inventario'),
    ('EntradasMercancia', 'Entradas de mercancía', 'Inventario'),
    ('SalidasMercancia', 'Salidas de mercancía', 'Inventario'),
    ('SociosNegocio', 'Socios de Negocio', 'SociosNegocio'),
    ('GruposSn', 'Grupos SN', 'SociosNegocio'),
    ('ListadosPrecio', 'Listado de Precios', 'SociosNegocio'),
    ('NumeracionDocumento', 'Numeración de documentos', 'Documentos'),
    ('Cotizaciones', 'Cotizaciones', 'Ventas'),
    ('Pedidos', 'Pedidos', 'Ventas'),
    ('Entregas', 'Entregas', 'Ventas'),
    ('Facturas', 'Facturas', 'Ventas'),
    ('PedidosCompra', 'Pedidos de compra', 'Compras'),
    ('EntregasCompra', 'Entregas de compra', 'Compras'),
    ('FacturasCompra', 'Facturas de compra', 'Compras'),
    ('NotasDebito', 'Notas de débito', 'Ventas'),
    ('NotasDebitoCompra', 'Notas de débito de compra', 'Compras'),
    ('NotasCredito', 'Notas de crédito', 'Ventas'),
    ('NotasCreditoCompra', 'Notas de crédito de compra', 'Compras'),
    ('Usuarios', 'Usuarios', 'Administracion')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

-------------------------------------------------------------------------------------------------
-- TALLER: ordenes de servicio de motocicletas, mecanicos y motocicletas (equivale a
-- sql/2026-10-22-ordenes-servicio-taller.sql). Idempotente; amplia el CHECK de FacturaDetalle.BaseTipo a 70.
-------------------------------------------------------------------------------------------------

-- ===== Mecanico =====
IF OBJECT_ID('dbo.Mecanico', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Mecanico (
        Codigo   nvarchar(10)  NOT NULL,
        Nombre   nvarchar(100) NOT NULL,
        Telefono nvarchar(30)  NULL,
        Activo   nvarchar(1)   NOT NULL CONSTRAINT DF_Mecanico_Activo DEFAULT ('S') CONSTRAINT CK_Mecanico_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_mecanico PRIMARY KEY (Codigo)
    );
END

-- ===== Motocicleta =====
IF OBJECT_ID('dbo.Motocicleta', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Motocicleta (
        Entry      int identity(1,1) NOT NULL,
        CodigoSN   nvarchar(15)  NOT NULL,
        Placa      nvarchar(15)  NOT NULL,
        Marca      nvarchar(50)  NULL,
        Modelo     nvarchar(50)  NULL,
        Anio       smallint      NULL,
        Color      nvarchar(30)  NULL,
        Vin        nvarchar(30)  NULL,
        Comentario nvarchar(254) NULL,
        Activo     nvarchar(1)   NOT NULL CONSTRAINT DF_Motocicleta_Activo DEFAULT ('S') CONSTRAINT CK_Motocicleta_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_motocicleta PRIMARY KEY (Entry),
        CONSTRAINT uq_motocicleta_placa UNIQUE (Placa),
        CONSTRAINT fk_motocicleta_sn FOREIGN KEY (CodigoSN) REFERENCES dbo.SocioNegocio(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Motocicleta_CodigoSN' AND object_id = OBJECT_ID('dbo.Motocicleta'))
    CREATE NONCLUSTERED INDEX IX_Motocicleta_CodigoSN ON dbo.Motocicleta (CodigoSN);

-- ===== OrdenServicio (ObjType 70) =====
IF OBJECT_ID('dbo.OrdenServicio', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrdenServicio (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_Cancelado DEFAULT ('N') CONSTRAINT CK_OrdenServicio_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_NumManual DEFAULT ('N') CONSTRAINT CK_OrdenServicio_NumManual CHECK (NumManual IN ('S','N')),
        TipoObjeto     nvarchar(11)  NOT NULL CONSTRAINT DF_OrdenServicio_TipoObjeto DEFAULT ('70') CONSTRAINT CK_OrdenServicio_TipoObjeto CHECK (TipoObjeto IN ('70')),
        FechaDoc       datetime      NOT NULL,
        FechaIngreso   datetime      NULL,
        FechaPromesa   datetime      NULL,
        FechaEntrega   datetime      NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        MotoEntry      int           NOT NULL,
        CodMecanico    nvarchar(10)  NULL,
        KmIngreso      int           NULL,
        FallaReportada nvarchar(500) NULL,
        Diagnostico    nvarchar(500) NULL,
        Observaciones  nvarchar(500) NULL,
        EstadoServicio nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_Estado DEFAULT ('R') CONSTRAINT CK_OrdenServicio_Estado CHECK (EstadoServicio IN ('R','P','T','E')),
        TotalBruto     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_orden_servicio PRIMARY KEY (Entry),
        CONSTRAINT fk_orden_servicio_serie    FOREIGN KEY (Serie)       REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_orden_servicio_sn       FOREIGN KEY (CodigoSN)    REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_orden_servicio_moneda   FOREIGN KEY (MonedaDoc)   REFERENCES dbo.Moneda(Codigo),
        CONSTRAINT fk_orden_servicio_moto     FOREIGN KEY (MotoEntry)   REFERENCES dbo.Motocicleta(Entry),
        CONSTRAINT fk_orden_servicio_mecanico FOREIGN KEY (CodMecanico) REFERENCES dbo.Mecanico(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrdenServicio_Moto' AND object_id = OBJECT_ID('dbo.OrdenServicio'))
    CREATE NONCLUSTERED INDEX IX_OrdenServicio_Moto ON dbo.OrdenServicio (MotoEntry) INCLUDE (Cancelado, EstadoServicio);

-- ===== OrdenServicioDetalle =====
IF OBJECT_ID('dbo.OrdenServicioDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrdenServicioDetalle (
        Entry          int           NOT NULL,
        NoLinea        int           NOT NULL,
        CodArticulo    nvarchar(15)  NOT NULL,
        Descripcion    nvarchar(200) NULL,
        CodAlmacen     nvarchar(8)   NULL,
        EsInsumo       nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicioDet_EsInsumo DEFAULT ('N') CONSTRAINT CK_OrdenServicioDet_EsInsumo CHECK (EsInsumo IN ('S','N')),
        Cantidad       decimal(19,6) NULL,
        Precio         decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        CodigoImpuesto nvarchar(8)   NOT NULL,
        Impuesto       decimal(19,6) NULL,
        TotalLinea     decimal(19,6) NULL,
        CodMecanico    nvarchar(10)  NULL,
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_OrdenServicioDet_TipoObjeto DEFAULT ('70') CONSTRAINT CK_OrdenServicioDet_TipoObjeto CHECK (TipoObjeto IN ('70')),
        CONSTRAINT pk_orden_servicio_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_orden_servicio_det_orden    FOREIGN KEY (Entry)          REFERENCES dbo.OrdenServicio(Entry),
        CONSTRAINT fk_orden_servicio_det_cod_art  FOREIGN KEY (CodArticulo)    REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_orden_servicio_det_almacen  FOREIGN KEY (CodAlmacen)     REFERENCES dbo.Almacen(Codigo),
        CONSTRAINT fk_orden_servicio_det_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo),
        CONSTRAINT fk_orden_servicio_det_mecanico FOREIGN KEY (CodMecanico)    REFERENCES dbo.Mecanico(Codigo)
    );
END

-- ===== FacturaDetalle.BaseTipo admite 70 (orden de servicio como documento base) =====
-- El CHECK original es de columna y tiene nombre generado por SQL Server: se busca por definicion.
DECLARE @ck sysname;
DECLARE @sqlck nvarchar(400);
WHILE 1 = 1
BEGIN
    SET @ck = NULL;
    SELECT TOP 1 @ck = cc.name
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID('dbo.FacturaDetalle')
      AND cc.definition LIKE '%BaseTipo%'
      AND cc.definition NOT LIKE '%70%';
    IF @ck IS NULL BREAK;
    SET @sqlck = N'ALTER TABLE dbo.FacturaDetalle DROP CONSTRAINT ' + QUOTENAME(@ck);
    EXEC (@sqlck);
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FacturaDetalle') AND name = 'CK_FacturaDetalle_BaseTipo')
    ALTER TABLE dbo.FacturaDetalle ADD CONSTRAINT CK_FacturaDetalle_BaseTipo CHECK (BaseTipo IN (-1, 3, 4, 5, 70));

-- ===== Numeracion (serie "Primario" y fila padre para la pantalla "Numeracion de documentos") =====
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('70', (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet), 'Primario', 'N', '--', 'D', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '70' AND SubTipoDoc = '--')
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SubTipoDoc, DocAlias, SerieDfct)
    SELECT '70', '--', 'Orden de servicio', MIN(Serie) FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70';

-- ===== Catalogo de permisos (grupo Taller) =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('Mecanicos', 'Mecánicos', 'Taller'),
    ('Motocicletas', 'Motocicletas', 'Taller'),
    ('OrdenesServicio', 'Órdenes de servicio', 'Taller')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'Taller DDL aplicado.';
SELECT name FROM sys.tables WHERE name IN ('Mecanico','Motocicleta','OrdenServicio','OrdenServicioDetalle') ORDER BY name;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70';
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Grupo = 'Taller';
SELECT name, definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FacturaDetalle') AND definition LIKE '%BaseTipo%';
