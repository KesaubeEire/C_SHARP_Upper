import base64, os, sys

# The HTML content will be piped via stdin as base64
b64_data = sys.stdin.read().strip()
html = base64.b64decode(b64_data).decode('utf-8')

out_path = 'C:/KesaData/Projects/Claude_msi2020/C_SHARP_Upper/Project/Wpf.Ui.Gallery.Kesa/docs/S71200_Reference.html'
os.makedirs(os.path.dirname(out_path), exist_ok=True)
with open(out_path, 'w', encoding='utf-8') as f:
    f.write(html)
print('Written to', out_path)
