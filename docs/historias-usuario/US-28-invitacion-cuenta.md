# US-28 — Activación de cuenta por invitación

**Épica:** Registro y multiempresa  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-25  
**Depende de:** US-04, US-27, US-29  
**Habilita:** US-02, US-14

## Historia

Como solicitante aprobado, quiero abrir el link de invitación y definir mi contraseña para activar mi cuenta de administrador de la empresa.

## Contexto

La invitación es la prueba de control del email y el mecanismo de activación. El token se envía por email, se guarda hasheado y es de un solo uso. Al aceptarlo se crea el usuario administrador de la empresa con el `ContribuyenteId` de la invitación.

## Criterios de aceptación

- [ ] El link de invitación tiene formato `/activar?token=...`.
- [ ] `GET /v1/invitaciones/{token}` valida el token y devuelve razón social, email enmascarado y fecha de expiración.
- [ ] Un token inválido, vencido, usado o revocado devuelve un error genérico sin revelar datos.
- [ ] La página de activación muestra la empresa y el email, y no permite editarlos.
- [ ] El formulario solicita contraseña y confirmación.
- [ ] La contraseña cumple mínimo 10 caracteres, al menos una letra y un número, y no es trivial.
- [ ] El email del usuario se toma de la invitación, no del formulario.
- [ ] El `ContribuyenteId` se toma de la invitación, no del formulario ni de la URL.
- [ ] El usuario se crea con rol `Administrador`, activo y con email verificado.
- [ ] Un usuario existente con el mismo email en la empresa devuelve `409` y no duplica.
- [ ] La invitación se marca como `Usada` en la misma operación que crea el usuario.
- [ ] Dos aceptaciones simultáneas del mismo token generan un solo usuario.
- [ ] Aceptar una invitación no inicia sesión automáticamente; redirige al login con mensaje de éxito.
- [ ] La contraseña se hashea y nunca se devuelve ni se loguea.
- [ ] La aceptación tiene rate limit.
- [ ] Se registra auditoría de activación.
- [ ] Si la empresa está inactiva, no se permite la activación.

## Escenarios Gherkin

```gherkin
Scenario: Activar una cuenta válida
  Given una invitación vigente para "solicitante@demo.com" de la empresa "Demo SRL"
  When el solicitante abre el link y define una contraseña válida
  Then se crea un usuario administrador para "Demo SRL"
  And la invitación queda marcada como usada
  And se redirige al login con el mensaje "Cuenta activada"
```

```gherkin
Scenario: Token vencido
  Given una invitación con fecha de expiración pasada
  When el solicitante intenta activar la cuenta
  Then la API devuelve 410 con código "INVITACION_VENCIDA"
  And no se crea ningún usuario
```

```gherkin
Scenario: Token ya usado
  Given una invitación que ya fue aceptada
  When se intenta usar el mismo token otra vez
  Then la API devuelve 409 con código "INVITACION_USADA"
  And no se crea otro usuario
```

```gherkin
Scenario: Contraseña débil
  Given una invitación vigente
  When el solicitante define la contraseña "123456"
  Then la API devuelve 400 con código "PASSWORD_DEBIL"
  And la invitación sigue vigente
```

```gherkin
Scenario: Aceptación concurrente
  Given una invitación vigente
  When dos requests aceptan el token al mismo tiempo
  Then solo uno crea el usuario
  And el otro recibe 409
```

## Tareas técnicas

- [ ] Implementar `IInvitacionRepository` con búsqueda por hash de token.
- [ ] Implementar `ValidarInvitacionService`.
- [ ] Implementar `AceptarInvitacionService` con transacción y control de concurrencia.
- [ ] Crear `GET /v1/invitaciones/{token}`.
- [ ] Crear `POST /v1/invitaciones/{token}/aceptar`.
- [ ] Crear la página `/activar` y el cliente HTTP.
- [ ] Aplicar la política de contraseña.
- [ ] Crear el usuario con `PasswordHasher`.
- [ ] Marcar la invitación como usada y registrar auditoría.
- [ ] Agregar rate limit.
- [ ] Agregar tests de token válido, vencido, usado, revocado, contraseña débil y concurrencia.
- [ ] Documentar el flujo y los códigos de error.

## Definition of Done

- [ ] El flujo invitación → activación → login funciona de punta a punta.
- [ ] Un token no puede usarse dos veces.
- [ ] El usuario creado queda asociado a la empresa correcta.
- [ ] El email y el contribuyente no se pueden manipular desde el formulario.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el proceso de alta.

## Notas de alcance

- No implementa recuperación de contraseña.
- No implementa doble factor.
- No permite cambiar el email durante la activación; si es incorrecto, el administrador debe reenviar la invitación al email correcto.
- No crea API keys; eso ocurre en US-02.
