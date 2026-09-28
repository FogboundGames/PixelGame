import re

with open('Assets/Scenes/Gemi.unity', 'r', encoding='utf-8') as f:
    text = f.read()

docs = text.split('\n--- !u!')
doc_map = {}
for doc in docs:
    fl = doc.splitlines()[0]
    m = re.search(r'(\d+)\s+&(\d+)', fl)
    if m:
        doc_map[m.group(2)] = (m.group(1), doc)

for fid, (cid, doc) in doc_map.items():
    if 'm_Text:' in doc:
        txt = re.search(r'm_Text: ([^\n\r]+)', doc).group(1)
        goid = re.search(r'm_GameObject: \{fileID: (\d+)\}', doc).group(1)
        gdoc = doc_map.get(goid, (None, ''))[1]
        gn = re.search(r'm_Name: ([^\n\r]+)', gdoc).group(1) if 'm_Name:' in gdoc else '?'
        print(f"Text doc {fid} on GO {goid} ({gn}): '{txt}'")
