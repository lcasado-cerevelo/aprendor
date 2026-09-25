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
- `Jwt:Key` — secreto largo y aleatorio (mínimo 32 bytes). Vacío en `appsettings.json`; el de
  desarrollo está en `appsettings.Development.json`. Fuera de Development va por variable de
  entorno (ver **Seguridad para publicar en internet**).
- `Bootstrap:AdminEmail` / `AdminPassword` — admin de plataforma que se siembra al primer arranque
  (solo si el catálogo está vacío). En desarrollo, `admin@local` / `ChangeMe123!`.

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
  -e APRENDOR_Jwt__Key="..." \
  -e APRENDOR_App__BaseUrl="https://aprendor.midominio.com" \
  trainingplatform
```
La imagen corre sin root (`USER $APP_UID`) y solo escribe en `/app/App_Data` (logs y archivos
subidos); `.dockerignore` deja fuera `bin`, `obj`, `App_Data`, `.claude`, `*.local.json` y
`appsettings.Development.json`.

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
(láminas de contenido, preguntas, puntos, aprobación si `/config` trae `passPercent` —hoy no lo
envía—, tiempo estimado). Si falta `photo`, usa la de la primera lámina `cover`. Sólo en cursos
con `intro`, al retomar un intento el botón dice «Continuar» e indica la lámina donde iba (sin
`intro` la entrada y el arranque quedan como antes). En modo clásico el `intro` se ve como primera
página con una caja de resumen; no se lista en el resumen final.

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
(`App:BaseUrl`; el origen de la petición solo en Development). Gestión autenticada por folio:
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

## Seguridad para publicar en internet (septiembre 2026, bloque S1)

### Variables obligatorias fuera de Development
Con `ASPNETCORE_ENVIRONMENT` distinto de `Development` la app **no arranca** (lanza
`InvalidOperationException` con el motivo) si falta alguna de estas. Van como variables de entorno,
con el prefijo `APRENDOR_` o sin él (`Jwt__Key`):

| Variable | Qué exige |
| --- | --- |
| `APRENDOR_Jwt__Key` | 32 bytes UTF-8 o más, sin `CHANGE-ME` (la de ejemplo se rechaza). |
| `APRENDOR_App__BaseUrl` | URL pública `https://...`. Los enlaces de los correos (restablecer contraseña, certificados, recordatorios) se arman solo con ella. |

Para el primer arranque con el catálogo vacío hacen falta además `APRENDOR_Bootstrap__AdminEmail` y
`APRENDOR_Bootstrap__AdminPassword`: sin ellas (o con `ChangeMe123!`) no se siembra el admin y queda un
error en la bitácora. El admin sembrado nace con cambio de contraseña obligatorio. El modo
`migrate` (`dotnet TrainingPlatform.dll migrate`) no exige nada de lo anterior.

### Configuración opcional
- `APRENDOR_AllowedHosts`: en el archivo queda `*`; en el servidor conviene fijarlo al dominio
  público más localhost (`aprendor.midominio.com;localhost`) para que otra cabecera Host reciba 400.
- `APRENDOR_Email__ApiKey`: clave de Brevo (sin ella no salen correos).
- `Security:TrustedProxies`: IPs de proxies, además de loopback, cuyo `X-Forwarded-For` se cree.
  Solo hace falta si el proxy o `cloudflared` corre en otra máquina.
- `Security:ForwardedForHeader`: `CF-Connecting-IP` detrás de Cloudflare; vacío usa `X-Forwarded-For`.
- `Security:MaxRequestBytes` (1 MB) y `Security:AuthoringMaxRequestBytes` (8 MB, contenido del autor
  con imágenes embebidas). `Media:MaxBytes` (50 MB) es el máximo de `POST /media`, que al pasarse
  responde `413 { error }`. En IIS, `web.config` corta antes cualquier cuerpo de más de 60 MB.
- `Tenants:ConnectionTemplate`: cadena con `{db}`. Si está, `POST /admin/tenants` arma la conexión en
  el servidor con `databaseName` (`^[A-Za-z0-9_]{3,50}$`, por defecto `TP_<nombre>`) e ignora la que
  mande el cliente.

### Qué cambia
- Cabeceras de seguridad y CSP en todas las respuestas, HSTS fuera de Development,
  `Cache-Control: no-store` en la API y sin cabecera `Server` (`web.config`).
- Todo endpoint exige sesión salvo los de acceso (`/auth/*`) y `GET /c/{token}`.
- Admin de plataforma (rol Admin sin compañía, comprobado en el catálogo) frente a Admin de compañía:
  el segundo solo ve y gestiona a la gente de su compañía y nunca a un admin de plataforma. Lo que
  afecta a la cuenta entera (restablecer la contraseña, borrar la cuenta) solo lo hace si la persona
  no pertenece a otra compañía; si pertenece, responde 409 y lo hace el admin de plataforma.
  Compañías, políticas de doble factor, envíos manuales y membresías entre compañías son solo del
  admin de plataforma.
