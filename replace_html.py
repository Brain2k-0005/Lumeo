import re, os, glob, sys

DOCS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "docs", "Lumeo.Docs")
SKIP_FILES = {"App.razor", "_Imports.razor"}

def has_razor(s):
    """Check if text contains Razor expressions that make it unsafe for component attributes."""
    if "@(" in s or "@_" in s or "@{" in s:
        return True
    # Check for @variable patterns (e.g. @cols, @album.Color, @NavLinkClass)
    # But not @onclick, @bind, @attributes etc which are Blazor directives
    if re.search(r'class="[^"]*@[a-zA-Z]', s):
        return True
    return False

def find_code_block_start(content):
    """Find the position where @code { starts. Returns len(content) if not found."""
    m = re.search(r"@code\s*\{", content)
    if m:
        return m.start()
    return len(content)

def process_file(filepath):
    bn = os.path.basename(filepath)
    if bn in SKIP_FILES:
        return False
    try:
        with open(filepath, "r", encoding="utf-8-sig") as f:
            content = f.read()
    except:
        return False
    original = content

    # Find where @code block starts - only apply replacements before it
    code_start = find_code_block_start(content)
    markup = content[:code_start]
    code_section = content[code_start:]

    all_events = []
    tag_types = ["h1","h2","h3","h4","h5","h6","p","span","label","a","button","div"]
    for tt in tag_types:
        for m in re.finditer(rf"<{tt}(\s[^>]*>|>)", markup, re.DOTALL):
            all_events.append((m.start(), m.end(), "open", tt, m.group(0)))
        for m in re.finditer(rf"</{tt}>", markup):
            all_events.append((m.start(), m.end(), "close", tt, m.group(0)))

    # Pre-existing <Link> tags (from previous conversions)
    for m in re.finditer(r"<Link\s[^>]*>", markup, re.DOTALL):
        all_events.append((m.start(), m.end(), "open", "Link", m.group(0)))
    for m in re.finditer(r"</Link>", markup):
        all_events.append((m.start(), m.end(), "close", "Link", m.group(0)))

    all_events.sort(key=lambda x: x[0])

    stacks = {tt: [] for tt in tag_types}
    stacks["Link"] = []
    replacement_map = {}

    for start, end, event_type, tag_type, tag_text in all_events:
        if event_type == "open":
            converted_to = tag_type

            if tag_type in ("h1","h2","h3","h4","h5","h6"):
                if has_razor(tag_text) or re.search(r'style="[^"]*@', tag_text):
                    pass  # keep as-is
                else:
                    level = tag_type[1]
                    class_m = re.search(r'class="([^"]*)"', tag_text)
                    if class_m:
                        cls = class_m.group(1)
                        rest = re.sub(r'\s*class="[^"]*"', '', tag_text[len(f"<{tag_type}"):])
                        replacement_map[(start, end)] = f'<Heading Level="{level}" Class="{cls}"{rest}'
                    else:
                        replacement_map[(start, end)] = f'<Heading Level="{level}">'
                    converted_to = "Heading"

            elif tag_type == "p":
                if has_razor(tag_text) or re.search(r'style="[^"]*@', tag_text):
                    converted_to = "p"
                else:
                    class_m = re.search(r'class="([^"]*)"', tag_text)
                    if class_m:
                        cls = class_m.group(1)
                        rest = re.sub(r'\s*class="[^"]*"', '', tag_text[2:])
                        replacement_map[(start, end)] = f'<Lumeo.Text Class="{cls}"{rest}'
                    else:
                        replacement_map[(start, end)] = "<Lumeo.Text>"
                    converted_to = "Lumeo.Text"

            elif tag_type == "span":
                if has_razor(tag_text) or re.search(r'style="[^"]*@', tag_text):
                    converted_to = "span"
                else:
                    class_m = re.search(r'class="([^"]*)"', tag_text)
                    if class_m:
                        cls = class_m.group(1)
                        rest = re.sub(r'\s*class="[^"]*"', '', tag_text[5:])
                        replacement_map[(start, end)] = f'<Lumeo.Text As="span" Class="{cls}"{rest}'
                        converted_to = "Lumeo.Text"
                    elif tag_text == "<span>":
                        replacement_map[(start, end)] = '<Lumeo.Text As="span">'
                        converted_to = "Lumeo.Text"
                    else:
                        converted_to = "span"

            elif tag_type == "label":
                if has_razor(tag_text):
                    converted_to = "label"
                else:
                    class_m = re.search(r'class="([^"]*)"', tag_text)
                    if class_m:
                        cls = class_m.group(1)
                        rest = re.sub(r'\s*class="[^"]*"', '', tag_text[6:])
                        replacement_map[(start, end)] = f'<Label Class="{cls}"{rest}'
                        converted_to = "Label"
                    elif tag_text == "<label>":
                        replacement_map[(start, end)] = "<Label>"
                        converted_to = "Label"
                    else:
                        converted_to = "label"

            elif tag_type == "a":
                if has_razor(tag_text):
                    converted_to = "a"
                else:
                    href_m = re.search(r'href="([^"]*)"', tag_text)
                    if href_m:
                        href = href_m.group(1)
                        if "@" in href:
                            converted_to = "a"
                        else:
                            class_m = re.search(r'class="([^"]*)"', tag_text)
                            if class_m:
                                cls = class_m.group(1)
                                replacement_map[(start, end)] = f'<Lumeo.Link Href="{href}" Class="{cls}">'
                                converted_to = "Lumeo.Link"
                            else:
                                stripped = re.sub(r'\s*href="[^"]*"', '', tag_text)
                                stripped = stripped.replace("<a", "").replace(">", "").strip()
                                if stripped:
                                    converted_to = "a"
                                else:
                                    replacement_map[(start, end)] = f'<Lumeo.Link Href="{href}">'
                                    converted_to = "Lumeo.Link"
                    else:
                        converted_to = "a"

            elif tag_type == "button":
                if has_razor(tag_text):
                    converted_to = "button"
                else:
                    new_tag = tag_text.replace("<button", "<Button", 1)
                    new_tag = new_tag.replace("@onclick=", "OnClick=")
                    new_tag = re.sub(r'\sclass=', ' Class=', new_tag)
                    replacement_map[(start, end)] = new_tag
                    converted_to = "Button"

            elif tag_type == "div":
                if has_razor(tag_text) or re.search(r'style="[^"]*@', tag_text):
                    converted_to = "div"
                else:
                    class_m = re.search(r'class="([^"]*)"', tag_text)
                    if class_m:
                        classes = class_m.group(1).split()
                        if any(c.startswith("space-y-") for c in classes):
                            gap = next(c[8:] for c in classes if c.startswith("space-y-"))
                            remaining = " ".join(c for c in classes if not c.startswith("space-y-"))
                            if remaining:
                                replacement_map[(start, end)] = f'<Stack Gap="{gap}" Class="{remaining}">'
                            else:
                                replacement_map[(start, end)] = f'<Stack Gap="{gap}">'
                            converted_to = "Stack"
                        elif "flex" in classes:
                            remaining = " ".join(c for c in classes if c != "flex")
                            if remaining:
                                replacement_map[(start, end)] = f'<Flex Class="{remaining}">'
                            else:
                                replacement_map[(start, end)] = "<Flex>"
                            converted_to = "Flex"
                        elif "grid" in classes:
                            remaining = " ".join(c for c in classes if c != "grid")
                            if remaining:
                                replacement_map[(start, end)] = f'<Grid Class="{remaining}">'
                            else:
                                replacement_map[(start, end)] = "<Grid>"
                            converted_to = "Grid"
                        elif "inline-flex" in classes:
                            remaining = " ".join(c for c in classes if c != "inline-flex")
                            if remaining:
                                replacement_map[(start, end)] = f'<Flex Class="inline-flex {remaining}">'
                            else:
                                replacement_map[(start, end)] = '<Flex Class="inline-flex">'
                            converted_to = "Flex"
                        else:
                            converted_to = "div"
                    elif tag_text.strip() == "<div>":
                        replacement_map[(start, end)] = "<Stack>"
                        converted_to = "Stack"
                    else:
                        converted_to = "div"

            elif tag_type == "Link":
                new_tag = tag_text.replace("<Link", "<Lumeo.Link", 1)
                replacement_map[(start, end)] = new_tag
                converted_to = "Lumeo.Link"

            stacks[tag_type].append(converted_to)

        elif event_type == "close":
            st = tag_type
            if st in stacks and stacks[st]:
                repl = stacks[st].pop()
                if repl != st:
                    replacement_map[(start, end)] = f"</{repl}>"

    if not replacement_map:
        return False

    # Apply replacements in reverse order to preserve positions (only in markup section)
    sorted_repls = sorted(replacement_map.items(), key=lambda x: x[0][0], reverse=True)
    for (s, e), repl_text in sorted_repls:
        markup = markup[:s] + repl_text + markup[e:]

    content = markup + code_section

    if content != original:
        try:
            with open(filepath, "w", encoding="utf-8", newline="\n") as f:
                f.write(content)
            return True
        except:
            return False
    return False

def main():
    pattern = os.path.join(DOCS_DIR, "**", "*.razor")
    files = glob.glob(pattern, recursive=True)
    modified = 0
    for fp in sorted(files):
        if process_file(fp):
            modified += 1
            print(f"  Modified: {os.path.basename(fp)}")
    print(f"\nTotal: {modified} files modified out of {len(files)}")

if __name__ == "__main__":
    main()
