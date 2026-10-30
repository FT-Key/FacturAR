# Manual de marca — FacturAR

<p>
  <img src="logos/facturar-horizontal-light.svg" alt="FacturAR" height="56">
</p>

Manual de marca de **FacturAR**, el simulador de facturación electrónica AFIP/ARCA: onboarding de empresas, emisión de comprobantes A, B y C con CAE simulado, API keys, portal y verificación pública por CAE.

## 1. Esencia

FacturAR convierte un trámite fiscal en un flujo ágil. La marca tiene que transmitir dos cosas a la vez: **confianza** (es facturación, los números tienen que estar bien) y **velocidad** (emitir un comprobante es cuestión de segundos).

- **Azul** = confianza, institucionalidad, el mundo fiscal. Es el color dominante.
- **Morado** = el toque digital y moderno: aparece en el pliegue del isotipo, en la barra que subraya el logotipo y en el sello del CAE. Siempre como acento, nunca como protagonista.
- **Diagonal e inclinación** = movimiento. La F del isotipo está inclinada 10° y el pliegue corta la esquina en 45°.

Tagline: *Facturación electrónica, sin fricción.*

## 2. Logo

### Construcción
- **Isotipo**: una placa de 64×64 con radio 14 (el mismo que `radius-lg`) y la esquina superior derecha plegada, como una hoja de comprobante. Dentro, una **F** armada con tres barras de punta redonda (`radius-pill`) e inclinada 10°: las barras evocan los renglones de una factura y la inclinación, la velocidad.
- **Pliegue**: triángulo en `accent-500`. Es el único lugar del isotipo donde aparece el morado.
- **Logotipo**: "FacturAR" completo en un solo color (`ink`), en Plus Jakarta Sans ExtraBold (800) con tracking −2%. El nombre nunca se parte en dos colores.
- **Barra de acento**: debajo del nombre, una barra en `accent-500` de punta redonda, inclinada 10° y de un tercio del ancho de la palabra. Es la misma pieza que las barras de la F: un renglón de factura que "sale" del isotipo y le da el toque de color al logotipo.

### Versiones (carpeta [`logos/`](logos/))

| Claro | Oscuro |
|---|---|
| <img src="logos/facturar-horizontal-light.svg" height="48"> | <img src="logos/facturar-horizontal-dark.svg" height="48"> |
| <img src="logos/facturar-vertical-light.svg" height="96"> | <img src="logos/facturar-vertical-dark.svg" height="96"> |
| <img src="logos/facturar-isotipo-light.svg" height="48"> | <img src="logos/facturar-isotipo-dark.svg" height="48"> |

> La versión oscura se ve sobre fondo oscuro; en GitHub con tema claro puede verse con poco contraste.

| Versión | Archivo | Uso |
|---|---|---|
| Horizontal clara | `facturar-horizontal-light.svg` | Encabezado del portal, documentos y PDF del comprobante sobre fondo claro. **Versión principal.** |
| Horizontal oscura | `facturar-horizontal-dark.svg` | Portal en modo oscuro, presentaciones con fondo `bg` oscuro. |
| Vertical clara / oscura | `facturar-vertical-*.svg` | Pantallas de login, splash, portadas, redes. |
| Isotipo claro / oscuro | `facturar-isotipo-*.svg` | Favicon, avatar de redes, ícono de app, sello en el PDF. |
| Monocromo tinta | `*-mono-ink.svg` | Impresión a una tinta, sellos, fondos claros con poca tinta. |
| Monocromo blanco | `*-mono-white.svg` | Sobre `brand-500`, fotos o fondos oscuros saturados. |

### Área de resguardo y tamaños mínimos
- Margen libre alrededor del logo: **la altura de una barra de la F** (≈ 15% del alto del isotipo) en todos los lados. En la práctica, `space-4` alrededor del logo horizontal de 48px.
- Horizontal: **mínimo 96px** de ancho en pantalla / 25 mm impreso.
- Isotipo: **mínimo 16px** (favicon). Debajo de 24px se puede usar la versión monocromo para ganar legibilidad.

### Usos incorrectos
- No cambiar los colores fuera de las versiones oficiales.
- No partir el nombre en dos colores ("Factur" + "AR"): el color va solo en la barra.
- No mover, estirar ni enderezar la barra de acento, ni usarla sin el nombre.
- No enderezar la F ni girar el isotipo.
- No aplicar degradados, sombras ni contornos al logo.
- No usar la versión clara sobre fondos oscuros (ni al revés): para eso están las versiones oscura y mono blanco.
- No reescribir "FacturAR" con otra tipografía: usar siempre los archivos SVG (el texto está convertido a trazos).
- Escribir siempre **FacturAR** (F y AR en mayúscula). Nunca "Facturar", "FACTURAR" ni "Factur.AR".

## 3. Color

Dos temas con los mismos nombres de token: **Claro** (por defecto) y **Oscuro**. Todo el texto cumple 4.5:1 sobre su fondo en ambos temas.

### Proporción
- **~70% neutros** (`bg`, `surface`, `ink`): la interfaz respira.
- **~20% azul** (`brand-500` y su escala): acciones, enlaces, navegación activa.
- **≤10% morado** (`accent-500`): detalles que hay que notar — la barra del logotipo, el sello CAE, el foco, una novedad.
- **Estados** (`success`, `warning`, `danger`): solo para estados reales de comprobantes y solicitudes, nunca decorativos.

### Paleta núcleo
| Token | Claro | Oscuro | Rol |
|---|---|---|---|
| `brand-500` | #2F5BEA | #5B82FF | Azul FacturAR |
| `accent-500` | #7C3AED | #A78BFA | Morado de acento |
| `ink` | #0E1A33 | #EAF0FF | Texto |
| `bg` | #F6F8FC | #0A1022 | Fondo |
| `surface` | #FFFFFF | #111A33 | Tarjetas |

