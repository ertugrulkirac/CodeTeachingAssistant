import sys
import pycodestyle
import re
from collections import defaultdict

history_file = "error_history.txt"

# 🔍 Öneri tablosu (PEP8 + Syntax + Common Errors)
def get_fix_suggestion(error_msg):
    suggestions = [
        ("expected ':'", "Öneri: `:` eksik olabilir. Örnek: `for i in range(5):`"),
        ("unexpected indent", "Öneri: Satır başında fazladan boşluk olabilir. Girintileri kontrol et."),
        ("unindent does not match", "Öneri: Girintiler karışmış olabilir. Tab ve boşlukları karıştırma."),
        ("EOL while scanning string literal", "Öneri: Tırnak açıldı ama kapanmadı. Örn: `print(\"Merhaba\")`"),
        ("is not defined", "Öneri: Tanımsız değişken kullanımı olabilir."),
        ("E113", "Öneri: Satır yanlış girintili. Bir blok içinde değilse baştaki boşlukları kaldır.")
    ]

    for key, suggestion in suggestions:
        if key in error_msg:
            return suggestion
    return ""

# 🧠 Temel Python yapılarının açıklamaları
def explain_keywords(code):
    keywords = {
        "for": "`for`: Bir döngü yapısıdır, örn: `for i in range(5):`",
        "if": "`if`: Koşullu ifade, örn: `if x > 0:`",
        "print": "`print`: Ekrana çıktı verir, örn: `print(\"Merhaba\")`",
        "def": "`def`: Fonksiyon tanımı başlatır, örn: `def fonk():`",
        "while": "`while`: Koşul sağlandıkça döner, örn: `while x < 10:`"
    }

    found = []
    for word in keywords:
        if re.search(rf'\b{word}\b', code):
            found.append(keywords[word])
    return "\n".join(found)

# 🔧 Syntax hatası analizi
def check_syntax(code):
    try:
        compile(code, "<string>", "exec")
        return "Sözdizimi hatası yok.", None
    except SyntaxError as e:
        error_msg = f"Sözdizimi Hatası:\nSatır {e.lineno}: {e.msg}"
        return error_msg + "\n" + get_fix_suggestion(e.msg), e.msg
    except Exception as e:
        return f"Hata: {str(e)}\n" + get_fix_suggestion(str(e)), str(e)

# ⚖️ PEP8 kontrolü
def check_pep8(filename):
    style_guide = pycodestyle.StyleGuide()
    result = style_guide.check_files([filename])
    output = []
    common_errors = []
    for stat in result.get_statistics(''):
        output.append(stat)
        if "E113" in stat:
            common_errors.append("E113")
    return output, common_errors

# 🧠 Hata geçmişi takibi
def update_error_history(error_keys):
    try:
        if not error_keys:
            return
        counter = defaultdict(int)
        if os.path.exists(history_file):
            with open(history_file, "r") as f:
                for line in f:
                    key = line.strip()
                    counter[key] += 1
        for key in error_keys:
            counter[key] += 1
        with open(history_file, "w") as f:
            for k, v in counter.items():
                f.write(f"{k}\n" * v)

        # Tekrar eden hata önerileri
        tips = []
        for k, v in counter.items():
            if v >= 3:
                tips.append(f"Sık yaptığınız hata: {k} — daha dikkatli olmalısınız.")
        return "\n".join(tips)
    except Exception as ex:
        return f"Hata geçmişi güncellenemedi: {str(ex)}"

# ▶️ Kod yürütme
def execute_code(code):
    try:
        local_vars = {}
        exec(code, {}, local_vars)
        return "Kod başarıyla yürütüldü."
    except Exception as e:
        return f"Kod çalıştırılırken hata oluştu: {e}"

# Ana fonksiyon
def main():
    if len(sys.argv) < 2:
        print("Kod dosyası belirtilmedi.")
        return

    filename = sys.argv[1]

    with open(filename, "r", encoding="utf-8") as f:
        code = f.read()

    print("Kod Analizi Sonuçları\n" + "="*30)

    # 1. Sözdizimi
    print("\n1. Sözdizimi Kontrolü:\n" + "-"*25)
    syntax_msg, error_key = check_syntax(code)
    print(syntax_msg)

    # 2. PEP8
    print("\n2. PEP8 Uyumluluğu:\n" + "-"*25)
    pep8_output, pep8_keys = check_pep8(filename)
    if pep8_output:
        print("\n".join(pep8_output))
    else:
        print("PEP8: Uyumlu")

    # 3. Temel açıklamalar
    print("\n3. Anahtar Kelime Açıklamaları:\n" + "-"*25)
    explanations = explain_keywords(code)
    print(explanations if explanations else "Belirgin bir anahtar kelime bulunamadı.")

    # 4. Kod yürütme
    if "Sözdizimi Hatası" not in syntax_msg:
        print("\n4. Kod Çalıştırma:\n" + "-"*25)
        print(execute_code(code))

    # 5. Hata geçmişi takibi
    keys_to_track = []
    if error_key:
        keys_to_track.append(error_key)
    keys_to_track.extend(pep8_keys)
    repeated = update_error_history(keys_to_track)
    if repeated:
        print("\n5. Hata Alışkanlığı Uyarısı:\n" + "-"*25)
        print(repeated)

if __name__ == "__main__":
    import os
    main()
