# Auditoría de QA — 2026-11-06

Auditoría de las tarjetas en **In QA** del tablero FacturAR (11 historias, 36 checks sin marcar).
Objetivo: verificar cada check con evidencia real y marcar solo lo comprobado.

## Entorno

| Elemento | Valor |
| --- | --- |
| Producción (portal) | `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net` |
| Producción (API) | `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net` |
| Commit desplegado | `c4c59443f39d28029120b1cdff43fc55926fb2e1` (igual a `origin/main`) |
| MongoDB producción | `facturar` — 1 administrador y 1 evento; sin contribuyentes, usuarios, API keys ni comprobantes |
| Stack local | API + portal `Debug` contra MongoDB 8.2 con replica set (`rs0`) y seed habilitado |
| Suite completa | `dotnet test FacturAR.slnx -c Release` con `MONGO_CONNECTION_STRING` real: **685 unitarios + 370 integración, 0 omitidos, 0 fallos** |
| Herramientas usadas | Playwright (Chromium 156), axe-core 4, Lighthouse 12, Postman/HTTP sobre SOAP, Azure CLI, OpenCV (QR), MongoDB Driver |

La base de producción no tiene usuarios demo, así que los flujos autenticados se verificaron en el stack local con el seed y MongoDB real, y los checks de despliegue, SMTP, Groq y páginas públicas se verificaron contra producción.

## Resultado por historia

### US-01 — Solicitud pública de alta

| Check | Estado | Evidencia |
| --- | --- | --- |
| Mensaje de éxito con próximo paso y error claro | **Verificado** | CUIT inválido → «El CUIT no es válido.» (`docs/capturas/qa-2026-11-06/us01-error.png`); envío válido → «Solicitud recibida · Recibimos tu solicitud. Revisamos los datos y te escribimos al email de contacto.» (`docs/capturas/qa-2026-11-06/us01-exito.png`). Mismo comportamiento en producción |
| Persistida y visible para el administrador global | **Verificado por API** | `GET /v1/admin/solicitudes` devuelve las dos solicitudes de QA con estado `Pendiente` |
| Email de confirmación en desarrollo | **Verificado** | `FileEmailSender`: archivo `20261106-221817-446_SolicitudRecibida_*.html` |
| Tests de integración del endpoint e índices | **Verificado** | Suite con MongoDB real, 0 omitidos |
| Resto de criterios | Ya marcados | — |

### US-05 — Emisión de facturas A, B y C

| Check | Estado | Evidencia |
| --- | --- | --- |
| `400` con `ErrorResponse` y **detalles por campo** | **No se puede marcar** | La API devuelve `400` con `{"codigo":"VALIDACION","mensaje":"..."}`, sin `detalles`. `docs/contratos-api.md` §2 define `ErrorResponse` **solo** con `Codigo` y `Mensaje`; el contrato es normativo y contradice el texto del check |
| Transacción, índices, consulta y verificación por CAE | Verificado | 370 tests de integración con MongoDB real; emisión `201`, detalle `200`, verificación por CAE `200` |

### US-07 — Numeración atómica

| Check | Estado | Evidencia |
| --- | --- | --- |
| El índice único impide duplicados | **Verificado** | `ux_comprobantes_numeracion` `{ContribuyenteId, PuntoVenta, TipoComprobante, Numero}` único + `ux_secuencias_contribuyente_punto_tipo` |
| Tests de concurrencia y rollback | **Verificado** | `MongoSecuenciasTests`: 12/12 aprobados, repetido 3 veces (41 s, 30 s, 21 s) contra replica set |

### US-10 — PDF con código QR

| Check | Estado | Evidencia |
| --- | --- | --- |
| El QR se lee con un lector estándar | **Verificado** | PDF emitido por la API; QR decodificado con OpenCV `QRCodeDetector`: `http://localhost:5116/verificar?cae=13598747697833`, coincide con el CAE emitido |

### US-11 — Verificación pública por CAE

| Check | Estado | Evidencia |
| --- | --- | --- |
| `/verificar` muestra el resultado con un comprobante existente | **Verificado** | Portal local: «COMPROBANTE VÁLIDO», CAE, emisor, CUIT, Factura C, punto de venta/número, fecha, importe, vencimiento y estado |
| Probar el flujo completo desde `/verificar` | **Verificado** | Igual al anterior, con CAE real emitido |
| Verificación desde el portal y por API | **Verificado** | `GET /v1/comprobantes/verificar/13598747697833` → `200`; portal → resultado completo |

### US-15 — SOAP de emisión

