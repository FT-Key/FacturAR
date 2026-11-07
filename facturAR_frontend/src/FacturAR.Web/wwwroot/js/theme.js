(function () {
    'use strict';

    var CLAVE = 'facturar-tema';
    var OPCIONES = ['claro', 'oscuro', 'system'];
    var raiz = document.documentElement;

    function normalizar(valor) {
        return OPCIONES.indexOf(valor) >= 0 ? valor : 'system';
    }

    function prefiereOscuro() {
        return !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
    }

    function resolver(preferencia) {
        if (preferencia === 'claro') {
            return 'light';
        }
        if (preferencia === 'oscuro') {
            return 'dark';
        }
        return prefiereOscuro() ? 'dark' : 'light';
    }

    function leerPreferencia() {
        try {
            return normalizar(window.localStorage.getItem(CLAVE));
        } catch (error) {
            return 'system';
        }
    }

    function guardarPreferencia(preferencia) {
        try {
            window.localStorage.setItem(CLAVE, preferencia);
        } catch (error) {
        }
        try {
            document.cookie = CLAVE + '=' + preferencia + '; path=/; max-age=31536000; samesite=lax';
        } catch (error) {
        }
    }

    function actualizarBotones(preferencia) {
        var botones = document.querySelectorAll('[data-theme-option]');
        for (var i = 0; i < botones.length; i += 1) {
            var boton = botones[i];
            var activo = boton.getAttribute('data-theme-option') === preferencia;
            boton.setAttribute('aria-pressed', activo ? 'true' : 'false');
        }
    }

    function aplicar(preferencia) {
        var normalizada = normalizar(preferencia);
        var resuelto = resolver(normalizada);
        raiz.setAttribute('data-theme', resuelto);
        raiz.setAttribute('data-bs-theme', resuelto);
        raiz.setAttribute('data-theme-preference', normalizada);
        actualizarBotones(normalizada);
    }

    function cambiar(preferencia) {
        var normalizada = normalizar(preferencia);
        guardarPreferencia(normalizada);
        aplicar(normalizada);
    }

    function conectarTema() {
        var botones = document.querySelectorAll('[data-theme-option]');
        for (var i = 0; i < botones.length; i += 1) {
            var boton = botones[i];
            if (boton.getAttribute('data-tema-conectado') === 'true') {
                continue;
            }

            boton.setAttribute('data-tema-conectado', 'true');
            boton.addEventListener('click', function (evento) {
                cambiar(evento.currentTarget.getAttribute('data-theme-option'));
            });
        }

        if (window.matchMedia && !window.facturarTemaMediaConectada) {
            window.facturarTemaMediaConectada = true;
            var consulta = window.matchMedia('(prefers-color-scheme: dark)');
            var alCambiar = function () {
                if (leerPreferencia() === 'system') {
                    aplicar('system');
                }
            };

            if (typeof consulta.addEventListener === 'function') {
                consulta.addEventListener('change', alCambiar);
            } else if (typeof consulta.addListener === 'function') {
                consulta.addListener(alCambiar);
            }
        }

        aplicar(leerPreferencia());
    }

    function conectarFormularios() {
        var formularios = document.querySelectorAll('form[data-envio]');
        for (var i = 0; i < formularios.length; i += 1) {
            var formulario = formularios[i];
            if (formulario.getAttribute('data-envio-conectado') === 'true') {
                continue;
            }

            formulario.setAttribute('data-envio-conectado', 'true');
            formulario.addEventListener('submit', function (evento) {
                var form = evento.currentTarget;
                var boton = evento.submitter || form.querySelector('button[type="submit"]');
                if (!boton) {
                    return;
                }

                var texto = boton.getAttribute('data-envio-texto');
                if (texto) {
                    boton.setAttribute('data-texto-original', boton.textContent.trim());
                    boton.textContent = texto;
                }

                var botones = form.querySelectorAll('button[type="submit"]');
                for (var j = 0; j < botones.length; j += 1) {
                    botones[j].disabled = true;
                }
            });
        }
    }

    function restaurarFormularios() {
        var botones = document.querySelectorAll('form[data-envio] button[data-texto-original]');
        for (var i = 0; i < botones.length; i += 1) {
            botones[i].disabled = false;
            botones[i].textContent = botones[i].getAttribute('data-texto-original');
            botones[i].removeAttribute('data-texto-original');
        }
    }

    function conectarMenu() {
        var boton = document.querySelector('[data-nav-toggle]');
        var cabecera = document.querySelector('[data-site-header]');
        if (!boton || !cabecera) {
            return;
        }

        function cerrar() {
            boton.setAttribute('aria-expanded', 'false');
            cabecera.setAttribute('data-nav-open', 'false');
        }

        boton.addEventListener('click', function () {
            var abierto = boton.getAttribute('aria-expanded') === 'true';
            boton.setAttribute('aria-expanded', abierto ? 'false' : 'true');
            cabecera.setAttribute('data-nav-open', abierto ? 'false' : 'true');
        });

        var enlaces = cabecera.querySelectorAll('.site-nav a');
        for (var i = 0; i < enlaces.length; i += 1) {
            enlaces[i].addEventListener('click', cerrar);
        }

        document.addEventListener('keydown', function (evento) {
            if (evento.key === 'Escape') {
                cerrar();
                boton.focus();
            }
        });
    }

    function conectarMenuUsuario() {
        var menus = document.querySelectorAll('[data-user-menu]');
        for (var i = 0; i < menus.length; i += 1) {
            (function (menu) {
                if (menu.getAttribute('data-menu-conectado') === 'true') {
                    return;
                }

                menu.setAttribute('data-menu-conectado', 'true');

                document.addEventListener('click', function (evento) {
                    if (menu.open && !menu.contains(evento.target)) {
                        menu.open = false;
                    }
                });

                menu.addEventListener('keydown', function (evento) {
                    if (evento.key !== 'Escape' || !menu.open) {
                        return;
                    }

                    menu.open = false;
                    var resumen = menu.querySelector('summary');
                    if (resumen) {
                        resumen.focus();
                    }
                });
            }(menus[i]));
        }
    }

    function respaldoCopiar(texto, alCopiar) {
        var area = document.createElement('textarea');
        area.value = texto;
        area.setAttribute('readonly', 'readonly');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        try {
            if (document.execCommand('copy')) {
                alCopiar();
            }
        } catch (error) {
        }
        document.body.removeChild(area);
    }

    function conectarCopiar() {
        var botones = document.querySelectorAll('[data-copiar]');
        for (var i = 0; i < botones.length; i += 1) {
            if (botones[i].hasAttribute('data-copiar-manual')) {
                continue;
            }
            (function (boton) {
                boton.addEventListener('click', function () {
                    var objetivoId = boton.getAttribute('data-copiar-target');
                    var bloque = boton.closest('.code-block');
                    var objetivo = objetivoId
                        ? document.getElementById(objetivoId)
                        : bloque
                            ? bloque.querySelector('pre')
                            : null;

                    if (!objetivo) {
                        return;
                    }

                    var texto = objetivo.textContent || '';
                    var original = boton.textContent;
                    var alCopiar = function () {
                        boton.setAttribute('data-copiado', 'true');
                        boton.textContent = 'Copiado';
                        window.setTimeout(function () {
                            boton.removeAttribute('data-copiado');
                            boton.textContent = original;
                        }, 2000);
                    };

                    if (navigator.clipboard && navigator.clipboard.writeText) {
                        navigator.clipboard.writeText(texto).then(alCopiar, function () {
                            respaldoCopiar(texto, alCopiar);
                        });
                    } else {
                        respaldoCopiar(texto, alCopiar);
                    }
                });
            }(botones[i]));
        }
    }

    function iniciar() {
        guardarPreferencia(leerPreferencia());
        conectarTema();
        conectarFormularios();
        conectarMenu();
        conectarMenuUsuario();
        conectarCopiar();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }

    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancedload', function () {
            restaurarFormularios();
            conectarTema();
            conectarFormularios();
        });
    }
}());
