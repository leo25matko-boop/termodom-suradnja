"""Prepare a separate copy of the verified m.php UI template for upload."""

import argparse
from pathlib import Path
import re


BRIDGE = """window.termoDomHouseBridge = {
    refresh: () => { loadData(); loadPumps(); },
    publish(kind, payload) {
        const detail = { payload, fetchedAt: Date.now() };
        this[kind] = detail;
        this[kind + 'Error'] = false;
        document.dispatchEvent(new CustomEvent('termodom:' + kind, { detail }));
    },
    fail(kind) {
        this[kind + 'Error'] = true;
        document.dispatchEvent(new Event('termodom:' + kind + '-error'));
    }
};"""


def replace_once(source, anchor, replacement):
    if source.count(anchor) != 1:
        raise ValueError(f"Predložak se razlikuje od provjerene stranice. Očekivana oznaka: {anchor}")
    return source.replace(anchor, replacement, 1)


def patch(source):
    if "window.termoDomHouseBridge" in source:
        raise ValueError("Presjek je već umetnut u ovu kopiju.")
    # Limit replacements to the HTML response. The PHP action handlers and
    # any server-side includes preceding <!doctype html> are kept verbatim.
    document = re.search(r"<!doctype\s+html\b", source, re.IGNORECASE)
    if not document:
        raise ValueError("Kopija m.php ne sadrži očekivani HTML predložak.")
    backend, html = source[:document.start()], source[document.start():]
    if '<canvas id="roomChart">' not in html or '<canvas id="temperatureChart">' not in html:
        raise ValueError("Nedostaju izvorni grafikoni. Potrebna je ručna prilagodba predloška.")
    newline = "\r\n" if "\r\n" in html else "\n"
    head = [
        '<link rel="stylesheet" href="./m-house.css">',
        '<script type="module" src="./m-house.js"></script>',
    ]
    html = replace_once(html, "</head>", newline.join([*head, "</head>"]))
    # Existing polling remains intact. Both the new view and original history
    # use the same successful requests, with independent error handling.
    changes = [
        ("$('refresh').addEventListener('click', loadData);",
         BRIDGE.replace("\n", newline) + newline + "$('refresh').addEventListener('click', loadData);"),
        ("renderPumpCards(data.pumps || []);",
         "window.termoDomHouseBridge.publish('pumps', data);" + newline + "        renderPumpCards(data.pumps || []);"),
        ("renderCards(data.latest || []);",
         "window.termoDomHouseBridge.publish('measurements', data);" + newline + "        renderCards(data.latest || []);"),
        ("$('pump-message').className = 'message error';",
         "window.termoDomHouseBridge.fail('pumps');" + newline + "        $('pump-message').className = 'message error';"),
        ("$('message').className = 'message error';",
         "window.termoDomHouseBridge.fail('measurements');" + newline + "        $('message').className = 'message error';"),
        ('<h2 class="section-title">Sobni termostati</h2>',
         '<h2 class="section-title td-old-thermostat-title">Sobni termostati</h2>'),
        ('<meta http-equiv="refresh" content="150">', ""),
        ('<meta name="theme-color" content="#9933cc">', '<meta name="theme-color" content="#101c24">'),
    ]
    for anchor, replacement in changes:
        html = replace_once(html, anchor, replacement)
    return backend + html


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Lokalna kopija izvornog m.php")
    parser.add_argument("output", type=Path, help="Nova kopija m.php za pregled prije uploada")
    args = parser.parse_args()
    try:
        original = args.source.read_bytes()
        patched = patch(original.decode("utf-8")).encode("utf-8")
        with args.output.open("xb") as output:
            output.write(patched)
    except (OSError, UnicodeError, ValueError) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
