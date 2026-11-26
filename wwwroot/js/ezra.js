// Ezra: confirm before delete, the tag merge's new-name box, the header search box, the editor's live preview, mobile Write/Preview toggle, the pop-out window and its collection of notes, and tag and references chip boxes.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('form[data-confirm]').forEach(form => {
        form.addEventListener('submit', e => {
            if (!confirm(form.dataset.confirm)) e.preventDefault();
        });
    });

    // Typing in a box picks its radio button (the tag merge's "A new name").
    document.querySelectorAll('input[data-check]').forEach(input => {
        input.addEventListener('input', () => { document.getElementById(input.dataset.check).checked = true; });
    });

    const search = document.querySelector('.ezra-search');
    // Search chips post as ?tag=, ?ref=, ?strongs=; Enter searches.
    if (search) setUpSuggestBox(search, { inputName: kind => kind, enterSubmits: true });

    const popout = document.querySelector('.ezra-popout');
    if (popout) { setUpPopout(popout); return; }

    const collect = document.querySelectorAll('[data-collect-id]');
    if (collect.length > 0) setUpCollect(collect);

    // A signed-in note page (as after a save) tells an open pop-out window to
    // refresh that note's tab.
    const seen = document.querySelector('article[data-note-id]');
    if (seen && 'BroadcastChannel' in window) {
        new BroadcastChannel(PopoutChannel).postMessage({ seen: Number(seen.dataset.noteId), title: seen.dataset.noteTitle });
    }

    const editor = document.querySelector('.ezra-editor');
    if (!editor) return;
    const body = editor.querySelector('textarea');
    const panes = editor.querySelector('.ezra-panes');
    const preview = editor.querySelector('.ezra-preview');
    const tabs = editor.querySelectorAll('.ezra-tabs button');
    const title = editor.querySelector('input[name="Title"]');
    let timer;
    let latest = 0;

    // The pop-out preview window hears every new preview and title over a
    // BroadcastChannel, and asks for the current one when it opens.
    const channel = 'BroadcastChannel' in window ? new BroadcastChannel(PopoutChannel) : null;
    const share = () => channel?.postMessage({ title: title.value, html: preview.innerHTML });
    if (channel) {
        channel.onmessage = e => { if (e.data?.hello) share(); };
        title.addEventListener('input', share);
        // Leaving the editor (saving, cancelling) tells the window to stop waiting.
        window.addEventListener('pagehide', () => channel.postMessage({ closed: true }));
        const popButton = editor.querySelector('[data-popout-url]');
        popButton.hidden = false;
        // The window opens (or comes forward) on its Live preview tab.
        popButton.addEventListener('click', () => {
            const collection = loadCollection();
            collection.active = 'live';
            saveCollection(collection);
            openPopout(popButton.dataset.popoutUrl);
        });
    }

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
            .then(html => { if (request === latest) { preview.innerHTML = html; share(); } })
            .catch(() => { if (request === latest) { preview.textContent = 'Preview unavailable. Are you still signed in?'; share(); } });
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

    // Scripture and Strong's chips only (tags have their own box). Enter
    // never submits the note from here.
    const refBox = editor.querySelector('.ezra-refs');
    if (refBox) setUpSuggestBox(refBox, { kinds: ['ref', 'strongs'], inputName: () => 'ReferenceChips', enterSubmits: false });
});

const PopoutChannel = 'ezra-preview';

// Opens the pop-out window, or brings it forward. It's the same window name
// each time, and a window that's already open isn't reloaded: opening an
// empty URL finds it, and only a new (blank) window is sent to the page. A
// window that went to another site can't be read, so it's sent back too.
function openPopout(url) {
    const popup = window.open('', 'ezra-preview', 'popup,width=640,height=820');
    if (!popup) return;
    let blank;
    try { blank = popup.location.href === 'about:blank'; } catch { blank = true; }
    if (blank) popup.location.href = url;
    popup.focus();
}

// The pop-out collection, one per browser: { tabs: [{ id, title }], active }.
// `active` is a note id or "live". Pages change it and the window follows
// (the storage event). Titles are kept so the tab strip draws without
// fetching every note.
const CollectionKey = 'ezra-collection';

