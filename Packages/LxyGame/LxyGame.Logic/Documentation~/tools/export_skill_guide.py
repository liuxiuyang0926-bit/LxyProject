"""Export the skill authoring manual to an offline HTML document (stdlib only).

This intentionally supports the Markdown and Mermaid subset used by this manual.
Unknown diagram syntax fails the export instead of silently losing connections.
"""
from pathlib import Path
from collections import OrderedDict
import html
import re
import xml.etree.ElementTree as ET

SOURCE = Path(__file__).resolve().parents[1] / 'TurnBasedSkillAuthoringGuide.md'
DESTINATION = SOURCE.with_suffix('.html')


def esc(text):
    return html.escape(str(text), quote=True)


def inline(text):
    value = esc(text)
    value = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', value)
    return re.sub(r'`([^`]+)`', r'<code>\1</code>', value)


def wrap_label(text, limit=16):
    result = []
    for paragraph in text.replace('<br/>', '\n').splitlines():
        line, width = '', 0
        for char in paragraph:
            amount = 1 if ord(char) > 255 else 0.55
            if width + amount > limit and line:
                result.append(line)
                line, width = '', 0
            line += char
            width += amount
        result.append(line)
    return result or ['']


def svg_text(x, y, lines, css='node-text', anchor='middle'):
    spans = ''.join(f'<tspan x="{x}" dy="{0 if i == 0 else 21}">{esc(line)}</tspan>'
                    for i, line in enumerate(lines))
    return f'<text x="{x}" y="{y}" text-anchor="{anchor}" class="{css}">{spans}</text>'


def svg_root(width, height, body, number):
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" '
           f'role="img" aria-labelledby="chart-title-{number}">'
           f'<title id="chart-title-{number}">技能配置流程图 {number}</title>'
           f'<defs><marker id="arrow-{number}" viewBox="0 0 10 10" refX="9" refY="5" '
           'markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
           '<path d="M 0 0 L 10 5 L 0 10 z" fill="#526b89"/></marker></defs>'
           + ''.join(body) + '</svg>')
    ET.fromstring(svg)
    return svg


