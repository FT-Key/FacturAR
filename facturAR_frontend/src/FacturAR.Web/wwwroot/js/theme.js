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
        raiz.setAttribute('data-theme', resolver(normalizada));
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
            botones[i].addEventListener('click', function (evento) {
                cambiar(evento.currentTarget.getAttribute('data-theme-option'));
            });
        }

        if (window.matchMedia) {
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
        conectarTema();
        conectarMenu();
        conectarCopiar();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
}());
