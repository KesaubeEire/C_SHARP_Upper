import sys, re, html

raw = sys.stdin.read()

# Remove scripts and styles
clean = re.sub(r'<script[^>]*>.*?</script>', '', raw, flags=re.DOTALL | re.IGNORECASE)
clean = re.sub(r'<style[^>]*>.*?</style>', '', clean, flags=re.DOTALL | re.IGNORECASE)

# Extract text
text = re.sub(r'<br\s*/?>', '\n', clean)
text = re.sub(r'</(p|div|h[1-6]|li|tr|pre|code|article|section)>', '\n', text)
text = re.sub(r'<[^>]+>', '', text)
text = html.unescape(text)

lines = [l.strip() for l in text.split('\n') if l.strip()]
print('\n'.join(lines[:600]))
