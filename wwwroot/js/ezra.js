// Ezra: confirm before delete, the header search box, and the editor's live preview and mobile Write/Preview toggle.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('form[data-confirm]').forEach(form => {
        form.addEventListener('submit', e => {
            if (!confirm(form.dataset.confirm)) e.preventDefault();
        });
    });

    const search = document.querySelector('.ezra-search');
    if (search) setUpSearch(search);

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

    const tagBox = editor.querySelector('.ezra-tags');
    if (tagBox) setUpTags(tagBox);
});

// Tag chips with a searchable flyout (an ARIA combobox). Typing filters the
// existing tags; when nothing matches exactly, a "Create" option adds a new
// one. Each chip carries a hidden "Tags" input, so nothing is saved until the
// form is. The name rule comes from the server (TagName.cs).
function setUpTags(box) {
    const all = JSON.parse(box.dataset.allTags);
    const pattern = new RegExp(box.dataset.tagPattern, 'u');
    const chips = box.querySelector('.ezra-chips');
    const input = box.querySelector('[role="combobox"]');
    const list = box.querySelector('[role="listbox"]');
    let options = [];
    let active = -1;

    const clean = s => s.normalize('NFC').trim().replace(/\s+/g, ' ');
    const same = (a, b) => a.toLowerCase() === b.toLowerCase();
    const applied = () => [...chips.querySelectorAll('input')].map(i => i.value);

    function render() {
        const query = clean(input.value);
        const have = applied();
        options = all
            .filter(t => !have.some(h => same(h, t)) && t.toLowerCase().includes(query.toLowerCase()))
            .map(name => ({ name }));
        if (query && !all.some(t => same(t, query)) && !have.some(h => same(h, query)))
            options.push({ name: query, create: true, valid: pattern.test(query) });

        list.replaceChildren(...options.map((option, i) => {
            const li = document.createElement('li');
            li.id = 'tag-option-' + i;
            li.setAttribute('role', 'option');
            if (option.create && !option.valid) {
                li.textContent = `"${option.name}": ${box.dataset.tagRule}`;
                li.setAttribute('aria-disabled', 'true');
            } else {
                li.textContent = option.create ? `Create "${option.name}"` : option.name;
            }
            // Keep focus in the input, so the flyout doesn't close before the tap lands.
            li.addEventListener('pointerdown', e => e.preventDefault());
            li.addEventListener('click', () => pick(i));
            return li;
        }));
        active = options.length ? 0 : -1;
        highlight();
        show(options.length > 0);
    }

    function show(open) {
        list.hidden = !open;
        input.setAttribute('aria-expanded', String(open));
        if (!open) input.removeAttribute('aria-activedescendant');
    }

    function highlight() {
        [...list.children].forEach((li, i) => li.setAttribute('aria-selected', String(i === active)));
        if (active < 0) return;
        input.setAttribute('aria-activedescendant', 'tag-option-' + active);
        list.children[active].scrollIntoView({ block: 'nearest' });
    }

    function pick(i) {
        const option = options[i];
        if (!option || (option.create && !option.valid)) return;
        if (option.create) all.push(option.name);
        addChip(option.name);
        input.value = '';
        input.focus();
        render();
    }

    function addChip(name) {
        const li = document.createElement('li');
        li.className = 'ezra-chip';
        const hidden = Object.assign(document.createElement('input'), { type: 'hidden', name: 'Tags', value: name });
        const remove = Object.assign(document.createElement('button'), { type: 'button', textContent: '×' });
        remove.setAttribute('aria-label', 'Remove tag ' + name);
        li.append(name, hidden, remove);
        chips.append(li);
    }

    chips.addEventListener('click', e => {
        const remove = e.target.closest('button');
        if (!remove) return;
        remove.closest('li').remove();
        input.focus();
    });

    input.addEventListener('focus', render);
    input.addEventListener('input', render);
    input.addEventListener('blur', () => show(false));
    input.addEventListener('keydown', e => {
        const open = !list.hidden;
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
            if (!open) return render();
            if (!options.length) return;
            active = (active + (e.key === 'ArrowDown' ? 1 : -1) + options.length) % options.length;
            highlight();
        } else if (e.key === 'Enter') {
            e.preventDefault(); // never submit the note from the tag box
            if (open && active >= 0) pick(active);
        } else if (e.key === 'Escape' && open) {
            e.preventDefault();
            show(false);
        } else if (e.key === 'Backspace' && input.value === '') {
            chips.lastElementChild?.remove();
            if (open) render();
        }
    });
}

