# US-01 — Solicitud pública de alta de empresa

**Épica:** Registro y multiempresa  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-23  
**Depende de:** US-29 (email de confirmación)  
**Habilita:** US-27, US-28

## Historia

Como visitante que representa a una empresa, quiero enviar una solicitud de alta con mis datos fiscales y de contacto para que FacturAR revise mi empresa antes de habilitarme el acceso.

## Contexto

El alta no crea la empresa ni un usuario. Solo registra una solicitud pendiente. La aprobación y la creación del contribuyente, punto de venta e invitación se hacen en US-27 y US-28.

El CUIT es un dato público: no se usa como prueba de titularidad. La solicitud no debe revelar si un CUIT ya está registrado.

## Criterios de aceptación

- [ ] El formulario público solicita razón social, CUIT, condición frente al IVA, nombre del solicitante, email, teléfono, domicilio, ciudad, provincia, código postal y aceptación de términos.
- [ ] El CUIT se normaliza sin guiones y se valida con el dígito verificador.
- [ ] El email se normaliza a minúsculas y se valida con formato.
- [ ] Los campos obligatorios se validan en el borde y en el caso de uso.
- [ ] La solicitud se guarda con estado `Pendiente` y fecha UTC.
- [ ] No se crea `Contribuyente`, `Usuario`, `PuntoVenta` ni `ApiKey` en este paso.
- [ ] No se permite otra solicitud pendiente con el mismo CUIT.
- [ ] No se permite otra solicitud pendiente con el mismo email.
- [ ] Si el CUIT ya pertenece a un contribuyente, se devuelve un mensaje genérico y se registra el intento para revisión.
- [ ] La respuesta no expone datos de otras solicitudes ni confirma la existencia de un contribuyente.
- [ ] Se envía o encola un email de confirmación al solicitante.
- [ ] El endpoint público tiene rate limit por IP y por email.
- [ ] La página muestra un mensaje de éxito con el próximo paso y un mensaje claro ante errores.
- [ ] La solicitud puede auditarse sin guardar datos sensibles innecesarios.

## Escenarios Gherkin

```gherkin
Scenario: Solicitud válida
  Given un visitante completa todos los datos obligatorios con un CUIT válido y un email válido
  And acepta los términos
  When envía la solicitud de alta
  Then la solicitud se guarda con estado "Pendiente"
  And no se crea ningún contribuyente ni usuario
  And se envía un email de confirmación al solicitante
  And el portal muestra el mensaje "Solicitud recibida"
```

```gherkin
Scenario: CUIT inválido
  Given un visitante ingresa un CUIT con dígito verificador incorrecto
  When envía la solicitud
  Then la API devuelve 400 con código "VALIDACION"
  And no se guarda la solicitud
  And el formulario muestra el error junto al campo CUIT
```

```gherkin
Scenario: Solicitud pendiente duplicada
  Given existe una solicitud pendiente con el CUIT 20123456786
  When un visitante envía otra solicitud con el mismo CUIT
  Then la API devuelve 409 con código "SOLICITUD_PENDIENTE"
  And no se crea una segunda solicitud
```

```gherkin
Scenario: CUIT ya registrado
  Given existe un contribuyente con el CUIT 20123456786
  When un visitante envía una solicitud con ese CUIT
  Then la API devuelve 202 con un mensaje genérico de revisión
  And no se confirma la existencia del contribuyente
  And se registra el intento para el administrador global
```

```gherkin
Scenario: Rate limit
  Given una IP superó el límite de solicitudes permitidas
  When envía otra solicitud
  Then la API devuelve 429 con código "RATE_LIMIT"
  And no se guarda la solicitud
```

## Tareas técnicas

- [ ] Crear `SolicitudAltaEmpresa` y `EstadoSolicitud` en Domain.
- [ ] Crear `ISolicitudAltaRepository` en Application.
- [ ] Implementar el repositorio MongoDB y los índices parciales de unicidad.
- [ ] Crear DTOs `SolicitudAltaRequest` y `SolicitudAltaResponse` en Contracts.
- [ ] Implementar `CrearSolicitudAltaService` con normalización y validaciones.
- [ ] Crear `POST /v1/solicitudes-alta` con rate limit y `ErrorResponse`.
- [ ] Crear la página de solicitud en el portal y el cliente HTTP.
- [ ] Enviar el email de confirmación mediante US-29.
- [ ] Agregar tests unitarios de validación y duplicados.
- [ ] Agregar tests de integración del endpoint y de los índices.
- [ ] Documentar el endpoint en OpenAPI y Postman.

## Definition of Done

- [ ] Una solicitud válida queda persistida y visible para el administrador global.
- [ ] Los duplicados y errores no crean registros parciales.
- [ ] El email de confirmación se genera en desarrollo.
- [ ] El formulario funciona en desktop y mobile.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el proceso de alta.

## Notas de alcance

- No implementa aprobación, creación de empresa ni invitación; eso corresponde a US-27 y US-28.
- No implementa recuperación de contraseña ni login.
- No consulta AFIP/ARCA.
- No se debe guardar el token de invitación en esta historia.
