(function () {
    'use strict';

    let disparador = null;

    function cerrar(dialogo) {
        if (dialogo instanceof HTMLDialogElement && dialogo.open) {
            dialogo.close();
        }
    }

    document.addEventListener('click', function (evento) {
        const objetivo = evento.target instanceof Element ? evento.target : null;
        if (!objetivo) {
            return;
        }

        const abrir = objetivo.closest('[data-confirmar-dialogo]');
        if (abrir) {
            evento.preventDefault();
            const dialogo = document.getElementById(abrir.getAttribute('data-confirmar-dialogo'));
            if (!dialogo) {
                return;
            }

            disparador = abrir;
            if (typeof dialogo.showModal === 'function') {
                dialogo.showModal();
            } else {
                dialogo.setAttribute('open', 'open');
            }
            return;
        }

        const confirmar = objetivo.closest('[data-confirmar-envio]');
        if (confirmar) {
            evento.preventDefault();
            const dialogo = confirmar.closest('dialog');
            const boton = disparador;
            cerrar(dialogo);
            disparador = null;

            if (boton && boton.form) {
                boton.form.requestSubmit(boton);
            }
        }
    });

    document.addEventListener('close', function (evento) {
        if (evento.target instanceof HTMLDialogElement) {
            disparador = null;
        }
    }, true);

    document.addEventListener('cancel', function (evento) {
        if (evento.target instanceof HTMLDialogElement) {
            disparador = null;
        }
    }, true);
}());
