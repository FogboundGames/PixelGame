import re

with open('Assets/Scenes/Gemi.unity', 'r', encoding='utf-8') as f:
    text = f.read()

target = "m_Name: '[ShipQueuePool]'"
pos = text.find(target)
if pos != -1:
    print(text[pos-200:pos+1500])
else:
    print("Not found")