| Check | Estado | Evidencia |
| --- | --- | --- |
| La numeración y el CAE son los mismos que en REST | **Verificado** | `FECAESolicitar` devolvió `CodAutorizacion=24118210949728`; el listado REST muestra número 4 con el mismo CAE |
| `FECAESolicitar` emite un comprobante válido | **Verificado** | `200` con `Resultado=A`, CAE de 14 dígitos y `FchVto` |
| SOAP y REST producen el mismo resultado | **Verificado** | `FECompConsultar` (número 4) devuelve el mismo CAE, tipo, punto de venta e importes que REST |
| WSDL consumible desde SoapUI o Postman | **Verificado con reserva** | `GET /soap?wsdl` → `200`, WSDL válido con `FECAESolicitar`, `FECompConsultar`, `FECompUltimoAutorizado` y `FEDummy`; las cuatro operaciones respondieron por SOAP 1.1. No se hizo la importación manual en la UI de SoapUI/Postman |

### US-02 — Gestión de API keys

| Check | Estado | Evidencia |
| --- | --- | --- |
| Solo `Administrador` gestiona keys | Verificado | `Emisor` y `Consulta` → `403 SIN_PERMISOS` |
| Revocar deja de autenticar | Verificado | Key nueva autentica `200`; tras `DELETE` → `401` |
| Más de una key activa | Verificado | Dos altas `201` |
| Valor completo con advertencia de copia única | Verificado | Modal «Copiá tu API key ahora… único momento» + botón Copiar |
| Desktop **y mobile** | **No se puede marcar** | `/api-keys` desborda horizontalmente a 375 px (`scrollWidth 534` vs `clientWidth 375`): la tabla no está envuelta en `.table-responsive` |

### US-14 — API keys desde el portal

| Check | Estado | Evidencia |
| --- | --- | --- |
| Prueba manual del flujo completo | **Verificado** | Alta con modal de valor único, revocación con confirmación (estado pasa a `Revocada`) y regeneración (prefijo `fak_350d09064576` → `fak_39a1fee3d839` con nuevo modal) |
| Agregar prueba manual | **Verificado** | Registrada en este documento |
| Responsive y accesible por teclado | **Parcial** | Teclado: orden lógico y foco visible de 2 px en 14 tabulaciones. Responsive: falla por desborde de tablas a 375 px |
| Aislamiento por empresa y estados de carga/vacío/error | No se pudieron verificar con dos empresas reales | Cubierto por tests con MongoDB; no se levantó una segunda empresa |

### US-17 — Auditoría y outbox

| Check | Estado | Evidencia |
| --- | --- | --- |
| Consultar el estado de un mensaje para soporte | **Verificado** | `IOutboxRepository.ConsultarAsync` y `ListarPorEstadoAsync` con tests `MongoOutboxTests` ejecutados contra MongoDB real |
| Índices para consulta por estado y fecha | **Verificado** | `ix_outbox_estado_fecha` `{Estado:1, CreatedAt:1}` |
| Integrar emails de onboarding si US-29 lo permite | **No se puede marcar** | No hay implementaciones de `IOutboxHandler`: la outbox guarda y reintenta, pero ningún mensaje se procesa; US-29 envía emails en forma directa |

### US-29 — Notificaciones transaccionales

| Check | Estado | Evidencia |
| --- | --- | --- |
| Flujo con SMTP configurado en staging | **Verificado en producción** | Logs de producción: `Email SMTP enviado. plantilla=SolicitudRecibida exito=True duracionMs=4276` y `plantilla=NuevaSolicitud exito=True` |
| El panel muestra advertencia si el email falla y permite reenviar | **No se puede marcar** | API: aprobación con SMTP caído → `emailEnviado=false` + advertencia y estado `Aprobada`; reenvío → `502 EMAIL_NO_ENVIADO`. La UI no se puede verificar porque `/admin/solicitudes` responde **500** |
| Integración en US-01/US-27/US-28 | Verificado | Solicitud local genera email; aprobación dispara invitación |
| Flujo sin SMTP en desarrollo | Verificado | `FileEmailSender` genera archivos |
| Un fallo de email no revierte la aprobación | Verificado | Aprobación `200` con `emailEnviado=false` y estado `Aprobada` |

### US-32 — Landing, sistema de diseño y asistente