def flowchart(source, number):
    node = r'([A-Za-z][A-Za-z0-9_]*)(?:\["([^"]+)"\]|\{"([^"]+)"\})?'
    pattern = re.compile(node + r'\s*(-->|-- .*? -->|-\..*?\.->)\s*' + node)
    nodes, edges = OrderedDict(), []
    for line in source.splitlines()[1:]:
        if not line.strip():
            continue
        match = pattern.fullmatch(line.strip())
        if not match:
            raise ValueError('Unsupported flowchart statement: ' + line)
        start, box, decision, arrow, end, target_box, target_decision = match.groups()
        for key, label, diamond in [(start, box, decision), (end, target_box, target_decision)]:
            if key not in nodes:
                nodes[key] = {'label': label or diamond or key, 'decision': bool(diamond)}
            elif label or diamond:
                nodes[key].update(label=label or diamond, decision=bool(diamond))
        label = '' if arrow == '-->' else arrow[2:-3].strip(' ."')
        edges.append((start, end, label, arrow.startswith('-.')))
    order = {key: i for i, key in enumerate(nodes)}
    rank = {key: 0 for key in nodes}
    for key in nodes:
        for start, end, _, _ in edges:
            if start == key and order[end] > order[start]:
                rank[end] = max(rank[end], rank[start] + 1)
    levels = {}
    for key in nodes:
        levels.setdefault(rank[key], []).append(key)
    backward = sum(order[end] <= order[start] for start, end, _, _ in edges)
    rail = 120 + backward * 18
    box_width = 280
    main_width = max(len(keys) for keys in levels.values()) * 350
    total_width = main_width + rail * 2
    top = 30
    for level in sorted(levels):
        keys = levels[level]
        for i, key in enumerate(keys):
            item = nodes[key]
            item['lines'] = wrap_label(item['label'])
            item['height'] = max(78, len(item['lines']) * 21 + 38)
            item['x'] = rail + main_width / 2 + (i - (len(keys) - 1) / 2) * 350
            item['y'] = top
        top += max(nodes[key]['height'] for key in keys) + 85
    body, labels, rail_index = [], [], 0
    outgoing = {}
    for edge in edges:
        outgoing.setdefault(edge[0], []).append(edge)
    for edge in edges:
        start, end, label, dashed = edge
        left, right = nodes[start], nodes[end]
        sx, sy = left['x'], left['y'] + left['height']
        tx, ty = right['x'], right['y']
        if order[end] <= order[start]:
            lane_x = 36 + rail_index * 18
            rail_index += 1
            sx, sy = left['x'] - box_width / 2, left['y'] + left['height'] / 2
            tx, ty = right['x'] - box_width / 2, right['y'] + right['height'] / 2
            path = f'M {sx} {sy} H {lane_x} V {ty} H {tx}'
            if label:
                labels.append(svg_text(sx - 8, sy - 10, wrap_label(label, 10), 'edge-text', 'end'))
        else:
            siblings = outgoing[start]
            offset = (siblings.index(edge) - (len(siblings) - 1) / 2) * 48
            sx += offset
            mid_y = sy + 42
            path = f'M {sx} {sy} V {mid_y} H {tx} V {ty - 3}'
            if label:
                labels.append(svg_text((sx + tx) / 2 + 5, mid_y - 10,
                                       wrap_label(label, 13), 'edge-text'))
        dash = ' stroke-dasharray="6 5"' if dashed else ''
        body.append(f'<path d="{path}" fill="none" stroke="#526b89" stroke-width="2"{dash} '
                    f'marker-end="url(#arrow-{number})"/>')
    body.extend(labels)
    for key, item in nodes.items():
        x, y, height = item['x'], item['y'], item['height']
        if item['decision']:
            shape = (f'<path d="M {x - box_width/2 + 22} {y} H {x + box_width/2 - 22} '
                     f'L {x + box_width/2} {y + height/2} L {x + box_width/2 - 22} {y + height} '
                     f'H {x - box_width/2 + 22} L {x - box_width/2} {y + height/2} Z" '
                     'fill="#fff4d6" stroke="#bf9227" stroke-width="1.5"/>')
        else:
            fill, stroke = ('#e8f5ef', '#469878') if key not in outgoing else ('#edf4ff', '#7193c2')
            shape = (f'<rect x="{x - box_width/2}" y="{y}" width="{box_width}" height="{height}" '
                     f'rx="12" fill="{fill}" stroke="{stroke}" stroke-width="1.5"/>')
        body.append(shape)
        text_y = y + height / 2 - (len(item['lines']) - 1) * 10.5 + 5
        body.append(svg_text(x, text_y, item['lines']))
    return svg_root(total_width, top - 40, body, number)


def sequence(source, number):
    participants, messages = OrderedDict(), []
    for line in source.splitlines()[1:]:
        line = line.strip()
        if not line:
            continue
        match = re.fullmatch(r'participant (\w+) as (.+)', line)
        if match:
            participants[match[1]] = match[2]
            continue
        match = re.fullmatch(r'(\w+)(-->>|->>)(\w+): (.+)', line)
        if not match:
            raise ValueError('Unsupported sequence statement: ' + line)
        messages.append(match.groups())
    width, height = 1080, 140 + len(messages) * 104
    positions = {key: 120 + i * 275 for i, key in enumerate(participants)}
    body = []
    for key, label in participants.items():
        x = positions[key]
        body.append(f'<rect x="{x-102}" y="20" width="204" height="52" rx="10" fill="#edf4ff" stroke="#7193c2"/>')
        body.append(svg_text(x, 52, [label]))
        body.append(f'<path d="M {x} 72 V {height-20}" stroke="#90a2b6" stroke-dasharray="5 5"/>')
    for index, (start, arrow, end, label) in enumerate(messages):
        sx, tx, y = positions[start], positions[end], 130 + index * 104
        dash = ' stroke-dasharray="6 5"' if arrow == '-->>' else ''
        if sx == tx:
            path = f'M {sx} {y} h 72 v 35 h -72'
            body.append(svg_text(sx - 12, y - 10, wrap_label(label, 13), 'edge-text', 'end'))
        else:
            path = f'M {sx} {y} H {tx}'
            body.append(svg_text((sx+tx)/2, y-24, wrap_label(label, max(10, abs(tx-sx)/18)), 'edge-text'))
        body.append(f'<path d="{path}" fill="none" stroke="#526b89" stroke-width="2"{dash} marker-end="url(#arrow-{number})"/>')
    return svg_root(width, height, body, number)


