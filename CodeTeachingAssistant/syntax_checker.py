import sys

def check_syntax(code_path):
    try:
        with open(code_path, "r", encoding="utf-8") as f:
            code = f.read()
        compile(code, "<string>", "exec")
        print("Sözdizimi geçerli.")
    except SyntaxError as e:
        print(f"Sözdizimi Hatası: Satır {e.lineno} → {e.msg}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Kod dosyası belirtilmedi.")
    else:
        check_syntax(sys.argv[1])