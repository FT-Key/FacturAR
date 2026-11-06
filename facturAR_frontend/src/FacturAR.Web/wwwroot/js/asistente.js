(function () {
    'use strict';

    var CLAVE_ABIERTO = 'facturar-asistente-abierto';
    var CLAVE_HISTORIAL = 'facturar-asistente-historial';
    var MAX_TURNOS_ENVIO = 8;
    var MAX_MENSAJES = 40;
    var BIENVENIDA_POR_DEFECTO =
        'Hola. Te ayudo con el alta de tu empresa en FacturAR. ¿Querés saber qué datos pide el formulario o cómo sigue el proceso?';

    var raiz = null;
    var boton = null;
    var panel = null;
    var cerrar = null;
    var mensajes = null;
    var sugerencias = null;
    var error = null;
    var errorTexto = null;
    var reintentar = null;
    var textarea = null;
    var contador = null;
    var enviar = null;

    var bienvenida = BIENVENIDA_POR_DEFECTO;
    var maxCaracteres = 600;

    var consultaMovil = window.matchMedia ? window.matchMedia('(max-width: 640px)') : null;

    var iniciado = false;
    var raizIniciada = null;
    var observer = null;
    var escuchaEscape = false;
    var escuchaMovil = false;

    var abierto = false;
    var enviando = false;
    var indicador = null;
    var ultimoEnvio = null;
    var historial = [];

    function esMovil() {
        return consultaMovil ? consultaMovil.matches : window.innerWidth <= 640;
    }

    function nodo(id, respaldo) {
        var encontrado = document.getElementById(id);
        return encontrado || respaldo;
    }

    function campoTexto() {
        var campo = nodo('asistente-texto', textarea);
        if (campo) {
            textarea = campo;
        }

        return textarea;
    }

    function esTextarea(elemento) {
        return Boolean(elemento && elemento.id === 'asistente-texto');
    }

    function resolverElementos() {
        raiz = document.getElementById('asistente');

        if (!raiz) {
            return false;
        }

        boton = document.getElementById('asistente-toggle');
        panel = document.getElementById('asistente-panel');
        cerrar = document.getElementById('asistente-cerrar');
        mensajes = document.getElementById('asistente-mensajes');
        sugerencias = document.getElementById('asistente-sugerencias');
        error = document.getElementById('asistente-error');
        errorTexto = document.getElementById('asistente-error-texto');
        reintentar = document.getElementById('asistente-reintentar');
        textarea = document.getElementById('asistente-texto');
        contador = document.getElementById('asistente-contador');
        enviar = document.getElementById('asistente-enviar');

        return Boolean(boton && panel && mensajes && textarea && enviar);
    }

    function leerAbierto() {
        try {
            return window.sessionStorage.getItem(CLAVE_ABIERTO) === 'true';
        } catch (e) {
            return false;
        }
    }

    function guardarAbierto(valor) {
        try {
            window.sessionStorage.setItem(CLAVE_ABIERTO, valor ? 'true' : 'false');
        } catch (e) {
        }
    }

    function leerHistorial() {
        try {
            var crudo = window.sessionStorage.getItem(CLAVE_HISTORIAL);
            if (!crudo) {
                return [];
            }
            var datos = JSON.parse(crudo);
            if (!Array.isArray(datos)) {
                return [];
            }
            return datos.filter(function (mensaje) {
                return mensaje
                    && (mensaje.rol === 'user' || mensaje.rol === 'assistant')
                    && typeof mensaje.texto === 'string';
            }).slice(-MAX_MENSAJES);
        } catch (e) {
            return [];
        }
    }

    function guardarHistorial() {
        try {
            window.sessionStorage.setItem(CLAVE_HISTORIAL, JSON.stringify(historial.slice(-MAX_MENSAJES)));
        } catch (e) {
        }
    }

    function crearMensaje(rol, texto) {
        mensajes = nodo('asistente-mensajes', mensajes);

        var fila = document.createElement('div');
        fila.className = 'asistente__mensaje ' + (rol === 'user'
            ? 'asistente__mensaje--usuario'
            : 'asistente__mensaje--asistente');

        var burbuja = document.createElement('p');
        burbuja.className = 'asistente__burbuja';
        burbuja.textContent = texto;

        fila.appendChild(burbuja);
        mensajes.appendChild(fila);
        mensajes.scrollTop = mensajes.scrollHeight;
    }

    function agregarMensaje(rol, texto) {
        var contenido = typeof texto === 'string' ? texto : '';
        historial.push({ rol: rol, texto: contenido });
        if (historial.length > MAX_MENSAJES) {
            historial = historial.slice(-MAX_MENSAJES);
        }
        guardarHistorial();
        crearMensaje(rol, contenido);
    }

    function renderizarHistorial() {
        mensajes = nodo('asistente-mensajes', mensajes);
        mensajes.textContent = '';
        for (var i = 0; i < historial.length; i += 1) {
            crearMensaje(historial[i].rol, historial[i].texto);
        }
    }

    function renderizarSugerencias(lista) {
        sugerencias = nodo('asistente-sugerencias', sugerencias);
        if (!sugerencias) {
            return;
        }

        sugerencias.textContent = '';
        if (!lista || !lista.length) {
            sugerencias.hidden = true;
            return;
        }

        sugerencias.hidden = false;
        for (var i = 0; i < lista.length; i += 1) {
            var botonSugerencia = document.createElement('button');
            botonSugerencia.type = 'button';
            botonSugerencia.className = 'asistente__sugerencia';
            botonSugerencia.setAttribute('data-sugerencia', '');
            botonSugerencia.textContent = lista[i];
            sugerencias.appendChild(botonSugerencia);
        }
    }

    function mostrarEscribiendo() {
        if (indicador) {
            return;
        }

        mensajes = nodo('asistente-mensajes', mensajes);

        indicador = document.createElement('div');
        indicador.className = 'asistente__mensaje asistente__mensaje--asistente asistente__mensaje--escribiendo';

        var burbuja = document.createElement('p');
        burbuja.className = 'asistente__burbuja';
        burbuja.textContent = 'Escribiendo…';

        indicador.appendChild(burbuja);
        mensajes.appendChild(indicador);
        mensajes.scrollTop = mensajes.scrollHeight;
    }

    function ocultarEscribiendo() {
        if (!indicador) {
            return;
        }

        if (indicador.parentNode) {
            indicador.parentNode.removeChild(indicador);
        }
        indicador = null;
    }

    function mostrarError(mensaje, permitirReintento) {
        error = nodo('asistente-error', error);
        errorTexto = nodo('asistente-error-texto', errorTexto);
        reintentar = nodo('asistente-reintentar', reintentar);
        mensajes = nodo('asistente-mensajes', mensajes);

        if (!error || !errorTexto) {
            return;
        }

        error.hidden = false;
        errorTexto.textContent = mensaje;
        if (reintentar) {
            reintentar.hidden = !permitirReintento;
        }
        mensajes.scrollTop = mensajes.scrollHeight;
    }

    function ocultarError() {
        error = nodo('asistente-error', error);
        errorTexto = nodo('asistente-error-texto', errorTexto);

        if (!error || !errorTexto) {
            return;
        }

        error.hidden = true;
        errorTexto.textContent = '';
    }

    function actualizarContador() {
        contador = nodo('asistente-contador', contador);
        if (contador) {
            contador.textContent = campoTexto().value.length + '/' + maxCaracteres;
        }
    }

    function ajustarAltura() {
        var campo = campoTexto();
        campo.style.height = 'auto';
        campo.style.height = Math.min(campo.scrollHeight, 96) + 'px';
    }

    function deshabilitar(valor) {
        textarea = nodo('asistente-texto', textarea);
        enviar = nodo('asistente-enviar', enviar);

        textarea.disabled = valor;
        enviar.disabled = valor;
        raiz.setAttribute('data-enviando', valor ? 'true' : 'false');
    }

    function obtenerToken() {
        var campo = document.querySelector('input[name="__RequestVerificationToken"]');
        return campo ? campo.value : '';
    }

    function prepararHistorial(lista) {
        var preparado = [];
        var desde = Math.max(0, lista.length - MAX_TURNOS_ENVIO);
        for (var i = desde; i < lista.length; i += 1) {
            var texto = typeof lista[i].texto === 'string' ? lista[i].texto : '';
            if (texto.length > maxCaracteres) {
                texto = texto.slice(0, maxCaracteres);
            }
            if (texto.length === 0) {
                continue;
            }
            preparado.push({
                rol: lista[i].rol === 'user' ? 'user' : 'assistant',
                texto: texto
            });
        }
        return preparado;
    }

    function manejarErrorHttp(respuesta, datos) {
        var mensaje = datos && typeof datos.mensaje === 'string' ? datos.mensaje : '';

        if (respuesta.status === 429) {
            var espera = respuesta.headers.get('Retry-After');
            mostrarError(
                espera
                    ? 'Esperá ' + espera + ' segundos antes de volver a intentar.'
                    : 'Esperá un momento antes de volver a intentar.',
                true);
            return;
        }

        if (respuesta.status === 503) {
            mostrarError(
                mensaje || 'No pude responder ahora. Podés revisar las preguntas frecuentes o completar el formulario.',
                true);
            return;
        }

        if (respuesta.status === 400) {
            mostrarError(mensaje || 'No pude procesar el mensaje. Revisalo y probá de nuevo.', true);
            return;
        }

        mostrarError('No pude responder ahora. Probá de nuevo en unos minutos.', true);
    }

    function enviarTexto(texto, esReintento) {
        if (enviando) {
            return;
        }

        if (!abierto) {
            abrirPanel(false);
        }

        var contenido = (texto || '').trim();
        var historialEnvio;

        if (esReintento && ultimoEnvio) {
            contenido = ultimoEnvio.texto;
            historialEnvio = ultimoEnvio.historial.slice();
        } else {
            if (!contenido) {
                campoTexto().focus();
                return;
            }

            if (contenido.length > maxCaracteres) {
                mostrarError('El mensaje no puede superar los ' + maxCaracteres + ' caracteres.', false);
                return;
            }

            historialEnvio = prepararHistorial(historial);
            agregarMensaje('user', contenido);
            campoTexto().value = '';
            actualizarContador();
            ajustarAltura();
        }

        ocultarError();
        ultimoEnvio = { texto: contenido, historial: historialEnvio };
        enviando = true;
        deshabilitar(true);
        mostrarEscribiendo();

        fetch('/asistente/mensajes', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': obtenerToken()
            },
            body: JSON.stringify({ mensaje: contenido, historial: historialEnvio })
        }).then(function (respuesta) {
            if (respuesta.ok) {
                return respuesta.json().then(function (datos) {
                    ocultarEscribiendo();
                    ultimoEnvio = null;
                    var respuestaTexto = datos && typeof datos.respuesta === 'string' ? datos.respuesta : '';
                    agregarMensaje('assistant', respuestaTexto);
                    renderizarSugerencias(datos && Array.isArray(datos.sugerencias) ? datos.sugerencias : []);
                });
            }

            return respuesta.json().catch(function () {
                return {};
            }).then(function (datos) {
                ocultarEscribiendo();
                manejarErrorHttp(respuesta, datos);
            });
        }).catch(function () {
            ocultarEscribiendo();
            mostrarError('No pudimos conectarnos. Revisá tu conexión y volvé a probar.', true);
        }).then(function () {
            enviando = false;
            deshabilitar(false);
            if (abierto) {
                campoTexto().focus();
            }
        });
    }

    function reintentarEnvio() {
        if (!ultimoEnvio) {
            ocultarError();
            return;
        }

        enviarTexto(ultimoEnvio.texto, true);
    }

    function aplicarModalidad() {
        panel = nodo('asistente-panel', panel);

        if (!panel || !abierto) {
            if (panel) {
                panel.removeAttribute('role');
                panel.removeAttribute('aria-modal');
            }
            return;
        }

        if (esMovil()) {
            panel.setAttribute('role', 'dialog');
            panel.setAttribute('aria-modal', 'true');
        } else {
            panel.removeAttribute('role');
            panel.removeAttribute('aria-modal');
        }
    }

    function abrirPanel(enfocar) {
        panel = nodo('asistente-panel', panel);
        boton = nodo('asistente-toggle', boton);

        if (!panel || !boton) {
            return;
        }

        abierto = true;
        panel.hidden = false;
        boton.setAttribute('aria-expanded', 'true');
        aplicarModalidad();
        guardarAbierto(true);

        if (mensajes.childElementCount === 0 && historial.length > 0) {
            renderizarHistorial();
        }

        if (enfocar !== false) {
            window.setTimeout(function () {
                campoTexto().focus();
            }, 0);
        }
    }

    function cerrarPanel() {
        panel = nodo('asistente-panel', panel);
        boton = nodo('asistente-toggle', boton);

        abierto = false;

        if (panel) {
            panel.hidden = true;
        }

        if (boton) {
            boton.setAttribute('aria-expanded', 'false');
        }

        aplicarModalidad();
        guardarAbierto(false);

        if (boton) {
            boton.focus();
        }
    }

    function aplicarTrampaFoco(evento) {
        if (!abierto || !esMovil()) {
            return;
        }

        var panelActual = nodo('asistente-panel', panel);
        if (!panelActual) {
            return;
        }

        var enfocables = panelActual.querySelectorAll('button, [href], textarea, input, select, [tabindex]:not([tabindex="-1"])');
        var visibles = [];
        for (var i = 0; i < enfocables.length; i += 1) {
            if (!enfocables[i].disabled && enfocables[i].offsetParent !== null) {
                visibles.push(enfocables[i]);
            }
        }

        if (!visibles.length) {
            return;
        }

        var primero = visibles[0];
        var ultimo = visibles[visibles.length - 1];

        if (evento.shiftKey && document.activeElement === primero) {
            evento.preventDefault();
            ultimo.focus();
        } else if (!evento.shiftKey && document.activeElement === ultimo) {
            evento.preventDefault();
            primero.focus();
        }
    }

    function manejarClick(evento) {
        var objetivo = evento.target;

        if (!objetivo || typeof objetivo.closest !== 'function') {
            return;
        }

        var sugerencia = objetivo.closest('[data-sugerencia], .asistente__sugerencia');
        if (sugerencia) {
            enviarTexto(sugerencia.textContent);
            return;
        }

        if (objetivo.closest('#asistente-toggle')) {
            if (abierto) {
                cerrarPanel();
            } else {
                abrirPanel(true);
            }
            return;
        }

        if (objetivo.closest('#asistente-cerrar')) {
            cerrarPanel();
            return;
        }

        if (objetivo.closest('#asistente-enviar')) {
            enviarTexto(campoTexto().value);
            return;
        }

        if (objetivo.closest('#asistente-reintentar')) {
            reintentarEnvio();
        }
    }

    function manejarInput(evento) {
        if (!esTextarea(evento.target)) {
            return;
        }

        textarea = evento.target;
        actualizarContador();
        ajustarAltura();
    }

    function manejarKeydown(evento) {
        if (evento.key === 'Tab') {
            aplicarTrampaFoco(evento);
            return;
        }

        if (!esTextarea(evento.target)) {
            return;
        }

        textarea = evento.target;

        if (evento.key === 'Enter' && !evento.shiftKey && !evento.isComposing) {
            evento.preventDefault();
            enviarTexto(textarea.value);
        }
    }

    function manejarEscape(evento) {
        if (evento.key !== 'Escape' || !abierto) {
            return;
        }

        evento.preventDefault();
        cerrarPanel();
    }

    function iniciar() {
        bienvenida = raiz.getAttribute('data-bienvenida') || BIENVENIDA_POR_DEFECTO;

        maxCaracteres = parseInt(raiz.getAttribute('data-max-caracteres'), 10);
        if (!maxCaracteres || maxCaracteres < 1) {
            maxCaracteres = 600;
        }

        raiz.addEventListener('click', manejarClick);
        raiz.addEventListener('input', manejarInput);
        raiz.addEventListener('keydown', manejarKeydown);

        if (!escuchaEscape) {
            document.addEventListener('keydown', manejarEscape);
            escuchaEscape = true;
        }

        if (!escuchaMovil && consultaMovil) {
            if (typeof consultaMovil.addEventListener === 'function') {
                consultaMovil.addEventListener('change', aplicarModalidad);
            } else if (typeof consultaMovil.addListener === 'function') {
                consultaMovil.addListener(aplicarModalidad);
            }

            escuchaMovil = true;
        }

        historial = leerHistorial();

        if (mensajes.childElementCount === 0) {
            if (historial.length === 0) {
                agregarMensaje('assistant', bienvenida);
            } else {
                renderizarHistorial();
            }
        }

        actualizarContador();
        ajustarAltura();

        if (leerAbierto()) {
            abrirPanel(false);
        }
    }

    function intentarIniciar() {
        var candidata = document.getElementById('asistente');

        if (!candidata) {
            return;
        }

        if (iniciado && raizIniciada === candidata) {
            return;
        }

        if (!resolverElementos()) {
            return;
        }

        iniciado = true;
        raizIniciada = raiz;
        iniciar();
    }

    function observar() {
        if (observer || typeof MutationObserver !== 'function') {
            return;
        }

        observer = new MutationObserver(function () {
            intentarIniciar();
        });

        observer.observe(document.documentElement, { childList: true, subtree: true });
    }

    function arrancar() {
        intentarIniciar();
        observar();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', arrancar);
    } else {
        arrancar();
    }
}());
