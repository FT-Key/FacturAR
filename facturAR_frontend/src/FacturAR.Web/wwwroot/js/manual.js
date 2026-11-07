(function () {
    'use strict';

    function respaldoCopiar(texto, callback) {
        var area = document.createElement('textarea');
        area.value = texto;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        try {
            document.execCommand('copy');
            callback();
        } catch (e) {
        }
        document.body.removeChild(area);
    }

    function conectarBotonesCopiar() {
        var botones = document.querySelectorAll('[data-copiar-manual]');
        for (var i = 0; i < botones.length; i += 1) {
            (function (boton) {
                var textoOriginal = boton.textContent || 'Copiar';
                var temporizador = null;

                boton.addEventListener('click', function () {
                    var objetivoId = boton.getAttribute('data-copiar-target');
                    var bloque = boton.closest('.code-block');
                    var objetivo = objetivoId
                        ? document.getElementById(objetivoId)
                        : bloque
                            ? bloque.querySelector('pre code, pre')
                            : null;

                    if (!objetivo) {
                        return;
                    }

                    var codigo = objetivo.textContent || '';
                    var estado = objetivoId
                        ? document.querySelector('[data-copiar-estado="' + objetivoId + '"]')
                        : null;

                    var notificarCopiado = function () {
                        if (temporizador) {
                            window.clearTimeout(temporizador);
                        }

                        boton.setAttribute('data-copiado', 'true');
                        boton.textContent = 'Copiado';
                        if (estado) {
                            estado.textContent = 'Código copiado al portapapeles';
                        }

                        temporizador = window.setTimeout(function () {
                            boton.removeAttribute('data-copiado');
                            boton.textContent = textoOriginal;
                            if (estado) {
                                estado.textContent = '';
                            }
                        }, 2000);
                    };

                    if (navigator.clipboard && navigator.clipboard.writeText) {
                        navigator.clipboard.writeText(codigo).then(notificarCopiado, function () {
                            respaldoCopiar(codigo, notificarCopiado);
                        });
                    } else {
                        respaldoCopiar(codigo, notificarCopiado);
                    }
                });
            }(botones[i]));
        }
    }

    function conectarTablaContenidos() {
        var detalles = document.querySelector('.manual-toc__details');
        if (!detalles) {
            return;
        }

        var consultaMovil = window.matchMedia ? window.matchMedia('(max-width: 960px)') : null;
        var sincronizarApertura = function () {
            if (consultaMovil && consultaMovil.matches) {
                detalles.removeAttribute('open');
            } else {
                detalles.setAttribute('open', '');
            }
        };

        sincronizarApertura();
        if (consultaMovil && consultaMovil.addEventListener) {
            consultaMovil.addEventListener('change', sincronizarApertura);
        }

        var enlaces = detalles.querySelectorAll('.manual-toc__link');
        for (var i = 0; i < enlaces.length; i += 1) {
            enlaces[i].addEventListener('click', function () {
                if (consultaMovil && consultaMovil.matches) {
                    detalles.removeAttribute('open');
                }
            });
        }
    }

    function iniciar() {
        conectarBotonesCopiar();
        conectarTablaContenidos();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
}());

