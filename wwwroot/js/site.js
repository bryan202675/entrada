const menuButton = document.querySelector('[data-menu-button]');
const menu = document.querySelector('[data-menu]');

const closeMenu = () => {
    menu?.classList.remove('is-open');
    menuButton?.setAttribute('aria-expanded', 'false');
};

menuButton?.addEventListener('click', () => {
    const open = menu?.classList.toggle('is-open') ?? false;
    menuButton.setAttribute('aria-expanded', String(open));
});

document.addEventListener('click', event => {
    if (menu?.classList.contains('is-open') && event.target instanceof Element && !event.target.closest('.nav-shell')) closeMenu();
});

document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && menu?.classList.contains('is-open')) {
        closeMenu();
        menuButton?.focus();
    }
});

menu?.querySelectorAll('a').forEach(link => link.addEventListener('click', closeMenu));

document.querySelectorAll('[data-loading-button]').forEach(button => {
    button.closest('form')?.addEventListener('submit', event => {
        if (!event.defaultPrevented) {
            button.classList.add('is-loading');
            button.setAttribute('disabled', 'disabled');
        }
    });
});

document.querySelectorAll('[data-confirm]').forEach(button => {
    button.addEventListener('click', event => {
        if (!window.confirm(button.getAttribute('data-confirm') ?? '¿Continuar?')) {
            event.preventDefault();
        }
    });
});

document.querySelectorAll('[data-image-fallback]').forEach(image => {
    image.addEventListener('error', () => {
        const fallback = image.getAttribute('data-image-fallback');
        if (fallback && image.getAttribute('src') !== fallback) image.setAttribute('src', fallback);
    });
});

const search = document.querySelector('[data-event-search]');
const cards = [...document.querySelectorAll('[data-event-card]')];
const searchEmpty = document.querySelector('[data-search-empty]');
const searchCount = document.querySelector('[data-search-count]');
const normalizeSearch = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('es');

search?.addEventListener('input', () => {
    const term = normalizeSearch(search.value.trim());
    let visible = 0;
    cards.forEach(card => {
        const matches = !term || normalizeSearch(card.dataset.search ?? '').includes(term);
        card.hidden = !matches;
        if (matches) visible += 1;
    });
    if (searchEmpty) searchEmpty.hidden = visible !== 0;
    if (searchCount) searchCount.textContent = String(visible);
});

document.querySelector('[data-search-clear]')?.addEventListener('click', () => {
    if (!search) return;
    search.value = '';
    search.dispatchEvent(new Event('input'));
    search.focus();
});

if (location.hash === '#buscar') requestAnimationFrame(() => {
    search?.focus({ preventScroll: true });
    search?.closest('.catalog-tools')?.scrollIntoView({ block: 'start' });
});

const toast = document.querySelector('[data-toast]');
if (toast) window.setTimeout(() => toast.remove(), 4200);
