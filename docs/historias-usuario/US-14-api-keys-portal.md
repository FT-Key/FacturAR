# US-14 — API keys desde el portal

**Épica:** Portal  
**Prioridad:** Media  
**Estimación:** 1 día  
**Requisito:** RF-14  
**Depende de:** US-02, US-04, US-28  
**Habilita:** US-21, US-30

## Historia

Como administrador de una empresa, quiero administrar mis API keys desde el portal para crear integraciones sin usar la API ni compartir mi contraseña.

## Contexto

La sección API keys consume los endpoints de US-02. El valor completo se muestra una sola vez y no se vuelve a solicitar al servidor.

## Criterios de aceptación

- [ ] Solo un usuario `Administrador` puede acceder a la sección.
- [ ] La sección lista keys con nombre, prefijo, estado, creación y último uso.
- [ ] Se puede crear una key con nombre.
- [ ] Al crear, se muestra el valor completo una única vez con una advertencia.
- [ ] Se puede copiar el valor al portapapeles.
- [ ] Al cerrar el modal, el valor deja de mostrarse y no se puede recuperar.
- [ ] Se puede revocar una key con confirmación.
- [ ] Se puede regenerar una key con confirmación y se muestra el nuevo valor una vez.
- [ ] La lista se actualiza después de crear, revocar o regenerar.
- [ ] Un usuario de otra empresa no puede ver ni modificar keys ajenas.
- [ ] La UI muestra estados de carga, vacío y error.
- [ ] La sección es responsive y accesible por teclado.
- [ ] Hay una prueba manual del flujo completo.

## Escenarios Gherkin

```gherkin
Scenario: Crear key desde el portal
  Given un administrador autenticado
  When crea una key llamada "Ecommerce"
  Then ve el valor completo una única vez
  And la lista muestra la key con su prefijo
```

```gherkin
Scenario: Copiar key
  Given una key recién creada
  When el administrador pulsa "Copiar"
  Then el valor se copia al portapapeles
  And el portal muestra una confirmación
```

```gherkin
Scenario: Cerrar el modal
  Given una key recién creada visible
  When el administrador cierra el modal
  Then el valor completo ya no se muestra
  And no hay forma de recuperarlo desde la lista
```

```gherkin
Scenario: Revocar key
  Given una key activa
  When el administrador confirma la revocación
  Then la key aparece como revocada
  And deja de autenticar en la API
```

```gherkin
Scenario: Usuario Consulta
  Given un usuario con rol "Consulta"
  When intenta abrir la sección API keys
  Then el portal le muestra acceso denegado
```

## Tareas técnicas

- [ ] Crear la página de API keys en el portal.
- [ ] Integrar listado, creación, revocación y regeneración.
- [ ] Crear el modal de valor único con botón de copiar.
- [ ] Agregar confirmaciones de revocación y regeneración.
- [ ] Aplicar autorización por rol.
- [ ] Agregar estados de carga, vacío y error.
- [ ] Agregar prueba manual y, si es posible, test de integración del endpoint.
- [ ] Enlazar el manual de integración.

## Definition of Done

- [ ] El flujo crear → copiar → revocar funciona desde el portal.
- [ ] El valor completo no se puede recuperar después del alta.
- [ ] La sección está aislada por empresa y rol.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa scopes ni permisos por key.
- No implementa historial de uso detallado.
- No implementa envío de keys por email.