- El enlace de restablecimiento es `BaseUrl/index.html#reset=TOKEN` (en el fragmento no llega a los
  logs del servidor ni del proxy); `index.html` sigue aceptando `?reset=`.
- `POST /media`: lista blanca por extensión y firma de bytes (png, jpg, gif, webp, mp3, mp4, webm,
  pdf, docx, pptx, xlsx), sin HTML, SVG, XML ni JS. `GET /media/{id}` sirve con `nosniff`, CSP propia
  y descarga para lo que no sea imagen, audio, video o PDF. Los documentos del expediente solo los ven
  su dueño, los autores y el oficial de cumplimiento.
- Migración de cliente `SecureMedia` (`MediaAsset.Purpose` y `OwnerUserId`): aplicarla con
  `dotnet TrainingPlatform.dll migrate` en cada despliegue.

### Bitácora
`App_Data/logs/app-log-AAAAMMDD.txt`, un archivo por día; se conservan los últimos 14. Los enlaces de
restablecimiento y los códigos de doble factor solo se escriben en la bitácora en Development.

## Autenticación y sesión (septiembre 2026, bloque S2)

### Al desplegar
- Migración de catálogo `SecurityHardening` (se aplica sola al arrancar o con `migrate`): tabla
  `SecurityEvent`, `AuditLog.Ip`, `PasswordResetToken.Purpose` y en `User` el sello de seguridad
  (`SecurityStamp`, cada fila existente recibe uno nuevo), contadores de fallos, bloqueos,
  `LastTotpStep`, `PendingTotpSecret` y `TempPasswordExpiresAt`.
- Los tokens emitidos antes no llevan sello: todas las sesiones abiertas se cierran y cada persona
  vuelve a entrar una vez.

### Qué cambia
- Límites por IP real (429 con `Retry-After` y `{ error: "Demasiados intentos. Espera N minutos.", minutes }`):
  `/auth/login` 10/min y 50/15 min; `/auth/2fa/verify` 10/min; `/auth/forgot-password` 5/h;
  `/auth/reset-password` 10/h; `/auth/2fa/resend` y `/me/email/send-code` 10/h.
- Por cuenta: 5 claves malas seguidas bloquean la entrada 15 minutos (con aviso por correo, como
  mucho uno por hora); 5 códigos de verificación malos, sumando todos los retos, bloquean el doble
  factor 15 minutos; «¿Olvidaste tu contraseña?» 3 por hora y 5 al día; códigos por correo con 60 s
  entre envíos y 10 al día; comprobar la clave actual con la sesión abierta (cambiar clave, quitar o
  cambiar el 2FA), 5 fallos → 15 minutos. El quinto fallo ya responde 429. Los topes aguantan
  peticiones en paralelo: el intento se reserva antes de comprobar la clave o el código (UPDATE
  condicionado en los contadores de `User`; los que cuentan filas de `SecurityEvent` van bajo un
  cerrojo por cuenta con `sp_getapplock`), así que nunca se comprueban más de 5 entre bloqueo y bloqueo.
- El token lleva `sst` (sello), `scope` y `amr`. Cambiar o restablecer la clave, tocar el 2FA, cambiar
  el rol o quitar una membresía rota el sello y cierra las sesiones de esa persona (la propia recibe
  un token nuevo en la respuesta). Cada petición comprueba sello, membresía y rol (caché de 60 s,
  que se invalida también después de guardar el cambio). Un token nuevo solo estrena vida si la
  petición comprobó la contraseña actual o completó el paso pendiente de un token restringido; si
  no (por ejemplo `/me/email/send-code` con el correo ya validado), vence cuando vencía el actual.
- Tokens restringidos de 15 minutos (`scope` = `change-password`, `enroll-2fa` o `verify-email`) que
  solo abren `/me`, `/me/password`, `/me/2fa/*` y `/me/email/*`. La política de doble factor del login
  es la más estricta entre las compañías de la persona; `/me/switch-company` no alarga la sesión y, si
  la compañía destino exige 2FA y la sesión no lo pasó, responde `requires2fa` con un reto.
- Cambiar de autenticador exige la clave actual y un código del autenticador vigente; el nuevo queda
  pendiente hasta confirmarlo.
- Contraseñas: mínimo 10 caracteres, distinta del correo y fuera de una lista de comunes, en todos los
  puntos que fijan una clave.
- Alta de usuarios sin contraseñas por correo: con invitación, enlace `#invite=TOKEN` de 72 h para que
  la persona cree la suya; sin invitación, el servidor genera una temporal de 16 caracteres que se
  muestra una sola vez al admin y vence a las 72 h. Si la invitación no sale (sin `App:BaseUrl`,
  sin `Email:ApiKey` o falla el envío), también se genera la temporal y se muestra al admin. El reset del admin también puede generarla.

## Doble factor por compañía, redes de confianza, recuperación y Turnstile (septiembre 2026, bloque S3)

### Al desplegar
- Migración de catálogo `CompanySecurityConfig` (se aplica sola al arrancar o con `migrate`):
  `Tenant.SecurityConfigJson`, las compañías existentes quedan con `{}` (recuperación por correo y
  avisos a los Admin encendidos, sin redes de confianza).
