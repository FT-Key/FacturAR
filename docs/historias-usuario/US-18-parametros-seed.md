# US-18 — Parámetros y datos semilla

**Épica:** Operación  
**Prioridad:** Media  
**Estimación:** 1 día  
**Requisito:** RF-18  
**Depende de:** US-02, US-04, US-07  
**Habilita:** US-11, US-21

## Historia

Como equipo de desarrollo, quiero contar con parámetros configurables y datos semilla idempotentes para demostrar el sistema y ejecutar pruebas sin cargar datos a mano.

## Contexto

Los parámetros incluyen alícuotas de IVA, días de vencimiento del CAE, moneda, punto de venta inicial y límites de paginación. Los datos semilla se cargan solo en Development o mediante un comando explícito.

## Criterios de aceptación

- [ ] Existe una fuente de parámetros para alícuotas, vencimiento de CAE, moneda y límites.
- [ ] Los valores por defecto coinciden con las reglas del MVP.
- [ ] Los parámetros se pueden sobrescribir por configuración sin recompilar.
- [ ] El seed es idempotente: ejecutarlo dos veces no duplica datos.
- [ ] El seed crea un contribuyente demo con CUIT válido y punto de venta 1.
- [ ] El seed crea usuarios demo `Administrador`, `Emisor` y `Consulta`.
- [ ] El seed crea secuencias iniciales si corresponde.
- [ ] El seed puede crear una API key demo solo en Development y muestra el valor una vez.
- [ ] El seed no se ejecuta en Production sin una bandera explícita.
- [ ] Las contraseñas demo se toman de configuración y se hashean.
- [ ] Los datos demo están rotulados como simulados.
- [ ] Hay tests de idempotencia y de parámetros.
- [ ] El README documenta cómo ejecutar el seed.

## Escenarios Gherkin

```gherkin
Scenario: Seed en Development
  Given una base vacía y el entorno Development
  When se ejecuta el seed
  Then se crea la empresa demo, sus usuarios y su punto de venta
  And se puede iniciar sesión con el usuario administrador demo
```

```gherkin
Scenario: Seed idempotente
  Given un seed ya ejecutado
  When se ejecuta nuevamente
  Then no se duplican contribuyentes, usuarios, puntos de venta ni secuencias
```

```gherkin
Scenario: Seed en Production
  Given el entorno Production y la bandera de seed deshabilitada
  When arranca la aplicación
  Then no se cargan datos demo
```

```gherkin
Scenario: Parámetro de vencimiento
  Given el parámetro de días de CAE en 10
  When se emite un comprobante
  Then el vencimiento es la fecha de emisión más 10 días
```

## Tareas técnicas

- [ ] Crear `ParametrosFacturacion` y su lectura desde configuración.
- [ ] Crear `SeedService` idempotente.
- [ ] Crear los datos demo de empresa, usuarios, punto de venta y secuencias.
- [ ] Crear la API key demo solo en Development si está configurada.
- [ ] Agregar la bandera `Seed__Enabled`.
- [ ] Integrar el seed al arranque o a un comando documentado.
- [ ] Agregar tests de idempotencia y parámetros.
- [ ] Documentar datos demo y credenciales de desarrollo sin secretos reales.

## Definition of Done

- [ ] Un entorno nuevo se puede demoar con el seed.
- [ ] El seed no duplica datos.
- [ ] Production no carga datos demo por accidente.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el README.

## Contrato técnico

### Modelo de datos

#### Colección `parametros` (nueva)

US-18 agrega la colección `parametros` (`MongoCollections.Parametros = "parametros"`). Permite sobrescribir, por contribuyente, los valores globales de la sección de configuración.

| Campo | Tipo | Obligatorio | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | Sí | Clave primaria |
| `ContribuyenteId` | `Guid?` | Sí | `null` para un valor global de plataforma |
| `Clave` | `string` | Sí | Nombre del parámetro, por ejemplo `DiasVencimientoCae` |
| `Valor` | `string` | Sí | Valor serializado. Las listas se guardan separadas por `;` |
| `UpdatedAt` | `DateTime` | Sí | UTC |

