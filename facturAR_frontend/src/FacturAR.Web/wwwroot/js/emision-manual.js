(() => {
    const marcarEnvio = (form) => {
        const boton = form.querySelector('[data-emitir]');
        if (!boton) {
            return;
        }

        setTimeout(() => {
            boton.disabled = true;
            boton.textContent = 'Emitiendo…';
        }, 0);
    };

    document.addEventListener('change', (event) => {
        const form = event.target?.closest?.('form#form-emision');
        if (!form) {
            return;
        }

        form.requestSubmit();
    });

    document.addEventListener('submit', (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.id !== 'form-emision') {
            return;
        }

        if (event.submitter?.hasAttribute('data-emitir')) {
            marcarEnvio(form);
        }
    });
})();
