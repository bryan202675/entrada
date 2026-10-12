const form = document.querySelector('[data-booking-form]');

if (form) {
    const quantity = form.querySelector('[data-quantity]');
    const total = form.querySelector('[data-total]');
    const subtotal = form.querySelector('[data-subtotal]');
    const selectedType = form.querySelector('[data-selected-type]');
    const selectedQuantity = form.querySelector('[data-selected-quantity]');
    const radios = form.querySelectorAll('input[name="TipoEntradaId"]');
    const formatter = new Intl.NumberFormat('es-SV', { style: 'currency', currency: 'USD' });
    const selected = () => form.querySelector('input[name="TipoEntradaId"]:checked');

    const update = () => {
        const option = selected();
        if (!option || !quantity || !total) return;
        const limit = Math.min(Number(quantity.max), Number(option.dataset.stock));
        const value = Math.max(1, Math.min(Number(quantity.value) || 1, limit));
        const amount = Number(option.dataset.price) * value;
        quantity.value = String(value);
        total.textContent = formatter.format(amount);
        if (subtotal) subtotal.textContent = formatter.format(amount);
        if (selectedType) selectedType.textContent = option.dataset.name;
        if (selectedQuantity) selectedQuantity.textContent = String(value);
    };

    form.querySelectorAll('[data-step]').forEach(button => {
        button.addEventListener('click', () => {
            quantity.value = String((Number(quantity.value) || 1) + Number(button.dataset.step));
            update();
        });
    });

    quantity?.addEventListener('input', update);
    radios.forEach(radio => radio.addEventListener('change', update));
    update();
}