Índices:

| Colección | Índice | Tipo |
| --- | --- | --- |
| `parametros` | `ux_parametros_contribuyente_clave` — `ContribuyenteId` + `Clave` | Único |

El índice es único sobre `(ContribuyenteId, Clave)`. Como `null` es un valor regular en MongoDB, solo puede existir **un** documento global por clave y **uno** por contribuyente y clave. Ese índice es el que impide que se acumulen duplicados o que dos claves compitan.

**Orden de resolución del valor:**

```text
Override por contribuyente (parametros con ContribuyenteId = X)
  → configuración global (Facturacion__<Clave>)
  → valor por defecto en código
```

#### Colecciones que el seed escribe

El seed escribe con *upsert* sobre las colecciones existentes, sin crear colecciones nuevas:

| Colección | Clave natural del upsert | Índice único que lo respalda |
| --- | --- | --- |
| `contribuyentes` | `Cuit` | `ux_contribuyentes_cuit` |
| `usuarios` | `ContribuyenteId` + `Email` | `ux_usuarios_contribuyente_email` |
| `administradores` | `Email` | `administradores.Email` (único) |
| `puntosVenta` | `ContribuyenteId` + `Numero` | `ux_puntos_venta_contribuyente_numero` |
| `secuencias` | `ContribuyenteId` + `PuntoVenta` + `TipoComprobante` | `ux_secuencias_contribuyente_punto_tipo` |
| `parametros` | `ContribuyenteId` + `Clave` | `ux_parametros_contribuyente_clave` |

Los índices de `administradores` los crea US-27; hasta entonces el seed de US-18 debe ejecutarse después de US-27 o crear ese índice. La dependencia está declarada más abajo.

### Configuración

Sección `Facturacion`, con prefijo de entorno `Facturacion__`:

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `Facturacion__DiasVencimientoCae` | `int` | `10` | Días de vigencia del CAE |
| `Facturacion__AlicuotasPermitidas` | lista de `decimal` | `0, 5, 10.5, 21, 27` | Alícuotas de IVA aceptadas |
| `Facturacion__PuntoVentaPorDefecto` | `int` | `1` | Punto de venta sugerido al crear el contribuyente |
| `Facturacion__Moneda` | `string` | `ARS` | Moneda única del MVP |
| `Facturacion__PaginaPorDefecto` | `int` | `20` | `pageSize` por defecto de los listados |
| `Facturacion__PaginaMaxima` | `int` | `100` | `pageSize` máximo de los listados |

Las dos últimas cubren los "límites de paginación" que pide el Contexto de la historia y que hoy están fijados en `docs/contratos-api.md` §6. US-18 debe actualizar la tabla de §10 de ese documento para incorporar estas dos claves.

El binding se hace con `IOptions<ParametrosFacturacion>` sobre la sección `Facturacion`. Los valores por defecto viven en la clase de opciones, no en el código de negocio, para que cambiar un default no requiera recompilar.

#### Vencimiento del CAE: resolución única

US-05 fijó el vencimiento del CAE en **10 días fijos** con `CaeGenerator.CalcularVencimiento`. US-18 lo resuelve de **una sola forma**:

> **`Facturacion__DiasVencimientoCae` es la única fuente del valor.** El default `10` reproduce exactamente el comportamiento de US-05, de modo que con la configuración por defecto no cambia nada. La implementación requiere ajustar `CaeGenerator`: `CalcularVencimiento` pasa a recibir los días como parámetro (`CalcularVencimiento(DateTime fechaEmision, int diasVencimiento)`) y el caso de uso de emisión resuelve el valor con `IParametrosResolver` y lo pasa. `CaeGenerator` sigue siendo estático, sin dependencias de DI ni de Infrastructure. Si un contribuyente tiene un override en `parametros`, ese valor manda sobre el global.

