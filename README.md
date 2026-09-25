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

## Cambios de septiembre 2026

### Migraciones nuevas
Después de traer esta rama hay que aplicar el esquema en el catálogo y en **cada** base de cliente:
```bash
dotnet run -- migrate
```
- Catálogo: `AddCertificateLinksAndComplianceConfig` (tabla `CertificateLink` y columna
  `Tenant.ComplianceConfigJson`) y `AddComplianceOfficerFlag` (`UserCompany.IsComplianceOfficer`).
- Cliente: `AddGroupPlansAndOnboarding` (`Training.Audience`/`OnboardingDays`, `UserGroup.OnboardingDays`,
  `UserGroupMember.JoinedAt` y tabla `GroupCourse`).

### Modo presentación (`Training.PlayerConfigJson`)
El bloque `presentation` enciende el reproductor 16:9 (escenario 1280×720 escalado, láminas
`cover`/`dark`/`split`/`photo-left`). Se lee y escribe con `GET/PUT /trainings/{id}/player-config`
y el player lo recibe en `GET /versions/{id}/config`. Sin `enabled` el reproductor es el clásico.
```json
{
  "allowBack": true, "reviewAfterPass": true, "immediateFeedback": false,
  "presentation": {
    "enabled": true,
    "theme": { "bg": "#0d0d0d", "accent": "#f97316", "panel": true, "panelTitle": "HOSTIGAMIENTO SEXUAL EN EL EMPLEO" },
    "transition": "cover"
  }
}
```
En el `PUT`, `presentation` es opcional: si no viene se conserva lo guardado; si viene se normaliza
(colores `#rgb`/`#rrggbb`, transición `cover|fade|none`, `panelTitle` hasta 120 caracteres).
El curso de Hostigamiento ya lo trae en `content/hostigamiento-sexual/course.mjs`
(`seed.mjs` y `tools/Seed-Curso.ps1` lo envían al sembrar).

**Pantalla de entrada (layout `intro`).** Un ítem Info con `layout: 'intro'` como **primer** ítem
(helper `intro({ title, description, minutes, photo })` de `content/_shared/authoring.mjs`; `seed.mjs`
rechaza un `intro` en otra posición) no es una lámina: en modo presentación el player lo saca de las
páginas y del contador «n / N» (la portada sigue siendo la 1) y lo usa para la pantalla previa al
botón «Comenzar»: foto de portada atenuada, título, descripción y los datos que calcula él mismo
(láminas, preguntas, puntos, aprobación si `/config` trae `passPercent`, tiempo estimado). Si se
retoma un intento, el botón dice «Continuar» e indica la lámina donde iba. En modo clásico el
`intro` se ve como primera página con una caja de resumen.

### Reglas de cumplimiento por compañía (`Tenant.ComplianceConfigJson`)
JSON tolerante (`ComplianceConfig` en `Catalog.cs`), editable con `GET/PUT /company/compliance`
(el Admin de la compañía escribe; los oficiales sólo leen). Valores por defecto:
```json
{
  "extraEmails": [], "dueSoonDays": 30,
  "digestFrequency": "weekly", "digestDayOfWeek": "Monday", "digestDayOfMonth": 1, "digestHour": 8,
  "expiredRepeatDays": 14, "includeNotStarted": true,
  "certificateDelivery": "link", "linkDays": 30
}
```
- `certificateDelivery`: `link` manda un enlace con vencimiento (`linkDays`); `attachment` adjunta el PDF como antes.
- `extraEmails`: correos que reciben copia de los certificados y el resumen de cumplimiento,
  además de los oficiales marcados.
- El resumen lo manda `ComplianceDigestService` (revisa cada 30 min con la cadencia de cada compañía);
  para probarlo: `POST /admin/run-compliance-digest` (admin) o `POST /company/compliance/test` (sólo a quien llama).

### Enlace público del certificado: `GET /c/{token}`
Único endpoint anónimo. El correo lleva `https://<host>/c/<token>`; en base sólo se guarda el hash
SHA-256 (`CertificateLink`). Sirve el PDF inline mientras el enlace esté vigente y no revocado, con
`Cache-Control: no-store`, `Referrer-Policy: no-referrer` y límite de 60 peticiones por IP cada 10 min.
Si venció o no existe responde 410 con una página sin datos personales y un botón «Entrar a Aprendor»
(`App:BaseUrl` o el origen de la petición). Gestión autenticada por folio:
`POST /certificates/{serial}/resend` (enlace nuevo), `POST /certificates/{serial}/revoke` y
`GET /certificates/{serial}/links` (accesos). Cada envío, reenvío y revocación queda en `AuditLogs` del cliente.

### Marcar un oficial de cumplimiento
Es una marca en la membresía (`UserCompany.IsComplianceOfficer`), no un rol: se suma al rol que ya tenga.
- Desde la UI: pantalla de usuarios del admin de plataforma (casilla «Oficial de cumplimiento») o, para
  el Admin de la compañía, la lista de oficiales en la sección **Cumplimiento**.
- Por API (rol Admin): `POST /admin/user-companies/{id}/compliance-officer` con
  `{ "isOfficer": true, "tenantId": "<guid>" }`; `{id}` es el Id de la membresía o el Id del usuario
  (si la compañía es la principal del usuario, se crea la membresía espejo). `GET /admin/users` y `GET /me`
  exponen `isComplianceOfficer`.
- El oficial recibe copia de los certificados y el resumen, ve **Cumplimiento** (`/compliance/summary`,
  `/compliance/alerts`, «Recordar ahora») y puede leer el expediente de cualquier empleado. No obtiene
  permisos de edición.