def render(markdown):
    lines, body, navigation = markdown.splitlines(), [], []
    index, heading_number, chart_number = 0, 0, 0
    while index < len(lines):
        line = lines[index]
        if not line.strip():
            index += 1
            continue
        if line.startswith('~~~'):
            language, content = line[3:], []
            index += 1
            while index < len(lines) and lines[index] != '~~~':
                content.append(lines[index])
                index += 1
            if index == len(lines):
                raise ValueError('Unclosed Markdown fence')
            source = '\n'.join(content)
            if language == 'mermaid':
                chart_number += 1
                svg = sequence(source, chart_number) if source.startswith('sequenceDiagram') else flowchart(source, chart_number)
                body.append(f'<figure class="diagram"><figcaption>流程图 {chart_number}</figcaption>{svg}</figure>')
                body.append('<details><summary>查看可编辑流程图源文</summary><pre><code>' + esc(source) + '</code></pre></details>')
            else:
                body.append('<pre><code>' + esc(source) + '</code></pre>')
            index += 1
            continue
        if line.startswith('|'):
            rows = []
            while index < len(lines) and lines[index].startswith('|'):
                cells = [cell.strip() for cell in lines[index].strip().strip('|').split('|')]
                if not all(re.fullmatch(r':?-+:?', cell) for cell in cells):
                    rows.append(cells)
                index += 1
            table = '<div class="table-wrap"><table><thead><tr>'
            table += ''.join('<th>' + inline(cell) + '</th>' for cell in rows[0]) + '</tr></thead><tbody>'
            for row in rows[1:]:
                table += '<tr>' + ''.join('<td>' + inline(cell) + '</td>' for cell in row) + '</tr>'
            body.append(table + '</tbody></table></div>')
            continue
        heading = re.match(r'^(#{1,6}) (.+)$', line)
        if heading:
            level, title = len(heading[1]), heading[2]
            heading_number += 1
            anchor = f'section-{heading_number}'
            body.append(f'<h{level} id="{anchor}">{inline(title)}</h{level}>')
            if level == 2:
                navigation.append(f'<a href="#{anchor}">{esc(title)}</a>')
        elif line == '---':
            body.append('<hr>')
        elif line.startswith('> '):
            body.append('<blockquote>' + inline(line[2:]) + '</blockquote>')
        elif re.match(r'^(?:- |\d+\. )', line):
            ordered = not line.startswith('- ')
            tag = 'ol' if ordered else 'ul'
            items = []
            while index < len(lines) and re.match(r'^\d+\. ' if ordered else r'^- ', lines[index]):
                text = re.sub(r'^(?:- |\d+\. )', '', lines[index])
                if text.startswith('[ ] '):
                    text = '☐ ' + text[4:]
                items.append('<li>' + inline(text) + '</li>')
                index += 1
            body.append(f'<{tag}>' + ''.join(items) + f'</{tag}>')
            continue
        else:
            body.append('<p>' + inline(line) + '</p>')
        index += 1
    return '\n'.join(body), '\n'.join(navigation), chart_number