Consecuencia: cambiar `Facturacion__DiasVencimientoCae` a un valor distinto de `10` **altera** el comportamiento fijado por US-05. Por eso el default debe permanecer en `10` salvo decisión explícita, y cualquier override debe quedar registrado en la documentación del ambiente. No hay dos caminos de cálculo: o el parámetro manda, o el parámetro no existe.

### Seed

#### Bandera

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `Seed__Enabled` | `bool` | `false` | Habilita la carga de datos demo |
| `Seed__Cuit` | `string` | `20123456786` | CUIT de la empresa demo |
| `Seed__RazonSocial` | `string` | `Empresa Demo (simulada)` | Razón social de la empresa demo |
| `Seed__Email` | `string` | `admin@demo.facturar.local` | Email de los usuarios demo de la empresa |
| `Seed__Nombre` | `string` | `Administrador Demo` | Nombre visible de los usuarios demo |
| `Seed__Password` | `string` | — | Contraseña de las cuentas demo. Se hashea antes de persistir |
| `Seed__ApiKeyDemo` | `bool` | `false` | Crea la API key demo. Solo se honra en Development |

`Seed__Enabled` es la **bandera explícita** que exige el criterio de aceptación: con `false` (default) no se carga nada, en ningún ambiente, incluido Production. Con `Seed__Enabled=true` en Production, la carga ocurre: por eso la bandera debe estar ausente o en `false` en los archivos de Production (`.env.production.example`) y en los manifiestos de despliegue.

El seed corre **al arrancar la API**, antes de atender requests, y es idempotente: puede ejecutarse cuántas veces se quiera sin duplicar.

#### Datos que crea

| Entidad | Datos | Clave de idempotencia |
| --- | --- | --- |
| `contribuyentes` | 1 empresa demo con CUIT válido, razón social, `CondicionIva` = `ResponsableInscripto`, email y `Activo` = `true` | `Cuit` |
| `usuarios` | 3 usuarios de la empresa demo: `Administrador`, `Emisor` y `Consulta`, con `PasswordHash` y `EmailVerificadoAt` informado | `ContribuyenteId` + `Email` |
| `administradores` | 1 `SuperAdmin` de plataforma, con `Rol` = `RolGlobal.SuperAdmin` y `Activo` = `true` | `Email` |
| `puntosVenta` | Punto de venta `1` (`Casa Central`) activo para la empresa demo | `ContribuyenteId` + `Numero` |
| `secuencias` | Secuencias `FacturaA`, `FacturaB` y `FacturaC` en `PuntoVenta` 1, con `UltimoNumero` = `0` | `ContribuyenteId` + `PuntoVenta` + `TipoComprobante` |
| `parametros` | Los valores por defecto de la sección `Facturacion`, con `ContribuyenteId` = `null` | `ContribuyenteId` + `Clave` |
| `apiKeys` | 1 API key demo para la empresa demo, **solo en Development y solo con `Seed__ApiKeyDemo=true`** | `Prefix` |

Reglas de idempotencia:

- Todos los inserts son *upsert* por clave natural. Si el documento ya existe, **solo se actualizan los campos de configuración** (`Activo`, `Nombre`, `Rol`), nunca los que llevan historial.
- Una `secuencia` existente **no** se reinicia: `UltimoNumero` se conserva. Ejecutar el seed sobre una base con comprobantes emitidos no puede duplicar números.
- Las contraseñas se hashean con el mismo algoritmo de US-04. La contraseña en claro nunca se persiste, no se escribe en un log y no se versiona.
- Los datos demo se rotulan como simulados en la razón social (`Empresa Demo (simulada)`).

#### API key demo

Cuando `Seed__ApiKeyDemo=true` y el ambiente es Development, el seed crea una API key de la empresa demo. Su valor completo **no** se escribe en un log, en un archivo versionado ni en el repositorio: se entrega **una sola vez** a través del archivo local de desarrollo `seed/api-key-demo.txt` (mismo patrón que el archivo de desarrollo de emails de US-29), que debe estar en `.gitignore` y en `.dockerignore` y tratarse como secreto local. El valor nunca se vuelve a poder recuperar: solo queda el `Prefix` y el `Hash`.

