import binascii

# Read the file
with open(r'C:\Users\admin\AppData\Local\Fishstrap\Versions\version-df7528517c6849f7\loacl.exe', 'rb') as f:
    data = f.read()

print('File size:', len(data))
print()

# Check for MZ header (DOS exe)
print('=== DOS Header (MZ) ===')
print('First bytes:', data[:2].decode('latin-1'))
print('Offset 0x3C (PE offset pointer):', int.from_bytes(data[0x3C:0x40], 'little'))
print()

# Get PE offset
pe_offset = int.from_bytes(data[0x3C:0x40], 'little')
print('=== PE Header ===')
print('PE signature at offset:', hex(pe_offset))
print('PE signature:', data[pe_offset:pe_offset+4])

# COFF Header
coff_offset = pe_offset + 4
machine = int.from_bytes(data[coff_offset:coff_offset+2], 'little')
num_sections = int.from_bytes(data[coff_offset+2:coff_offset+4], 'little')
optional_size = int.from_bytes(data[coff_offset+16:coff_offset+18], 'little')

print()
print('=== COFF Header ===')
print('Machine:', hex(machine), '(0x14c = i386, 0x8664 = AMD64)')
print('Number of Sections:', num_sections)
print('Optional Header Size:', optional_size)

# Optional Header
opt_offset = coff_offset + 20
magic = int.from_bytes(data[opt_offset:opt_offset+2], 'little')
print()
print('=== Optional Header ===')
print('Magic:', hex(magic), '(0x10b = PE32, 0x20b = PE32+)')

# Entry point
entry_point = int.from_bytes(data[opt_offset+16:opt_offset+20], 'little')
print('Entry Point RVA:', hex(entry_point))

# Image base
image_base = int.from_bytes(data[opt_offset+24:opt_offset+28], 'little')
print('Image Base:', hex(image_base))

# Section headers
print()
print('=== Section Headers ===')
section_offset = opt_offset + optional_size

for i in range(num_sections):
    sec_start = section_offset + (i * 40)
    name = data[sec_start:sec_start+8].decode('latin-1').rstrip('\x00')
    virtual_size = int.from_bytes(data[sec_start+8:sec_start+12], 'little')
    virtual_addr = int.from_bytes(data[sec_start+12:sec_start+16], 'little')
    raw_size = int.from_bytes(data[sec_start+16:sec_start+20], 'little')
    raw_offset = int.from_bytes(data[sec_start+20:sec_start+24], 'little')
    
    print(f'Section {i+1}: {name}')
    print(f'  Virtual Size: {hex(virtual_size)}')
    print(f'  Virtual Address: {hex(virtual_addr)}')
    print(f'  Raw Size: {hex(raw_size)}')
    print(f'  Raw Offset: {hex(raw_offset)}')
    print()
