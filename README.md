# Training Platform — Fase 1 (Fundaciones)

Base de la plataforma de adiestramientos: **database-per-tenant con una sola aplicación**.
Una app, un catálogo central, y una base de datos por cliente que se resuelve **según quién
inicia sesión**.

Esto es la Fase 1 del `SPEC_Adiestramientos.md`: catálogo + resolución de tenant por request +
runner de migraciones multi-DB + auth/RBAC + modelo de dominio. Las fases siguientes (entrega
autodirigida por REST, entrega facilitada por SignalR, reporting) van encima de esto.

## Requisitos
- .NET 8 SDK
- SQL Server accesible (local o remoto)
- Herramienta EF: `dotnet tool install --global dotnet-ef`

## 1. Configurar
Edita `appsettings.json`:
- `ConnectionStrings:Catalog` — base de datos del **catálogo** (tenants + usuarios).
- `ConnectionStrings:DesignTenant` — base "plantilla" que EF usa solo para **generar**
  migraciones del esquema de cliente (no guarda datos reales).
- `Jwt:Key` — secreto largo y aleatorio (mínimo 32 bytes).
- `Bootstrap:AdminEmail` / `AdminPassword` — admin de plataforma que se siembra al primer arranque.

## 2. Generar las migraciones iniciales (una sola vez)
Hay dos `DbContext`, así que cada uno lleva su set de migraciones:

```bash
dotnet ef migrations add InitialCatalog --context CatalogDbContext -o Migrations/Catalog
dotnet ef migrations add InitialTenant  --context TenantDbContext  -o Migrations/Tenant
```

## 3. Correr
```bash
dotnet run
```
Al arrancar: aplica las migraciones del **catálogo** y siembra el admin de plataforma.
(Si prefieres correr en un puerto accesible en red: `dotnet run --urls http://0.0.0.0:8080`.)

## 4. Flujo de uso (API)
1. **Login del admin** → `POST /auth/login` con `{ "email": "...", "password": "..." }`.
   Devuelve un JWT. Mándalo como `Authorization: Bearer <token>` en lo demás.
2. **Crear un cliente (tenant)** → `POST /admin/tenants`
   `{ "name": "Cliente A", "connectionString": "Server=...;Database=TP_ClienteA;..." }`.
   Crea la fila en el catálogo **y crea/migra la base del cliente automáticamente**.
3. **Crear usuarios del cliente** → `POST /admin/users`
   `{ "email": "...", "name": "...", "password": "...", "role": "Author", "tenantId": "<guid>" }`.
   Roles: `Admin | Author | Moderator | Learner`.
4. Ese usuario hace **login** y ya opera contra **su** base:
   - `GET/POST /categories`, `GET/POST /trainings` → van a la base del tenant resuelto por su token.

## 5. Actualizar el esquema en TODOS los clientes
Cuando cambies el modelo de cliente:
```bash
dotnet ef migrations add <NombreCambio> --context TenantDbContext -o Migrations/Tenant
dotnet run -- migrate
```
`migrate` aplica las migraciones pendientes al catálogo y a **cada** base de cliente activa.

## Cómo funciona el aislamiento
- El JWT lleva `tenant_id`. El `TenantResolutionMiddleware` lo lee, busca en el catálogo la
  cadena de conexión de ese tenant y la guarda en `ITenantContext` (scoped, por request).
- El `TenantDbContext` se construye con esa conexión. **Nunca** se comparte entre requests.
- El admin de plataforma no tiene `tenant_id`; por eso los endpoints de contenido responden
  400 si no hay tenant en contexto.

## Clonar una instancia (otro despliegue)
Toda la config específica está fuera del binario (`appsettings.json` / variables de entorno),
así que el mismo build corre en cualquier lado. Con Docker:
```bash
docker build -t trainingplatform .
docker run -p 8080:8080 \
  -e ConnectionStrings__Catalog="Server=...;Database=...;" \
  -e Jwt__Key="..." \
  trainingplatform
```

## Notas
- Las cadenas de conexión de tenant se guardan en el catálogo; en producción conviene cifrarlas.
- `Database.Migrate()` crea la base del cliente si no existe.
- Construido sobre tecnología propia (Background Technology); el motor genérico se mantiene
  separable del contenido específico de cada cliente.