function loadCollection() {
    try {
        const saved = JSON.parse(localStorage.getItem(CollectionKey));
        if (Array.isArray(saved?.tabs)) return saved;
    } catch { }
    return { tabs: [], active: 'live' };
}

function saveCollection(collection) {
    try { localStorage.setItem(CollectionKey, JSON.stringify(collection)); } catch { }
}

// The "Add to pop-out" buttons (the note page's, and the small ones in lists
// and icon panes): each adds its note (once), makes it the active tab and
// opens the window. A button's label (or icon) says when the note is already in.
function setUpCollect(buttons) {
    const label = () => {
        const ids = new Set(loadCollection().tabs.map(t => t.id));
        buttons.forEach(button => {
            const text = ids.has(Number(button.dataset.collectId)) ? 'In pop-out' : 'Add to pop-out';
            const icon = button.querySelector('img');
            if (icon) {
                icon.src = text === 'In pop-out' ? '/images/ezra-collect-in.svg' : '/images/ezra-collect.svg';
                icon.alt = button.title = text;
            } else {
                button.textContent = text;
            }
        });
    };
    label();
    window.addEventListener('storage', e => { if (e.key === CollectionKey || e.key === null) label(); });
    window.addEventListener('pageshow', label);
    buttons.forEach(button => {
        button.hidden = false;
        button.addEventListener('click', () => {
            const id = Number(button.dataset.collectId);
            const collection = loadCollection();
            if (!collection.tabs.some(t => t.id === id)) collection.tabs.push({ id, title: button.dataset.collectTitle });
            collection.active = id;
            saveCollection(collection);
            label();
            openPopout(button.dataset.popoutUrl);
        });
    });
}