// The header search box: free text plus filter chips. As you type, the
// server suggests tags, scripture and Strong's numbers that match the end of
// the text (NoteSearch.Suggest); picking one swaps those words for a chip.
// Each chip carries a hidden tag/ref/strongs input for the GET form. Enter
// searches unless an option is highlighted with the arrow keys.
function setUpSearch(form) {
    const chips = form.querySelector('.ezra-chips');
    const input = form.querySelector('[role="combobox"]');
    const list = form.querySelector('[role="listbox"]');
    const classes = { tag: 'ezra-chip', ref: 'ezra-chip ezra-chip-ref', strongs: 'ezra-chip ezra-chip-strongs' };
    let options = [];
    let active = -1;
    let timer;
    let latest = 0;

    const applied = () => [...chips.querySelectorAll('input')].map(i => i.name + ':' + i.value.toLowerCase());

    function fetchOptions() {
        clearTimeout(timer);
        const request = ++latest;
        const text = input.value;
        if (!text.trim()) return show([]);
        fetch(form.dataset.suggestUrl + '?q=' + encodeURIComponent(text))
            .then(r => { if (!r.ok) throw new Error(r.status); return r.json(); })
            .then(found => {
                if (request !== latest) return;
                const have = applied();
                show(found.filter(o => !have.includes(o.kind + ':' + o.value.toLowerCase())));
            })
            .catch(() => { if (request === latest) show([]); });
    }

    function show(found) {
        options = found;
        active = -1;
        list.replaceChildren(...options.map((option, i) => {
            const li = document.createElement('li');
            li.id = 'search-option-' + i;
            li.setAttribute('role', 'option');
            const chip = Object.assign(document.createElement('span'), { className: classes[option.kind], textContent: option.label });
            li.append(chip);
            // Keep focus in the input, so the flyout doesn't close before the tap lands.
            li.addEventListener('pointerdown', e => e.preventDefault());
            li.addEventListener('click', () => pick(i));
            return li;
        }));
        highlight();
        open(options.length > 0);
    }

    function open(isOpen) {
        list.hidden = !isOpen;
        input.setAttribute('aria-expanded', String(isOpen));
    }

    function highlight() {
        [...list.children].forEach((li, i) => li.setAttribute('aria-selected', String(i === active)));
        if (active < 0) return input.removeAttribute('aria-activedescendant');
        input.setAttribute('aria-activedescendant', 'search-option-' + active);
        list.children[active].scrollIntoView({ block: 'nearest' });
    }

    function pick(i) {
        const option = options[i];
        if (!option) return;
        addChip(option);
        // Drop the words the chip replaces, keep the rest as free text.
        const words = input.value.trim().split(/\s+/);
        input.value = words.slice(0, Math.max(0, words.length - option.words)).join(' ');
        if (input.value) input.value += ' ';
        input.focus();
        show([]);
    }

    function addChip(option) {
        const li = document.createElement('li');
        li.className = classes[option.kind];
        const hidden = Object.assign(document.createElement('input'), { type: 'hidden', name: option.kind, value: option.value });
        const remove = Object.assign(document.createElement('button'), { type: 'button', textContent: '×' });
        remove.setAttribute('aria-label', 'Remove ' + option.label);
        li.append(option.label, hidden, remove);
        chips.append(li);
    }

    chips.addEventListener('click', e => {
        const remove = e.target.closest('button');
        if (!remove) return;
        remove.closest('li').remove();
        input.focus();
    });

    input.addEventListener('input', () => {
        clearTimeout(timer);
        timer = setTimeout(fetchOptions, 200);
    });
    input.addEventListener('blur', () => open(false));
    input.addEventListener('keydown', e => {
        const isOpen = !list.hidden;
        if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && isOpen && options.length) {
            e.preventDefault();
            active = (active + (e.key === 'ArrowDown' ? 1 : -1) + options.length + 1) % (options.length + 1);
            if (active === options.length) active = -1; // back to the text
            highlight();
        } else if (e.key === 'Enter' && isOpen && active >= 0) {
            e.preventDefault();
            pick(active);
        } else if (e.key === 'Escape' && isOpen) {
            e.preventDefault();
            open(false);
        } else if (e.key === 'Backspace' && input.value === '') {
            chips.lastElementChild?.remove();
        }
    });
}
