# Manual de operación interna

> **Documento interno.** No se publica en `/manual`. Describe la consola global, los endpoints de administración, el seed de demostración y la operación de la infraestructura. El manual público para integradores es [`docs/manual-integracion.md`](manual-integracion.md).

## Consola global y roles

La consola global vive en `/admin` y `/admin/solicitudes`. Se autentica con el esquema de cookies separado de la sesión de empresa.

| Rol global | Puede |
| --- | --- |
| `SuperAdmin` | Consultar, editar, aprobar y rechazar solicitudes; reenviar invitaciones. |
| `Soporte` | Consultar solicitudes y reenviar invitaciones. No puede editar, aprobar ni rechazar. |

## Endpoints de administración

| Método | Ruta | Acceso |
| --- | --- | --- |
| `POST` | `/v1/admin/auth/login` | Público con rate limit. |
| `POST` | `/v1/admin/auth/logout` | Cookie de consola global. |
| `GET` | `/v1/admin/auth/me` | Cookie de consola global. |
| `GET` | `/v1/admin/solicitudes` | Cookie de consola global. |
| `GET` | `/v1/admin/solicitudes/{id}` | Cookie de consola global. |
| `PUT` | `/v1/admin/solicitudes/{id}` | `SuperAdmin`. |
| `POST` | `/v1/admin/solicitudes/{id}/aprobar` | `SuperAdmin`. |
| `POST` | `/v1/admin/solicitudes/{id}/rechazar` | `SuperAdmin`. |
| `POST` | `/v1/admin/solicitudes/{id}/reenviar-invitacion` | Administrador global. |

La aprobación crea el contribuyente, el punto de venta y la invitación en una transacción de MongoDB y dispara los emails de onboarding después del commit. Un fallo de email no revierte la aprobación.

Errores propios del backoffice: `404 SOLICITUD_NO_ENCONTRADA`, `409 SOLICITUD_YA_PROCESADA`, `409 CUIT_YA_REGISTRADO`, `403 SIN_PERMISOS` y `502 EMAIL_NO_ENVIADO`.

## Seed y datos de demostración

- `Seed__Enabled` habilita el seed. En Production requiere una bandera explícita.
- `Seed__Password` es obligatoria, se valida y se hashea; nunca se guarda en texto plano.
- Crea la empresa demo, los usuarios `Administrador`, `Emisor` y `Consulta`, el `SuperAdmin`, el punto de venta `1 Casa Central`, las secuencias y los parámetros globales con upserts idempotentes.
- La API key demo solo se crea en Development y su valor se entrega una vez en `seed/api-key-demo.txt`.
- El seed no se ejecuta en Production sin la bandera explícita.

## Salud, versión y despliegue

- `GET /` devuelve nombre del servicio, versión y commit.
- `GET /health` es readiness: verifica MongoDB; responde `200` o `503`.
- `GET /health/live` es liveness: no consulta MongoDB; siempre `200`.
- El health check de Azure App Service apunta a `/health/live`.
- `GET /openapi/v1.json` se publica solo en Development.
- Docker local: API `http://localhost:8080` y portal `http://localhost:8081`.
- El CD publica en Docker Hub y despliega en Azure App Service; no se usa Kubernetes.

## Configuración operativa

| Clave | Uso |
| --- | --- |
| `ApiKeys__HeaderName` | Nombre del header de API key. Default `X-Api-Key`. |
| `ApiKeys__IntervaloActualizacionUltimoUsoMinutos` | Frecuencia de actualización de `UltimoUso`. Default 1 minuto. |
| `RateLimit:Invitacion` | Límite por IP y hora para invitaciones. |
| `Manual__RutaArchivo` | Ruta del Markdown público dentro del portal. |
| `Manual__RutaColeccionPostman` | Ruta de la colección Postman descargable. |

## Cookies y sesión

- Empresa: cookie de sesión `HttpOnly`, `Secure` en producción y `SameSite=Lax`, con expiración absoluta de 8 horas.
- Consola global: esquema de cookie separado; no comparte sesión con la empresa.
- La invitación vence a las 48 horas y es de un solo uso.
- La API key no autoriza endpoints de cookie y la cookie de empresa no autoriza `/soap` ni `/v1/admin/**`.

## Códigos internos

- `MONGO_NO_DISPONIBLE` (`503`): no hay conexión con MongoDB o la operación agota el tiempo.
- `EMAIL_NO_ENVIADO` (`502`): falló el envío del email; la operación de negocio no se revierte.
- `ERROR_INTERNO` (`500`): falla inesperada; el detalle queda en el log y nunca en la respuesta.

## Mantenimiento del manual público

El contenido público vive en [`docs/manual-integracion.md`](manual-integracion.md) y se renderiza en `/manual`. Al agregar un endpoint de administración, de seed o de infraestructura, documentarlo en este archivo y no en el manual público.
