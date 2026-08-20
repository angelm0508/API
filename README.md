# API

API REST desarrollada en .NET 7 con arquitectura por capas (Domain, Application, Infraestructure y Service.WebApi).

## Estructura del proyecto

- **API.Domain.Core / API.Domain.Entity / API.Domain.Interface**: entidades de negocio e interfaces del dominio.
- **API.Application.DTO / API.Application.Interface / API.Application.Main**: casos de uso, DTOs y lógica de aplicación.
- **API.Infraestructure.Data / API.Infraestructure.Interface / API.Infraestructure.Repository**: acceso a datos y repositorios.
- **API.Transversal.Common / API.Transversal.Mapper**: utilidades y mapeos compartidos entre capas.
- **API.Service.WebApi**: proyecto expuesto como API REST, con controladores para entidades como artículos, almacenes, socios de negocio, monedas, países, municipios, entre otros.

## Requisitos

- .NET 7 SDK

## Cómo ejecutar

```bash
dotnet build API.sln
dotnet run --project API.Service.WebApi
```