// The pop-out window: the pinned "Live preview" tab, then a tab for each
// saved note in the collection. The Live tab shows whatever the open editor
// sends, and asks for the current preview when it opens (or when the editor
// reloads). A saved note is fetched the first time its tab is shown. Saved
// tabs close (×, middle-click, Delete, Close all) and drag to reorder.
function setUpPopout(page) {
    const strip = page.querySelector('[role="tablist"]');
    const closeAll = page.querySelector('.ezra-close-all');
    const livePanel = page.querySelector('#panel-live');
    const body = livePanel.querySelector('.ezra-popout-body');
    const heading = livePanel.querySelector('[data-popout-title]');
    const status = livePanel.querySelector('[data-popout-status]');
    const bar = page.querySelector('[data-popout-bar]');
    const panels = new Map(); // note id → its panel, once fetched
    let collection = loadCollection();
    let liveName = 'Preview';

    const hasTab = id => collection.tabs.some(t => t.id === id);

    // Redraws the saved notes' tabs (after the Live tab, in the collection's
    // order) and drops panels for notes no longer in it.
    function render() {
        strip.querySelectorAll('[data-note]').forEach(tab => tab.remove());
        for (const { id, title } of collection.tabs) {
            const tab = document.createElement('div');
            tab.id = `tab-${id}`;
            tab.dataset.note = id;
            tab.draggable = true;
            tab.setAttribute('role', 'tab');
            tab.setAttribute('aria-controls', `panel-${id}`);
            tab.innerHTML = '<span class="ezra-tab-label"></span><button type="button" class="ezra-tab-close" tabindex="-1">×</button>';
            strip.append(tab);
            setTabTitle(tab, title);
        }
        for (const [id, panel] of panels) {
            if (!hasTab(id)) { panel.remove(); panels.delete(id); }
        }
        if (collection.active !== 'live' && !hasTab(collection.active)) collection.active = 'live';
        closeAll.hidden = collection.tabs.length === 0;
        show();
    }

    function setTabTitle(tab, title) {
        tab.title = tab.querySelector('.ezra-tab-label').textContent = title;
        tab.querySelector('.ezra-tab-close').setAttribute('aria-label', `Close ${title}`);
    }

    // The tab keys in strip order: "live", then the saved notes' ids.
    const keys = () => ['live', ...collection.tabs.map(t => t.id)];
    const tabFor = key => strip.querySelector(key === 'live' ? '#tab-live' : `[data-note="${key}"]`);

    function activate(key, focus) {
        collection.active = key;
        saveCollection(collection);
        show();
        if (focus) tabFor(key).focus();
    }

    // Closing the shown tab shows the next one (or the one before, or Live).
    function closeTab(id, focus) {
        const i = collection.tabs.findIndex(t => t.id === id);
        if (i < 0) return;
        collection.tabs.splice(i, 1);
        if (collection.active === id) collection.active = (collection.tabs[i] ?? collection.tabs[i - 1])?.id ?? 'live';
        saveCollection(collection);
        render();
        if (focus) tabFor(collection.active).focus();
    }

    function show() {
        const active = collection.active;
        strip.querySelectorAll('[role="tab"]').forEach(tab => {
            const on = tab.dataset.note ? Number(tab.dataset.note) === active : active === 'live';
            tab.setAttribute('aria-selected', String(on));
            tab.tabIndex = on ? 0 : -1;
            if (on) tab.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        });
        livePanel.hidden = active !== 'live';
        panels.forEach(panel => { panel.hidden = true; });
        if (active === 'live') {
            bar.textContent = `${liveName} · Preview`;
            return;
        }
        (panels.get(active) ?? load(active)).hidden = false;
        bar.textContent = collection.tabs.find(t => t.id === active).title;
    }

    // Fetches a saved note into its panel (made the first time). Only the
    // newest fetch for a panel is shown.
    function load(id) {
        let panel = panels.get(id);
        if (!panel) {
            panel = document.createElement('section');
            panel.className = 'ezra-tabpanel';
            panel.id = `panel-${id}`;
            panel.setAttribute('role', 'tabpanel');
            panel.setAttribute('aria-labelledby', `tab-${id}`);
            panel.innerHTML = '<div class="ezra-tab-tools"><button type="button" class="ezra-button">↻ Refresh</button></div><div class="ezra-popout-note"><p class="ezra-meta">Loading…</p></div>';
            panel.querySelector('button').addEventListener('click', () => load(id));
            page.querySelector('.ezra-window-body').append(panel);
            panels.set(id, panel);
        }
        const content = panel.querySelector('.ezra-popout-note');
        const request = panel.request = (panel.request ?? 0) + 1;
        fetch(`/ezra/collection/${id}`)
            .then(r => {
                if (r.status === 404) return '<p class="ezra-meta">This note is gone.</p>';
                // A redirect means the sign-in expired and we got the login page.
                if (!r.ok || r.redirected) throw new Error(r.status);
                return r.text();
            })
            .then(html => {
                if (request !== panel.request) return;
                content.innerHTML = html;
                const title = content.querySelector('[data-note-title]')?.dataset.noteTitle;
                if (title) rename(id, title);
            })
            .catch(() => {
                if (request === panel.request) content.innerHTML = '<p class="ezra-meta">Couldn’t load this note. Are you still signed in?</p>';
            });
        return panel;
    }

    // Keeps a tab's saved title current after the note is renamed.
    function rename(id, title) {
        const saved = collection.tabs.find(t => t.id === id);
        if (!saved || saved.title === title) return;
        saved.title = title;
        saveCollection(collection);
        const tab = strip.querySelector(`[data-note="${id}"]`);
        if (tab) setTabTitle(tab, title);
        if (collection.active === id) bar.textContent = title;
    }

    const keyOf = tab => tab.dataset.note ? Number(tab.dataset.note) : 'live';

    strip.addEventListener('click', e => {
        const tab = e.target.closest('[role="tab"]');
        if (!tab) return;
        if (e.target.closest('.ezra-tab-close')) closeTab(keyOf(tab), false);
        else activate(keyOf(tab), false);
    });

    // Middle-click closes a saved tab (and doesn't start autoscroll).
    strip.addEventListener('mousedown', e => { if (e.button === 1) e.preventDefault(); });
    strip.addEventListener('auxclick', e => {
        const tab = e.target.closest('[data-note]');
        if (tab && e.button === 1) closeTab(keyOf(tab), false);
    });

    // In the strip: arrows, Home and End move between tabs, and Delete
    // closes a saved one. Anywhere: Ctrl+PgUp and Ctrl+PgDn, where the
    // browser lets the page have them.
    strip.addEventListener('keydown', e => {
        const all = keys();
        const i = all.indexOf(collection.active);
        let next;
        if (e.key === 'ArrowRight') next = all[(i + 1) % all.length];
        else if (e.key === 'ArrowLeft') next = all[(i - 1 + all.length) % all.length];
        else if (e.key === 'Home') next = all[0];
        else if (e.key === 'End') next = all[all.length - 1];
        else if (e.key === 'Delete' && collection.active !== 'live') {
            e.preventDefault();
            closeTab(collection.active, true);
            return;
        } else return;
        e.preventDefault();
        activate(next, true);
    });

    document.addEventListener('keydown', e => {
        if (!e.ctrlKey || (e.key !== 'PageDown' && e.key !== 'PageUp')) return;
        const all = keys();
        const i = all.indexOf(collection.active);
        e.preventDefault();
        activate(all[(i + (e.key === 'PageDown' ? 1 : all.length - 1)) % all.length], strip.contains(document.activeElement));
    });

    // Drag a saved tab to reorder it. A gold bar marks where it will land:
    // before the first tab whose middle is right of the pointer, or at the end.
    let dragged = null;
    const clearMarks = () => strip.querySelectorAll('.is-drop-before, .is-drop-after, .is-dragging')
        .forEach(tab => tab.classList.remove('is-drop-before', 'is-drop-after', 'is-dragging'));
    function dropBefore(x) {
        return [...strip.querySelectorAll('[data-note]')].find(tab => {
            const box = tab.getBoundingClientRect();
            return x < box.left + box.width / 2;
        }) ?? null;
    }

    strip.addEventListener('dragstart', e => {
        const tab = e.target.closest('[data-note]');
        if (!tab) return;
        dragged = keyOf(tab);
        e.dataTransfer.effectAllowed = 'move';
        e.dataTransfer.setData('text/plain', String(dragged)); // Firefox won't drag without data.
        tab.classList.add('is-dragging');
    });

    strip.addEventListener('dragover', e => {
        if (dragged === null) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
        strip.querySelectorAll('.is-drop-before, .is-drop-after').forEach(tab => tab.classList.remove('is-drop-before', 'is-drop-after'));
        const before = dropBefore(e.clientX);
        if (before) before.classList.add('is-drop-before');
        else strip.querySelector('[data-note]:last-child')?.classList.add('is-drop-after');
    });

    strip.addEventListener('dragleave', e => {
        if (!strip.contains(e.relatedTarget)) strip.querySelectorAll('.is-drop-before, .is-drop-after').forEach(tab => tab.classList.remove('is-drop-before', 'is-drop-after'));
    });

    strip.addEventListener('drop', e => {
        if (dragged === null) return;
        e.preventDefault();
        // Dropped on its own left half, it lands before the tab after it (so it stays put).
        let before = dropBefore(e.clientX);
        if (before && keyOf(before) === dragged) before = before.nextElementSibling;
        const from = collection.tabs.findIndex(t => t.id === dragged);
        const [moved] = collection.tabs.splice(from, 1);
        const to = before ? collection.tabs.findIndex(t => t.id === keyOf(before)) : -1;
        if (to < 0) collection.tabs.push(moved);
        else collection.tabs.splice(to, 0, moved);
        dragged = null;
        saveCollection(collection);
        render();
    });

    strip.addEventListener('dragend', () => { dragged = null; clearMarks(); });

    closeAll.addEventListener('click', () => {
        const count = collection.tabs.length;
        if (!confirm(`Close all ${count} note${count === 1 ? '' : 's'}? The Live preview tab stays.`)) return;
        collection.tabs = [];
        collection.active = 'live';
        saveCollection(collection);
        render();
    });

    // An add from another window.
    window.addEventListener('storage', e => {
        if (e.key !== CollectionKey && e.key !== null) return;
        collection = loadCollection();
        render();
    });

    // Nothing navigates this window away: Ezra links open in the main window
    // (which comes forward), or in a new tab if it has closed. Other sites
    // open in a new tab.
    page.addEventListener('click', e => {
        const link = e.target.closest('a[href]');
        if (!link || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        e.preventDefault();
        const main = window.opener;
        if (link.origin === location.origin && main && !main.closed) {
            main.location.href = link.href;
            main.focus();
        } else {
            window.open(link.href, '_blank', 'noopener');
        }
    });

    render();

    if (!('BroadcastChannel' in window)) {
        status.textContent = 'This browser can’t keep a separate preview window updated.';
        return;
    }
    const channel = new BroadcastChannel(PopoutChannel);
    channel.onmessage = e => {
        const data = e.data ?? {};
        // A note page loaded (as after a save): refresh its tab if it has one.
        if (typeof data.seen === 'number') {
            if (!hasTab(data.seen)) return;
            if (typeof data.title === 'string') rename(data.seen, data.title);
            if (panels.has(data.seen)) load(data.seen);
            return;
        }
        if (data.closed) {
            status.textContent = 'The editor was closed. This window keeps the last preview.';
            status.hidden = false;
            return;
        }
        if (typeof data.html === 'string') {
            body.innerHTML = data.html;
            status.hidden = true;
        }
        if (typeof data.title === 'string') {
            liveName = data.title.trim() || 'Untitled';
            heading.textContent = liveName;
            if (collection.active === 'live') bar.textContent = `${liveName} · Preview`;
        }
    };
    channel.postMessage({ hello: true });
}

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

// Free text plus chips, for the header search box and the editor's
// references box. As you type, the server suggests tags, scripture and
// Strong's numbers that match the end of the text (NoteSearch.Suggest);
// picking one swaps those words for a chip. Options:
//   kinds        which suggestion kinds to offer (default: all)
//   inputName    the hidden input name for a chip of a given kind
//   enterSubmits whether Enter submits the form when no option is highlighted
function setUpSuggestBox(box, { kinds = null, inputName, enterSubmits }) {
    const chips = box.querySelector('.ezra-chips');
    const input = box.querySelector('[role="combobox"]');
    const list = box.querySelector('[role="listbox"]');
    const classes = { tag: 'ezra-chip', ref: 'ezra-chip ezra-chip-ref', strongs: 'ezra-chip ezra-chip-strongs' };
    let options = [];
    let active = -1;
    let timer;
    let latest = 0;

    const applied = () => [...chips.children].map(li => li.dataset.kind + ':' + li.querySelector('input').value.toLowerCase());

    function fetchOptions() {
        clearTimeout(timer);
        const request = ++latest;
        const text = input.value;
        if (!text.trim()) return show([]);
        fetch(box.dataset.suggestUrl + '?q=' + encodeURIComponent(text))
            .then(r => { if (!r.ok) throw new Error(r.status); return r.json(); })
            .then(found => {
                if (request !== latest) return;
                const have = applied();
                show(found.filter(o => (!kinds || kinds.includes(o.kind)) && !have.includes(o.kind + ':' + o.value.toLowerCase())));
            })
            .catch(() => { if (request === latest) show([]); });
    }

    function show(found) {
        options = found;
        active = -1;
        list.replaceChildren(...options.map((option, i) => {
            const li = document.createElement('li');
            li.id = list.id + '-' + i;
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
        input.setAttribute('aria-activedescendant', list.id + '-' + active);
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
        li.dataset.kind = option.kind;
        const hidden = Object.assign(document.createElement('input'), { type: 'hidden', name: inputName(option.kind), value: option.value });
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
        } else if (e.key === 'Enter') {
            if (isOpen && active >= 0) {
                e.preventDefault();
                pick(active);
            } else if (!enterSubmits) {
                e.preventDefault();
            }
        } else if (e.key === 'Escape' && isOpen) {
            e.preventDefault();
            open(false);
        } else if (e.key === 'Backspace' && input.value === '') {
            chips.lastElementChild?.remove();
        }
    });
}