En modo oscuro, el texto sobre botones azules pasa a `on-brand` oscuro (#0A1022) porque el azul se aclara para mantener contraste.

### Reglas
- El morado **no** se usa para botones primarios; los primarios son siempre `brand-500`.
- Nada de degradados azul→morado de fondo: el contraste entre ambos colores se logra poniéndolos uno al lado del otro (como en el pliegue), no mezclándolos.
- El foco de teclado usa `focus` (morado) en 2px con 2px de separación: así no se confunde con un borde azul de selección.

## 4. Tipografía

- **Plus Jakarta Sans** (400–800) para todo: interfaz, titulares y logotipo. Geométrica, contemporánea y muy legible en números.
- **JetBrains Mono** (400, 600) para datos fiscales: CAE, CUIT, número de comprobante, API keys y código del manual de integración. Los números en mono se alinean en columnas y se leen dígito por dígito.

Archivos en [`fonts/`](fonts/). Escala (definida en [`tokens.json`](tokens.json)): `display-xl` 56 → `display` 40 → `h1` 32 → `h2` 24 → `h3` 18 → `body-lg` 17 → `body` 15 → `label` 14 → `caption` 13 → `overline` 12.

Reglas: titulares en 700–800 con tracking negativo; nunca más de dos pesos por bloque; los importes van alineados a la derecha y en mono.

## 5. Voz y tono

FacturAR habla como un contador joven que sabe de tecnología: claro, directo y en **voseo rioplatense**.

| Sí | No |
|---|---|
| "Tu solicitud está en revisión. Te avisamos por email." | "Su solicitud ha sido recibida y será procesada a la brevedad." |
| "Emitiste la Factura B 0003-00001284." | "Operación realizada con éxito." |
| "El CUIT no es válido: revisá el dígito verificador." | "Error 400: datos inválidos." |

- Frases cortas, verbo primero en botones: **Emitir factura**, **Aprobar solicitud**, **Copiar API key**.
- Usar los nombres oficiales del dominio: CAE, CUIT, punto de venta, comprobante, ARCA (ex AFIP).
- Sin emojis en la interfaz.

## 6. Iconografía

El portal ya usa Bootstrap; los íconos son **Bootstrap Icons** en estilo *outline*, a 16px en texto y 20px en navegación, en `currentColor`. Íconos rellenos solo para estados (check de autorizado, cruz de rechazado). Nunca íconos en morado salvo el sello CAE.

## 7. Movimiento

La marca es dinámica, pero el movimiento siempre comunica algo:
- **Duración**: 120ms en hovers, 200ms en paneles, 320ms en la animación del logo.
- **Curva**: `cubic-bezier(0.2, 0.8, 0.2, 1)` (sale rápido, frena suave).
- **Animación del isotipo** (splash, login, CAE emitido): las tres barras de la F entran de izquierda a derecha con 40ms de desfase y luego el pliegue "cae" desde la esquina. 
- Respetar `prefers-reduced-motion`: sin animación, logo estático.

## 8. Componentes y aplicaciones

- **Botón**: primario azul, secundario con borde, fantasma; radio `radius-md`.
- **Estado**: insignias pill para Autorizado / Pendiente / Rechazado / Borrador.
- **Comprobante**: tarjeta con tipo y número en `overline`, total en mono y el **sello CAE** en morado con el mini isotipo.
- **Logo**: versiones y animación en ambos temas.

## 9. Implementación en el repo

El frontend (`facturAR_frontend/src/FacturAR.Web`) usa Bootstrap 5 con los colores por defecto en `wwwroot/app.css`. Para aplicar la marca:

```css
@import url("tokens.css"); /* docs/manual-marca/tokens.css, copiado a wwwroot/ */
:root, [data-theme="light"] {
  --bs-primary: var(--brand-500);
  --bs-primary-rgb: 47, 91, 234;
  --bs-link-color: var(--brand-500);
  --bs-body-bg: var(--bg);
  --bs-body-color: var(--ink);
  --bs-border-radius: 10px;
  --bs-font-sans-serif: "Plus Jakarta Sans", system-ui, sans-serif;
}
[data-theme="dark"] { --bs-primary-rgb: 91, 130, 255; }
```

- Cambiar el modo con `data-theme="dark"` en `<html>` (o `data-bs-theme="dark"` si se usa el modo oscuro de Bootstrap, apuntando sus variables a estos tokens).
- Reemplazar `wwwroot/favicon.png` por el isotipo claro y el encabezado del `NavMenu` por el logo horizontal.
- El PDF del comprobante (QuestPDF) usa el logo horizontal claro, `ink` para texto y `accent-500` solo en el sello CAE.

## 10. Archivos de esta carpeta

| Archivo | Contenido |
|---|---|
| [`tokens.json`](tokens.json) | Fuente de verdad: colores (temas claro y oscuro), tipografía, espaciado, radios y sombras. |
| [`tokens.css`](tokens.css) | Los mismos tokens como variables CSS (`--brand-500`, `--space-4`…), `@font-face` y clases de texto (`.h1`, `.body`, `.mono`…). Tema con `data-theme="light|dark"` en `<html>`; sin atributo sigue al sistema. |
| [`logos/`](logos/) | 12 SVG oficiales: horizontal, vertical e isotipo en claro, oscuro, mono tinta y mono blanco. |
| [`fonts/`](fonts/) | Plus Jakarta Sans (400–800) y JetBrains Mono (400, 600) en woff2, licencia SIL OFL. |
