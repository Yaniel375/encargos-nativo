"""Recorrido automático de la app Android en un emulador (solo GitHub Actions).
Crea un acceso de PRUEBA, registra un encargo ficticio de prueba, recorre las pantallas y guarda capturas.
No usa ningún bot real ni datos reales."""
import re, subprocess, sys, time, os
import xml.etree.ElementTree as ET

OUT = sys.argv[1] if len(sys.argv) > 1 else "shots"
os.makedirs(OUT, exist_ok=True)
PKG = "com.agencia.encargos.nativo"
log = open(os.path.join(OUT, "recorrido.txt"), "w", encoding="utf-8")
n = [0]

def adb(*a, binary=False):
    r = subprocess.run(["adb", *a], capture_output=True)
    return r.stdout if binary else r.stdout.decode("utf-8", "replace")

def say(t):
    print(t); log.write(t + "\n"); log.flush()

def shot(name):
    n[0] += 1
    data = adb("exec-out", "screencap", "-p", binary=True)
    with open(os.path.join(OUT, f"android_{n[0]:02d}_{name}.png"), "wb") as f: f.write(data)
    say(f"captura {n[0]:02d} {name} ({len(data)} bytes)")

def nodes():
    adb("shell", "uiautomator", "dump", "/sdcard/ui.xml")
    x = adb("shell", "cat", "/sdcard/ui.xml")
    try: root = ET.fromstring(x[x.index("<?xml"):] if "<?xml" in x else x)
    except Exception: return []
    return list(root.iter("node"))

def center(nd):
    a, b, c, d = map(int, re.findall(r"\d+", nd.get("bounds")))
    return (a + c) // 2, (b + d) // 2

def find(text, cls=None, exact=False, last=False):
    ns = nodes()
    if last: ns = list(reversed(ns))
    for nd in ns:
        t = (nd.get("text") or "") + " " + (nd.get("content-desc") or "")
        ok = (t.strip() == text) if exact else (text.lower() in t.lower())
        if ok and (cls is None or cls in (nd.get("class") or "")): return nd
    # campo de texto: la etiqueta puede estar en un hijo
    if cls:
        for nd in nodes():
            if cls in (nd.get("class") or "") and any(text.lower() in ((c.get("text") or "") + (c.get("content-desc") or "")).lower() for c in nd.iter("node")): return nd
    return None

def tap(text, cls=None, exact=False, wait=1.5, last=False):
    nd = find(text, cls, exact, last)
    if nd is None: say(f"  ✗ no encontré '{text}'"); return False
    x, y = center(nd); adb("shell", "input", "tap", str(x), str(y)); time.sleep(wait); say(f"  ✓ toqué '{text}'"); return True

def type_in(label, value):
    # la etiqueta está encima del campo: se toca el primer campo de texto que hay debajo
    hide_kb()
    lab = find(label)
    es = edits()
    target = None
    if lab is not None:
        ly = center(lab)[1]
        lx = center(lab)[0]
        below = [e for e in es if center(e)[1] > ly]
        if below:
            row = min(center(e)[1] for e in below)
            same = [e for e in below if abs(center(e)[1] - row) < 40]
            # el campo de esa fila que está bajo la etiqueta (por la izquierda del campo)
            target = min(same, key=lambda e: abs(int(re.findall(r"\d+", e.get("bounds"))[0]) - (lx - 200)))
    if target is None: target = find(label, "EditText")
    if target is None: say(f"  ✗ campo '{label}'"); return
    x, y = center(target); adb("shell", "input", "tap", str(x), str(y)); time.sleep(0.6)
    adb("shell", "input", "text", value.replace(" ", "%s")); time.sleep(0.4); say(f"  ✓ escribí en {label}")

def edits():
    es = [nd for nd in nodes() if "EditText" in (nd.get("class") or "")]
    return sorted(es, key=lambda nd: center(nd)[1])

def type_nth(i, value):
    hide_kb()
    es = edits()
    if i >= len(es): say(f"  ✗ no hay campo #{i}"); return
    x, y = center(es[i]); adb("shell", "input", "tap", str(x), str(y)); time.sleep(0.6)
    adb("shell", "input", "text", value.replace(" ", "%s")); time.sleep(0.4); say(f"  ✓ escribí en el campo #{i}")

def kb_open():
    return "mInputShown=true" in adb("shell", "dumpsys", "input_method")

def hide_kb():
    for _ in range(2):
        if not kb_open(): return
        adb("shell", "input", "keyevent", "4"); time.sleep(0.8)  # ATRÁS con teclado abierto solo cierra el teclado

def crashed():
    out = adb("logcat", "-d", "-b", "crash")
    return PKG in out and "FATAL EXCEPTION" in out

adb("logcat", "-c")
adb("shell", "am", "start", "-W", "-n", f"{PKG}/.MainActivity"); time.sleep(8)
shot("crear_acceso")
def type_at(label, value):
    hide_kb()
    if tap(label, wait=0.7):
        adb("shell", "input", "text", value.replace(" ", "%s")); time.sleep(0.4)
type_at("Usuario", "prueba"); type_at("Contraseña (mínimo 6)", "clave123"); type_at("Repite la contraseña", "clave123"); hide_kb()
shot("acceso_lleno")
tap("Crear acceso", wait=5)
shot("inicio")
tap("Nuevo", exact=True, wait=2, last=True); shot("nuevo_encargo")
type_in("Nombre del cliente", "Cliente Prueba"); type_in("Teléfono", "55512345"); hide_kb()
type_in("Costo del pedido", "40"); type_in("Libras", "3"); hide_kb()
shot("nuevo_encargo_lleno")
for _ in range(4):
    hide_kb()
    if find("Guardar encargo") is not None:
        shot("resumen_antes_de_guardar"); tap("Guardar encargo", wait=3); break
    adb("shell", "input", "swipe", "540", "1500", "540", "500", "400"); time.sleep(1.2)
shot("guardado")
tap("Encargos", exact=True, wait=2, last=True); shot("encargos")
say("  encargo visible: " + ("sí" if find("Cliente Prueba") is not None else "NO"))
if tap("Cliente Prueba", wait=2): shot("ficha_encargo"); say("  total 61.00 visible: " + ("sí" if find("$61.00") is not None else "NO")); adb("shell", "input", "keyevent", "4"); time.sleep(1)
tap("Clientes", exact=True, wait=2, last=True); shot("clientes")
tap("Notas", exact=True, wait=2, last=True); shot("notas")
tap("Inicio", exact=True, wait=2, last=True); shot("inicio_con_datos")
for dest in ["Registro de pagos", "Estadísticas", "Respaldo", "Seguridad", "Sincronización", "Ajustes"]:
    if tap("Más", wait=1.2) and tap(dest, wait=2.5): shot(dest.lower().replace(" ", "_"))
    adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
tap("Centro de atención", wait=2); shot("atencion"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
tap("Notificaciones", wait=2); shot("notificaciones")
alive = adb("shell", "pidof", PKG).strip()
say("RESULTADO: " + ("SE CERRÓ (crash)" if crashed() or not alive else f"la app sigue abierta (pid {alive})"))
with open(os.path.join(OUT, "crash.txt"), "w", encoding="utf-8") as f: f.write(adb("logcat", "-d", "-b", "crash"))
sys.exit(1 if crashed() or not alive else 0)