STYLE = '''
:root{color-scheme:light;--ink:#20344b;--blue:#205da2;--border:#d8e2ed}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#f4f7fb;color:var(--ink);font:16px/1.85 "Microsoft YaHei","PingFang SC",system-ui,sans-serif}
aside{position:fixed;inset:0 auto 0 0;width:250px;padding:26px 20px;background:#112c4b;color:white;overflow:auto}
aside strong{display:block;font-size:20px;margin-bottom:20px}aside a{display:block;color:#dceafa;text-decoration:none;margin:9px 0;font-size:14px;line-height:1.55}aside a:hover{color:white;text-decoration:underline}
main{max-width:1250px;margin:0 auto 0 250px;padding:38px 50px;background:white}h1{font-size:30px;line-height:1.45}h2{font-size:24px;border-left:5px solid #3983ca;padding-left:14px;margin-top:50px}h3{font-size:20px;margin-top:32px}h4{font-size:18px}
p{margin:14px 0}strong{color:#123c68}blockquote{margin:8px 0;padding:9px 15px;border-left:3px solid #a4bbd6;background:#f3f7fc}li{margin:6px 0}hr{border:0;border-top:1px solid var(--border);margin:34px 0}
table{border-collapse:collapse;width:100%;font-size:14px;line-height:1.65}th,td{border:1px solid var(--border);padding:10px 12px;text-align:left;vertical-align:top}th{background:#eaf1f9;color:#184c7e}tbody tr:nth-child(even){background:#f8fafc}.table-wrap{overflow-x:auto;margin:20px 0}
pre{background:#f1f5f9;border:1px solid var(--border);padding:18px;border-radius:8px;overflow-x:auto;line-height:1.75;font-size:14px}code{font-family:Consolas,"Microsoft YaHei",monospace}
.diagram{margin:25px 0;border:1px solid var(--border);border-radius:12px;background:#fcfdff;padding:18px;overflow:auto}.diagram svg{display:block;min-width:580px;width:100%;height:auto}.diagram figcaption{font-size:13px;color:#607995;margin-bottom:8px}
.node-text{font:16px "Microsoft YaHei",sans-serif;fill:#193958}.edge-text{font:14px "Microsoft YaHei",sans-serif;fill:#385a7c;paint-order:stroke;stroke:#fcfdff;stroke-width:5px;stroke-linejoin:round}
details{font-size:13px;color:#536d87;margin-bottom:25px}summary{cursor:pointer}button{border:1px solid #91b5dd;border-radius:6px;background:#eef5ff;color:#174e86;padding:9px 16px;cursor:pointer;font-size:14px}.toolbar{display:flex;gap:15px;align-items:center;font-size:13px;color:#526d87}
@media(max-width:1000px){aside{position:static;width:auto;max-height:260px}main{margin:0;padding:24px}h1{font-size:25px}}
@media print{body{background:white;font-size:11pt}aside,.toolbar,details{display:none}main{margin:0;padding:0;max-width:none}h2,h3{break-after:avoid}tr{break-inside:avoid}thead{display:table-header-group}.table-wrap{overflow:visible}pre{white-space:pre-wrap;overflow:visible}.diagram{break-inside:avoid;padding:5px}.diagram svg{min-width:0;max-height:250mm}a{color:inherit;text-decoration:none}}
'''


def main():
    source = SOURCE.read_text(encoding='utf-8')
    body, navigation, charts = render(source)
    document = ('<!doctype html><html lang="zh-CN"><head><meta charset="utf-8">'
                '<meta name="viewport" content="width=device-width,initial-scale=1">'
                '<title>技能逻辑与技能表现 · 新手策划完整手册</title><style>' + STYLE + '</style></head><body>'
                '<aside><strong>技能制作手册</strong>' + navigation + '</aside><main>'
                '<div class="toolbar"><button onclick="window.print()">打印 / 保存为 PDF</button>'
                f'<span>离线阅读 · {charts} 张流程图 · 2026-09-20</span></div>'
                + body + '</main></body></html>')
    DESTINATION.write_text(document, encoding='utf-8')
    print(f'Exported {charts} diagrams: {DESTINATION} ({len(document.encode("utf-8"))} bytes)')


if __name__ == '__main__':
    main()
