# Scripts de base de datos

Generados con `dotnet ef migrations script --idempotent`. **Idempotentes**: cada bloque
comprueba si su migración ya se aplicó, así que se pueden correr sobre una base al día
sin que hagan nada, y sobre una atrasada aplicando solo lo que falta.

| Archivo | Contra qué base | Qué contiene |
|---|---|---|
| `catalogo.sql` | Base del **catálogo** (`Tenant`, `User`) | Todo el esquema desde cero |
| `tenant.sql` | Base de **cada cliente** | Todo el esquema desde cero |
| `catalogo-cambios.sql` | Base del **catálogo** | Solo lo añadido en esta ronda |
| `tenant-cambios.sql` | Base de **cada cliente** | Solo lo añadido en esta ronda |
| `borrar-hipaa-advance-logistics.sql` | Base del tenant de **Advance Logistics** | Borrado definitivo del curso HIPAA anterior (7 módulos), retirado y reemplazado por `content/ley-hipaa`. Ver `content/README.md`. |
| `limpiar-pruebas.sql` | Base de **una compañía** (y la del catálogo, por SQLCMD) | Limpieza después de las pruebas: reinicia el progreso de todos (intentos, certificados, asignaciones, planes de grupo, expedientes, bitácora de la compañía) y borra por completo a los usuarios dados de baja y a los huérfanos del «Eliminar» de antes. Simula primero (`@Simular = 1`); en SSMS, con «SQLCMD Mode». |
| `reinicio-total.sql` | Base de **una compañía** (y la del catálogo, por SQLCMD) | Deja la plataforma en cero: conserva solo dos cuentas (por correo) y los adiestramientos; borra las demás cuentas, todo el historial, grupos, asignaciones, expedientes y las bitácoras de la compañía y del catálogo. Se detiene si no encuentra las dos cuentas. Simula primero (`@Simular = 1`). |

Los primeros cuatro son de **esquema** (tablas/columnas). El curso HIPAA no lo es —
el contenido de los cursos no vive en migraciones, ver `content/README.md`.

Si la aplicación arranca normalmente, **no hace falta correr nada**: `Program.cs` aplica las
migraciones del catálogo al iniciar, y `dotnet run -- migrate` las aplica al catálogo y a
todas las bases de clientes activos. Estos scripts son para cuando el despliegue lo hace
otra persona, o hay que revisar el cambio antes de tocar producción.

## Lo que añade esta ronda

**Catálogo** (`catalogo-cambios.sql`):

- Tabla `PasswordResetToken` — recuperación de contraseña desde el login.
- Tabla `TwoFactorChallenge` — retos de doble factor y de validación de correo.
- `User.TwoFactorMode`, `User.TotpSecret`, `User.TwoFactorConfirmedAt` — doble factor.
- `User.EmailVerifiedAt` — validación del correo en el primer ingreso.
- `Tenant.TwoFactorPolicy` — la compañía decide si el doble factor está apagado,
  es opcional o es obligatorio.
- Tabla `UserCompany` — un usuario puede pertenecer a varias compañías, con rol
  distinto en cada una.

**Cliente** (`tenant-cambios.sql`):

- `Training.PlayerConfigJson` — opciones del reproductor (volver atrás, disponible para repaso).
- `Training.ExpiresOn` — vigencia por fecha fija.
- `Training.NotificationConfigJson` — avisos por curso.
- Tabla `NotificationLog` — recordatorios ya enviados, para no repetirlos.
- Tabla `ExternalCertification` — certificaciones tomadas fuera de la plataforma.

## Orden y precauciones

1. Respalda antes. Estos scripts alteran tablas con datos.
2. Corre primero el del **catálogo**, después el de **cada base de cliente**.
3. `EmailVerifiedAt` queda en `NULL` para los usuarios existentes: eso es a propósito,
   así todos validan su correo la próxima vez que entren. Si prefieres dar por válidos
   los correos actuales, corre después:

   ```sql
   UPDATE [User] SET EmailVerifiedAt = SYSUTCDATETIME() WHERE EmailVerifiedAt IS NULL;
   ```

4. La columna `TwoFactorMode` se crea con `''` en las filas existentes en vez de `'none'`.
   No afecta el funcionamiento (solo `'email'` y `'totp'` activan el doble factor), pero
   si quieres dejarlo limpio:

   ```sql
   UPDATE [User] SET TwoFactorMode = 'none' WHERE TwoFactorMode = '';
   ```

5. Lo mismo con `Tenant.TwoFactorPolicy`, que se crea con `''` en las compañías
   existentes. La aplicación lo trata como `optional`, pero conviene dejarlo explícito
   —y es el momento de decidir la política de cada compañía:

   ```sql
   UPDATE Tenant SET TwoFactorPolicy = 'optional' WHERE TwoFactorPolicy = '';
   -- o, para exigirlo en una compañía concreta:
   -- UPDATE Tenant SET TwoFactorPolicy = 'required' WHERE Name = 'Advance Logistics';
   ```

## Regenerarlos

```bash
dotnet ef migrations script --context CatalogDbContext --idempotent -o tools/sql/catalogo.sql
dotnet ef migrations script --context TenantDbContext  --idempotent -o tools/sql/tenant.sql
```