- `APRENDOR_Turnstile__SecretKey`: la clave secreta de Turnstile (widget con hostname
  `aprendor.advancelogisticspr.com`). La site key pública ya está en `appsettings.json`. **Sin la
  clave secreta Turnstile queda apagado** (no se pide y al arrancar queda una advertencia en la
  bitácora), así que desplegar sin ella no bloquea la entrada.
- Opcional: `APRENDOR_Security__TrustedNetworks` con las redes de confianza de la instancia (CIDR o IP
  sola, separadas por comas). Las que no se entienden se ignoran con una advertencia.

### Configuración
| Clave | Qué hace |
| --- | --- |
| `Turnstile:Enabled` | `true` en el archivo. Solo se activa con las dos claves. |
| `Turnstile:SiteKey` | Pública. En Development, la de prueba de Cloudflare (`1x00000000000000000000AA`). |
| `Turnstile:SecretKey` | Secreta, solo por variable de entorno. En Development, la de prueba (`1x0000000000000000000000000000000AA`). |
| `Security:TrustedNetworks` | Redes de confianza de la instancia: sin doble factor, sin su alta y sin Turnstile. |

Loopback **no** es de confianza salvo que se liste: detrás del túnel cuenta la IP real (`ClientIp`).
No se admiten redes más amplias que /8 (IPv4) o /32 (IPv6).

### Qué cambia
- `GET /auth/config` (anónimo): `{ turnstileSiteKey }`, `null` si Turnstile está apagado o la petición
  viene de una red de confianza de la instancia. `/auth/login`, `/auth/forgot-password`,
  `/auth/reset-password` y `/auth/2fa/recover/start` reciben `turnstileToken` y lo validan con
  Cloudflare antes de buscar el usuario: ausente o rechazado, `400 { error, turnstileFailed: true }`;
  Cloudflare sin respuesta (5 s) o clave secreta mala, `503` con un mensaje claro y un error en la bitácora.
- Seguridad de la compañía: `GET/PUT /company/security` (su Admin) y `GET/PUT /admin/tenants/{id}/security`
  (admin de plataforma) con `{ trustedNetworks, emailRecoveryEnabled, notifyAdmins }`; la respuesta añade
  `twoFactorPolicy` (se sigue cambiando en `/admin/tenants/{id}/two-factor`), `yourIp` y `yourIpTrusted`.
- Red de confianza: la de la instancia o la de **todas** las compañías de la persona que usan doble
  factor. Desde ella el login no pide el código ni el alta aunque la política sea `required`; el token
  lleva `amr=mfa-trusted` y queda la auditoría `2fa-skipped-trusted`. `/me/switch-company` solo acepta
  ese `amr` como doble factor si la petición sigue llegando desde una red de confianza del destino.
- Primera alta del autenticador desde fuera de las redes de confianza: antes hay que canjear un
  código enviado al correo (`POST /me/2fa/setup/send-code`, luego `/me/2fa/setup` con
  `emailChallengeId` y `emailCode`; sin él responde `400 { requiresEmailCode: true }`). `GET /me/2fa`
  dice si hace falta (`emailCodeRequired`). La prueba vale 15 minutos desde la misma IP.
- «Perdí mi autenticador»: `POST /auth/2fa/recover/start { challengeId, turnstileToken }` desde la
  pantalla del código y `POST /auth/2fa/recover/verify { challengeId, code }`. Solo con el correo
  validado, si ninguna de sus compañías apagó la recuperación y nunca para el admin de plataforma;
  la respuesta del inicio es siempre la misma. 3 códigos al día por cuenta y 5 por hora por IP. Al
  canjearlo se quita el autenticador, se cierran sus sesiones, se avisa a la persona y a los Admin
  (`notifyAdmins`), y se devuelve la sesión (con `enroll-2fa` si la política lo exige).
- `POST /auth/reset-password` acepta `totpCode`: si la cuenta tiene app autenticadora y la petición no
  llega desde una red de confianza, lo exige (`400 { requiresTotp: true }` sin gastar el enlace; los
  fallos cuentan para el bloqueo del doble factor). Tras restablecer desde fuera, aviso a los Admin
  (`notifyAdmins`). El admin de plataforma ya no recibe enlaces de restablecimiento (respuesta genérica
  y auditoría `password-reset-denied`).
- `POST /company/users/{id}/reset-2fa`: el Admin de la compañía reinicia el doble factor de su gente
  (409 si la persona pertenece también a otra compañía; nunca a un admin de plataforma ni a sí mismo);
  el admin de plataforma, a cualquiera que no lo sea. Cierra sus sesiones, audita y le avisa por correo.
  `GET /admin/users` trae `twoFactorEnabled`.
- Correos nuevos (`/admin/email-preview?kind=`): `recover`, `enroll-code`, `2fa-reset`, `2fa-recovered`,
  `admin-reset-notice`, `admin-2fa-notice`.
