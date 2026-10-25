---
name: blazor-ux-performance
description: Mejora UX, accesibilidad y rendimiento en Blazor Server y Razor Pages de FacturAR. Usar al crear o modificar .razor, .cshtml, formularios, listas o llamadas HTTP del portal.
---

# Blazor Server y Razor Pages

## Reglas de arquitectura

- El portal nunca accede a MongoDB. Toda lectura/escritura pasa por la API con un cliente HTTP tipado.
- No pongas secretos en componentes, `appsettings` públicos ni JavaScript del navegador.
- Mantené DTOs en `FacturAR.Contracts`; no dupliques modelos de API en la UI.

## Asincronía y circuito

- Usá `async`/`await` en todo I/O. Nunca bloquees el circuito con `.Result` o `.Wait()`.
- Pasá `CancellationToken` cuando la API lo permita.
- No llames a la API en el constructor; usá `OnInitializedAsync` o el handler correspondiente.
- Evitá `StateHasChanged` manual salvo eventos externos; no lo llames en bucles.
- Para listas grandes, usá paginación del servidor y virtualización si hace falta.

## UX y estados

- Mostrá estado de carga, deshabilitá botones mientras se envía y evitá envíos duplicados.
- Mostrá errores claros y accionables; no ocultes fallas con un mensaje genérico.
- Preservá los datos del formulario ante un error de validación.
- Confirmá acciones destructivas.
- Usá `EditForm` con validación y mensajes junto al campo.
- Después de un alta exitosa, redirigí o limpiá el formulario de forma explícita.

## Accesibilidad

- Etiquetas asociadas a inputs, `aria-*` cuando haga falta y foco visible.
- Contraste suficiente, texto alternativo y navegación por teclado.
- No dependas solo del color para indicar estado.
- Mensajes de error anunciados y asociados al campo.

## Rendimiento

- Evitá payloads enormes y renderizar listas completas sin paginar.
- Usá `@key` en listas dinámicas para preservar componentes.
- No cargues datos que la vista no muestra; pedí solo lo necesario a la API.
- Evitá JavaScript pesado; si es necesario, aislalo y justificalo.

## Razor Pages

- `OnGetAsync`/`OnPostAsync` para I/O.
- Mantené antiforgery y validación de modelo.
- Redirigí después de un POST exitoso (PRG).
- No pongas lógica de negocio en la página; delegá en la API o en un servicio del frontend.

## Verificación

- Compilá y probá el flujo con Playwright si está disponible.
- Revisá consola del navegador, estados de error y al menos una vista móvil y una de escritorio.
