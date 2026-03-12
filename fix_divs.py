#!/usr/bin/env python3
"""
Convert remaining <div> tags to <Stack> in Blazor doc pages.
Skips content inside verbatim strings in @code blocks.
Converts <code> to <Code> where found.
Does NOT touch table/thead/tbody/tr/th/td.
"""

import re
import os
import glob


def find_code_block(content):
    """Find the @code { ... } block. Returns (start, end) indices or None.
    The code block starts at '@code {' and ends at the matching '}'.
    """
    match = re.search(r'@code\s*\{', content)
    if not match:
        return None

    start = match.start()
    # Find matching closing brace
    brace_count = 1
    i = match.end()
    in_string = False
    in_verbatim = False
    in_char = False

    while i < len(content) and brace_count > 0:
        ch = content[i]

        if in_verbatim:
            if ch == '"':
                if i + 1 < len(content) and content[i + 1] == '"':
                    i += 2  # skip ""
                    continue
                else:
                    in_verbatim = False
            i += 1
            continue

        if in_string:
            if ch == '\\':
                i += 2  # skip escaped char
                continue
            if ch == '"':
                in_string = False
            i += 1
            continue

        if in_char:
            if ch == '\\':
                i += 2
                continue
            if ch == "'":
                in_char = False
            i += 1
            continue

        if ch == '@' and i + 1 < len(content) and content[i + 1] == '"':
            in_verbatim = True
            i += 2
            continue

        if ch == '"':
            in_string = True
            i += 1
            continue

        if ch == "'":
            in_char = True
            i += 1
            continue

        if ch == '{':
            brace_count += 1
        elif ch == '}':
            brace_count -= 1

        i += 1

    return (start, i)


def convert_div_to_stack(markup):
    """Convert <div ...> to <Stack ...> and </div> to </Stack> in markup."""

    def replace_opening_div(match):
        attrs = match.group(1) or ""
        attrs = attrs.strip()

        if not attrs:
            return "<Stack>"

        # Check for class="space-y-N" pattern
        space_match = re.match(r'^class="space-y-(\d+)"$', attrs)
        if space_match:
            return f'<Stack Gap="{space_match.group(1)}">'

        # For class="..." just pass through as Stack Class="..."
        class_match = re.match(r'^class="([^"]*)"$', attrs)
        if class_match:
            return f'<Stack Class="{class_match.group(1)}">'

        # For other attributes, pass through
        return f"<Stack {attrs}>"

    result = re.sub(r'<div\b([^>]*)>', replace_opening_div, markup)
    result = result.replace('</div>', '</Stack>')

    return result


def convert_code_tags(markup):
    """Convert <code ...> to <Code ...> and </code> to </Code>."""
    markup = re.sub(r'<code\b', '<Code', markup)
    markup = markup.replace('</code>', '</Code>')
    return markup


def process_file(filepath):
    """Process a single .razor file."""
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Split content into markup part and code block part
    code_range = find_code_block(content)

    if code_range:
        markup = content[:code_range[0]]
        code_block = content[code_range[0]:code_range[1]]
        after_code = content[code_range[1]:]
    else:
        markup = content
        code_block = ""
        after_code = ""

    # Convert divs and code tags only in the markup part
    new_markup = convert_div_to_stack(markup)
    new_markup = convert_code_tags(new_markup)

    # Also convert divs in the after-code section (API reference tables etc.)
    new_after = convert_div_to_stack(after_code)
    new_after = convert_code_tags(new_after)

    new_content = new_markup + code_block + new_after

    if new_content != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        return True
    return False


def main():
    pages_dir = 'docs/Lumeo.Docs/Pages/Components'
    charts_dir = 'docs/Lumeo.Docs/Pages/Components/Charts'

    files = glob.glob(os.path.join(pages_dir, '*.razor'))
    files += glob.glob(os.path.join(charts_dir, '*.razor'))

    # Filter to A-D pages + all charts
    target_files = []
    for f in files:
        basename = os.path.basename(f)
        if '/Charts/' in f.replace(os.sep, '/') or basename[0] in 'ABCD':
            target_files.append(f)

    # Add Home.razor
    target_files.append('docs/Lumeo.Docs/Pages/Home.razor')

    updated = 0
    for fpath in sorted(target_files):
        if process_file(fpath):
            print(f"  Updated: {os.path.basename(fpath)}")
            updated += 1

    print(f"\nTotal files updated: {updated}")


if __name__ == '__main__':
    main()