| Check | Estado | Evidencia |
| --- | --- | --- |
| Tema claro/oscuro/system en landing, login, alta, activación, verificación y shell | **Verificado** | `data-theme` y tokens correctos en `/`, `/login`, `/solicitar-alta`, `/verificar`, `/activar` y `/dashboard`; preferencia persistida y `aria-pressed` correcto |
| Azure App Settings de producción | **Verificado** | `Portal__BaseUrl`, `Asistente__*`, `Api__BaseUrl` y `ASPNETCORE_FORWARDEDHEADERS_ENABLED` presentes en ambas Web Apps |
| Ejecutar Lighthouse y axe en landing, login, alta y chat | **Verificado** | Reportes en `docs/qa-lighthouse-*.json` y `docs/qa-axe-2026-11-06.json` |
| Respuesta real de Groq verificada con la key de producción | **Verificado** | El asistente respondió con `openai/gpt-oss-120b` usando la key de producción (portal de producción y stack local) |
| WCAG 2.2 AA | **No se puede marcar** | axe reporta fallas serias: `scrollable-region-focusable` en `#codigo-curl` y `#asistente-mensajes`, `label-content-name-mismatch` en `#asistente-toggle`, `document-title` y `page-has-heading-one` en `/activar` |
| Lighthouse móvil ≥ 90 en las cuatro categorías | **No se puede marcar** | Landing: Performance **77**, Accessibility 100, Best Practices 100, SEO 100. TBT 930 ms; el resto de las páginas sí supera 90 (login 99/100/100/63 por `noindex`; alta 92/100/100/91) |
| El asistente responde que no hay registro libre y que la cuenta se crea en la activación | **No se puede marcar** | Respuestas reales: «El registro es gratuito; podés crear la solicitud sin costo» y «Al activar, recibís tu API Key» — no dice que no hay registro libre ni que la cuenta se crea en la activación; además afirma que la API key se recibe al aprobar |
| Rediseñar `/activar` con los tokens Perspective | **No se puede marcar** | Usa los tokens de tema, pero no tiene `<title>` ni `<h1>` (axe) |
| Agregar `og-image.png` y `apple-touch-icon.png` | **No se puede marcar** | No existen en `wwwroot` |
| 18 pruebas de red team | **No se puede marcar** | No se ejecutó la matriz completa; cinco casos requieren configuración adicional (Groq caído, asistente deshabilitado, historial con rol `system`) |
| Red team completo sin fugas ni secretos | **No se puede marcar** | Depende del punto anterior |
| Todos los criterios verificados con evidencia | **No se puede marcar** | Quedan criterios abiertos |
| Reportes de Lighthouse y axe adjuntos en el PR | **Verificado** | Se adjuntan en el PR de esta auditoría |
| PR contra development revisado y aprobado | **No se puede marcar** | Requiere revisión humana |
| Un revisor externo entiende el producto solo con `/` | **No se puede marcar** | Requiere una persona externa |

## Hallazgos que bloquean checks

1. **`/admin/solicitudes` y `/admin/solicitudes/{id}` responden `500`** en local y en producción:
   `InvalidOperationException: No authenticationScheme was specified, and there was no DefaultChallengeScheme found`.
   Las páginas Blazor `Solicitudes.razor` y `SolicitudDetalle.razor` tienen `[Authorize(Policy = "AdminGlobal")]` y el portal no registra ningún esquema de autenticación, así que el `AuthorizationMiddleware` falla al hacer `Challenge`. Bloquea la consola de US-27 y la verificación visual de la advertencia de US-29.
2. **Desborde horizontal en el shell privado**: `/api-keys` a 375 px (534 px) y `/dashboard`, `/comprobantes`, `/api-keys` y `/comprobantes/nuevo` a 768 px. Las tablas no están envueltas en un contenedor scrollable.
3. **axe con fallas serias** en `/` (`#codigo-curl`), `/solicitar-alta` (`#asistente-toggle`, `#asistente-mensajes`) y `/activar` (sin `title` ni `h1`).
4. **Lighthouse móvil de la landing: Performance 77** (TBT 930 ms con CPU 4×).
5. **El asistente da información incorrecta** sobre el proceso de alta (registro libre y momento en que se obtiene la API key).
6. **Faltan `og-image.png` y `apple-touch-icon.png`**.
7. **La outbox no tiene handlers**: guarda, reclama y reintenta, pero no procesa ningún mensaje.
8. **`ErrorResponse` sin `detalles` por campo**: coherente con el contrato, en contradicción con el texto del check de US-05.

## Artefactos

- `docs/capturas/qa-2026-11-06/` — capturas de la solicitud pública.
- `docs/qa-lighthouse-landing-2026-11-06.json`
- `docs/qa-lighthouse-login-2026-11-06.json`
- `docs/qa-lighthouse-alta-2026-11-06.json`
- `docs/qa-axe-2026-11-06.json`

## Fuera de esta auditoría

- No se modificó código de producción.
- No se cambió ningún dato de producción salvo dos solicitudes de alta de QA (`QA Auditoria SRL`, `QA SMTP FacturAR`, `QA SMTP Log FacturAR`) enviadas por el endpoint público.
- No se mergeó ningún PR.