En Development sin `Seed__ApiKeyDemo=true`, el seed no crea ninguna API key. Para obtener una key de prueba se usa el flujo normal de US-02 o US-14, que ya muestra el valor una vez.

### Casos de uso y servicios

| Pieza | Capa | Responsabilidad |
| --- | --- | --- |
| `ParametrosFacturacion` | `FacturAR.Application` (o `FacturAR.Contracts` si se comparte con el portal) | Clase de opciones con los defaults de la sección `Facturacion`. Sin dependencias de Infrastructure |
| `IParametrosResolver` | `FacturAR.Application/Abstractions` | `Task<string?> ObtenerAsync(string clave, Guid? contribuyenteId, CancellationToken)` |
| `ParametrosService` | `FacturAR.Application` | Aplica el orden de resolución: override por contribuyente → configuración → default |
| `IParametrosRepository` | `FacturAR.Application/Abstractions` | Lectura de `parametros` |
| `ParametrosRepository` | `FacturAR.Infrastructure` | Implementación sobre la colección `parametros` |
| `SeedService` | `FacturAR.Application` | Orquesta la carga idempotente usando las interfaces de repositorio. **No** conoce MongoDB |
| `CaeGenerator.CalcularVencimiento` | `FacturAR.Application/Comprobantes` (existente) | Cambia a `CalcularVencimiento(DateTime fechaEmision, int diasVencimiento)`. Sigue siendo estático |

`Application` sigue sin referenciar `MongoDB.Driver`; `SeedService` y `ParametrosService` trabajan contra interfaces.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-02 | Aporta el repositorio de `apiKeys` para la key demo y el formato de la key |
| US-04 | Aporta el hashing de contraseñas, `RolUsuario` y la creación de `AdministradorFacturar` |
| US-07 | Aporta la colección `secuencias` y el punto de venta por defecto |
| US-05 | Consume `Facturacion__DiasVencimientoCae` a través de `CaeGenerator` |
| US-11 | La verificación pública ya funciona; US-18 solo aporta el dato de demo |
| US-21 | El README debe documentar cómo ejecutar el seed |
| US-27 | Aporta `AdministradorFacturar` y su índice único por email, que el seed necesita |

### Documentación a sincronizar

