// Ezra: confirm before delete, and the editor's live preview and mobile Write/Preview toggle.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('form[data-confirm]').forEach(form => {
        form.addEventListener('submit', e => {
            if (!confirm(form.dataset.confirm)) e.preventDefault();
        });
    });

    const editor = document.querySelector('.ezra-editor');
    if (!editor) return;
    const body = editor.querySelector('textarea');
    const panes = editor.querySelector('.ezra-panes');
    const preview = editor.querySelector('.ezra-preview');
    const tabs = editor.querySelectorAll('.ezra-tabs button');
    let timer;
    let latest = 0;

    // Posts the whole form (it carries the antiforgery token) and shows only
    // the newest response, so slow replies can't overwrite newer text.
    function refresh() {
        clearTimeout(timer);
        const request = ++latest;
        fetch(editor.dataset.previewUrl, { method: 'POST', body: new FormData(editor) })
            .then(r => {
                // A redirect means the sign-in expired and we got the login page.
                if (!r.ok || r.redirected) throw new Error(r.status);
                return r.text();
            })
            .then(html => { if (request === latest) preview.innerHTML = html; })
            .catch(() => { if (request === latest) preview.textContent = 'Preview unavailable. Are you still signed in?'; });
    }

    body.addEventListener('input', () => {
        clearTimeout(timer);
        timer = setTimeout(refresh, 400);
    });

    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            panes.dataset.mode = tab.dataset.mode;
            tabs.forEach(t => t.setAttribute('aria-pressed', String(t === tab)));
            if (tab.dataset.mode === 'preview') refresh();
        });
    });
});
