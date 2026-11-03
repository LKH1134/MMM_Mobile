# Unity 씬(.unity YAML)에서 게임오브젝트 하나를 자식·컴포넌트째 지운다. 사용: python remove_scene_object.py <씬> <GameObject fileID>
# 부모 RectTransform/Transform의 m_Children에서도 뺀다. 지운 것을 다른 곳이 가리키면 멈춘다
import re, sys

path, root = sys.argv[1], sys.argv[2]
raw = open(path, 'rb').read()
crlf = b'\r\n' in raw
text = raw.decode('utf-8').replace('\r\n', '\n')
head, *docs = re.split(r'(?m)^(?=--- !u!)', text)
by_id = {}
for d in docs:
    m = re.match(r'--- !u!\d+ &(\d+)', d)
    by_id[m.group(1)] = d

gone = set()


def remove_go(go):
    gone.add(go)
    for comp in re.findall(r'- component: \{fileID: (\d+)\}', by_id[go]):
        gone.add(comp)
        for child in re.findall(r'- \{fileID: (\d+)\}', by_id[comp].split('m_Children:', 1)[1].split('m_Father', 1)[0]) if 'm_Children:' in by_id[comp] else []:
            remove_go(re.search(r'm_GameObject: \{fileID: (\d+)\}', by_id[child]).group(1))


remove_go(root)
transform = next(c for c in re.findall(r'- component: \{fileID: (\d+)\}', by_id[root]) if 'm_Father' in by_id[c])
father = re.search(r'm_Father: \{fileID: (\d+)\}', by_id[transform]).group(1)
keep = [d for d in docs if re.match(r'--- !u!\d+ &(\d+)', d).group(1) not in gone]
out = []
for d in keep:
    if d.startswith('--- !u!') and re.match(r'--- !u!\d+ &%s\b' % father, d):
        d2 = d.replace(f'  - {{fileID: {transform}}}\n', '', 1)
        assert d2 != d, '부모 m_Children에 없다'
        d = d2
    out.append(d)
body = head + ''.join(out)
for g in gone:
    assert f'fileID: {g}}}' not in body, ('아직 가리키는 곳', g)
if crlf: body = body.replace('\n', '\r\n')
open(path, 'wb').write(body.encode('utf-8'))
print('removed', len(gone), 'objects/components under', root)