- `docs/contratos-api.md` §10: agregar `Facturacion__PaginaPorDefecto` y `Facturacion__PaginaMaxima`, y dejar explícito que `Facturacion__DiasVencimientoCae` es la fuente del vencimiento con default `10`.
- `docs/contratos-api.md` §11: actualizar la tabla del seed, que hoy lista un solo `Administrador` de empresa, a los tres roles `Administrador`, `Emisor` y `Consulta`, más el `SuperAdmin` de plataforma.
- `docs/modelos.md`: agregar la entidad `Parametro` y su índice, y el índice único de `administradores.Email`.
- `README.md`: sección de cómo ejecutar el seed, con las claves de configuración y las credenciales demo **sin secretos reales**.
- `.env.example` y `.env.production.example`: `Seed__Enabled=false` explícito.

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-18.01 | Configuración por defecto | Resolver los parámetros globales | `DiasVencimientoCae` = 10, `AlicuotasPermitidas` = {0, 5, 10.5, 21, 27}, `PuntoVentaPorDefecto` = 1, `Moneda` = `ARS`, `PaginaPorDefecto` = 20 y `PaginaMaxima` = 100 |
| CP-18.02 | `Facturacion__Moneda` = `USD` por variable de entorno | Resolver el parámetro sin recompilar | Devuelve `USD`: el override de configuración gana sobre el default |
| CP-18.03 | Documento en `parametros` con `ContribuyenteId` = X y `Clave` = `Moneda` | Resolver `Moneda` para X y para otra empresa | Para X devuelve el override; para la otra empresa devuelve el valor global |
| CP-18.04 | Configuración por defecto | Emitir un comprobante | `CaeVencimiento` = fecha de emisión + 10 días, idéntico a US-05 |
| CP-18.05 | Override por contribuyente `DiasVencimientoCae` = 5 | Emitir un comprobante para ese contribuyente | `CaeVencimiento` = fecha de emisión + 5 días; otro contribuyente sigue con 10 |
| CP-18.06 | Base vacía, `Seed__Enabled=true` | Arrancar la API | Se crean 1 contribuyente, 3 usuarios (`Administrador`, `Emisor`, `Consulta`), 1 `SuperAdmin`, el punto de venta 1, las 3 secuencias en `0` y los parámetros por defecto |
| CP-18.07 | Seed ya ejecutado | Arrancar la API otra vez | Los conteos de `contribuyentes`, `usuarios`, `administradores`, `puntosVenta`, `secuencias` y `parametros` no cambian: el seed es idempotente |
| CP-18.08 | Secuencia con `UltimoNumero` = 7 | Ejecutar el seed | `UltimoNumero` sigue en 7: el seed no reinicia contadores |
| CP-18.09 | `Seed__Enabled=false` | Arrancar la API contra una base vacía | No se crea ningún dato demo |
| CP-18.10 | Ambiente Production con `Seed__Enabled=false` | Arrancar la API | No se carga ningún dato demo |
| CP-18.11 | Ambiente Production con `Seed__Enabled=true` explícito | Arrancar la API | Se carga el seed: la bandera es la única autorización y debe constar en la configuración del ambiente |
| CP-18.12 | Development con `Seed__ApiKeyDemo=false` | Ejecutar el seed | No se crea ninguna API key |
| CP-18.13 | Development con `Seed__ApiKeyDemo=true` | Ejecutar el seed | Se crea 1 API key de la empresa demo; su valor completo aparece una sola vez en el archivo local de desarrollo, no hay key en los logs y el repositorio no la contiene |
| CP-18.14 | Seed ejecutado | Inspeccionar `usuarios` y `administradores` | `PasswordHash` está hasheado, no hay ninguna contraseña en texto plano |
| CP-18.15 | Seed ejecutado | Inspeccionar `parametros` | Los valores por defecto están persistidos con `ContribuyenteId` = `null` y `UpdatedAt` en UTC |

## Dependencias técnicas

| Dependencia | Historia que la introduce | Detalle técnico requerido |
| --- | --- | --- |
| Gestión de API keys | US-02 | Repositorio de `apiKeys`, formato `fak_<prefix>_<secreto>` y hash SHA-256 para la key demo |
| Hashing de contraseñas y modelo de administrador global | US-04 | `PasswordHash`, `RolUsuario`, `RolGlobal` y la entidad `AdministradorFacturar` |
| Numeración y punto de venta | US-07 | Colección `secuencias` con su índice único y la convención de `PuntoVenta` |
| Administrador global persistido | US-27 | `AdministradorFacturar` con índice único por `Email`, necesario para el upsert del `SuperAdmin` |
| Vencimiento del CAE | US-05 | Ajuste de `CaeGenerator.CalcularVencimiento` para recibir los días y que el caso de uso de emisión resuelva el valor |
| Índices de MongoDB | US-19, US-20 | `MongoIndexInitializer` debe crear `ux_parametros_contribuyente_clave` y `ix_parametros_...` cuando corresponda. `Mongo__InitializeIndexes=true` sigue siendo el disparador |
| Configuración por ambiente | US-19 | `.env`, `appsettings.json` y los archivos de ejemplo para que `Facturacion__` y `Seed__` se puedan sobrescribir sin recompilar |
| Build y hosting | US-20 | La API arranca, corre el seed y la suite corre en CI |

## Notas de alcance

- No implementa un panel de administración de parámetros.
- No implementa migraciones de datos.
- No crea datos reales de AFIP.
- No commitear contraseñas ni API keys demo reales.
