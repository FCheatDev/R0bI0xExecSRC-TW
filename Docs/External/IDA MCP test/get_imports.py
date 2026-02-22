import json
import urllib.request

# Try to get entry point and functions
code = """import idaapi
print("Entry point:", hex(idaapi.get_entry(getattr(idaapi, 'get_imagebase', lambda: 0)())))
print("Imagebase:", hex(idaapi.get_imagebase()))
funcs = list(idaapi.Functions())
print("Total functions:", len(funcs))
for f in funcs[:20]:
    print(hex(f), idaapi.get_func_name(f))"""

data = json.dumps({
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
        "name": "py_eval",
        "arguments": {
            "code": code
        }
    },
    "id": 6
}).encode()

req = urllib.request.Request(
    'http://127.0.0.1:13337/mcp',
    data=data,
    headers={'Content-Type': 'application/json'}
)

response = urllib.request.urlopen(req).read().decode()
print(response)
