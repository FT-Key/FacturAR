let inicializado = false;
let origenFoco = null;
let focoValor = null;

function dialogosAbiertos() { return [...document.querySelectorAll('dialog.js-dialogo')]; }

function abrirPendientes() {
    let abierto = false;
    for (const dialogo of dialogosAbiertos()) {
        if (!dialogo.open) {
            dialogo.showModal();
            abierto = true;
            if (dialogo.id === 'api-key-valor') {
                focoValor = origenFoco || 'crear-api-key';
            }
        }
    }
    return abierto;
}

function enfocarDestino(id) {
    const destino = id ? document.getElementById(id) : null;
    (destino ?? document.getElementById('titulo-listado'))?.focus();
}

function manejarClic(evento) {
    const objetivo = evento.target instanceof Element ? evento.target : null;
    if (!objetivo) return;
    const abrir = objetivo.closest('.js-abrir-dialogo');
    if (abrir) {
        const dialogo = document.getElementById(abrir.getAttribute('aria-controls'));
        if (dialogo) { origenFoco = abrir.id; dialogo.showModal(); }
        return;
    }
    const enlaceCerrar = objetivo.closest('#cerrar-valor');
    if (enlaceCerrar) { enlaceCerrar.closest('dialog')?.close(); return; }
    const cerrar = objetivo.closest('.js-cerrar-dialogo');
    if (cerrar) { cerrar.closest('dialog')?.close(); return; }
    const enfocar = objetivo.closest('.js-enfocar');
    if (enfocar) { document.getElementById(enfocar.getAttribute('aria-controls'))?.focus(); return; }
    const copiar = objetivo.closest('.js-copiar-api-key');
    if (copiar) { void copiarValor(copiar); }
}

async function copiarValor(boton) {
    const campo = document.getElementById('valor-api-key');
    const estado = document.getElementById('estado-copiado');
    if (!campo) return;
    let copiado = false;
    if (window.isSecureContext && navigator.clipboard?.writeText) {
        try { await navigator.clipboard.writeText(campo.value); copiado = true; } catch { copiado = false; }
    }
    if (copiado) {
        if (estado) estado.textContent = 'Copiado';
        boton.focus();
    } else {
        campo.focus();
        campo.select();
        if (estado) estado.textContent = 'No pudimos copiar automáticamente. Seleccioná el valor y copialo con Ctrl+C.';
    }
}

function manejarEnvio(evento) {
    const formulario = evento.target;
    if (!(formulario instanceof HTMLFormElement)) return;
    if (!formulario.closest('.js-contenedor-api-keys')) return;
    const boton = evento.submitter;
    if (boton) {
        const texto = boton.getAttribute('data-envio-texto');
        if (texto) {
            boton.setAttribute('data-texto-original', boton.textContent.trim());
            boton.textContent = texto;
        }
        boton.disabled = true;
    }
    origenFoco = evento.submitter?.id ?? origenFoco;
    document.querySelector('.js-contenedor-api-keys')?.setAttribute('aria-busy', 'true');
}

function manejarCancelacion(evento) {
    const dialogo = evento.target;
    if (!(dialogo instanceof HTMLDialogElement)) return;
    if (dialogo.id === 'api-key-valor') {
        evento.preventDefault();
        dialogo.close();
        document.getElementById('cerrar-valor')?.click();
    }
}

function alActualizar() {
    for (const boton of document.querySelectorAll('.js-contenedor-api-keys button:disabled')) {
        boton.disabled = false;
        const original = boton.getAttribute('data-texto-original');
        if (original !== null) {
            boton.textContent = original;
            boton.removeAttribute('data-texto-original');
        }
    }
    const abierto = abrirPendientes();
    if (!abierto) {
        if (focoValor) {
            enfocarDestino(focoValor);
            focoValor = null;
        } else if (origenFoco) {
            enfocarDestino(origenFoco);
        }
    }
    origenFoco = null;
    document.querySelector('.js-contenedor-api-keys')?.setAttribute('aria-busy', 'false');
}

function inicializar() {
    if (inicializado) return;
    inicializado = true;
    document.addEventListener('click', manejarClic);
    document.addEventListener('submit', manejarEnvio);
    document.addEventListener('cancel', manejarCancelacion, true);
    document.addEventListener('close', (evento) => {
        if (evento.target instanceof HTMLDialogElement && evento.target.id !== 'api-key-valor' && origenFoco) {
            enfocarDestino(origenFoco);
            origenFoco = null;
        }
    }, true);
    window.Blazor?.addEventListener('enhancedload', alActualizar);
    window.Blazor?.addEventListener('enhancednavigationstart', () => {
        document.querySelector('.js-contenedor-api-keys')?.setAttribute('aria-busy', 'true');
    });
    alActualizar();
}

inicializar();
