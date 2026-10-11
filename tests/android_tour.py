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

def type_in(label, value, exact=True):
    # la etiqueta está encima del campo: se toca el primer campo de texto que hay debajo.
    # si la etiqueta queda tapada por la barra fija "Guardar encargo", se desplaza la pantalla primero
    hide_kb()
    for _ in range(4):
        lab = find(label, exact=exact)
        if lab is not None and center(lab)[1] < 1350: break
        adb("shell", "input", "swipe", "540", "1300", "540", "800", "400"); time.sleep(1)
    lab = find(label, exact=exact)
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
            labl = int(re.findall(r"\d+", lab.get("bounds"))[0])
            target = min(same, key=lambda e: abs(int(re.findall(r"\d+", e.get("bounds"))[0]) - labl))
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

def home():
    # vuelve a Inicio con la barra inferior; si la app quedó en segundo plano, la trae al frente (sin reiniciarla)
    hide_kb()
    if not tap("Inicio", exact=True, wait=1.5, last=True):
        adb("shell", "am", "start", "-n", f"{PKG}/.MainActivity"); time.sleep(3)
        tap("Inicio", exact=True, wait=1.5, last=True)

def crashed():
    out = adb("logcat", "-d", "-b", "crash")
    return PKG in out and "FATAL EXCEPTION" in out

adb("logcat", "-c")
adb("shell", "am", "start", "-W", "-n", f"{PKG}/.MainActivity"); time.sleep(8)
shot("crear_acceso")
def type_at(label, value, exact=False):
    hide_kb()
    if tap(label, exact=exact, wait=0.7):
        adb("shell", "input", "text", value.replace(" ", "%s")); time.sleep(0.4)
type_at("Usuario", "prueba"); type_at("Contraseña (mínimo 6)", "clave123"); type_at("Repite la contraseña", "clave123"); hide_kb()
shot("acceso_lleno")
tap("Crear acceso", wait=5)
shot("inicio")
tap("Nuevo", exact=True, wait=2, last=True); shot("nuevo_encargo")
type_in("Nombre del cliente *", "Cliente Prueba"); type_in("Teléfono (WhatsApp)", "55512345"); hide_kb()
type_in("Costo del pedido", "40"); type_in("Libras", "3"); hide_kb(); type_in("Pago inicial", "10"); hide_kb()
shot("nuevo_encargo_lleno")
for _ in range(4):
    hide_kb()
    if find("Guardar encargo") is not None:
        shot("resumen_antes_de_guardar"); tap("Guardar encargo", wait=3); break
    adb("shell", "input", "swipe", "540", "1500", "540", "500", "400"); time.sleep(1.2)
shot("guardado")
tap("Encargos", exact=True, wait=2, last=True); shot("encargos")
say("  encargo visible: " + ("sí" if find("Cliente Prueba") is not None else "NO"))
if tap("Cliente Prueba", wait=2): shot("ficha_encargo"); say("  total 61.00 visible: " + ("sí" if find("$61.00") is not None else "NO")); say("  pagado 10.00 visible: " + ("sí" if find("$10.00") is not None else "NO")); adb("shell", "input", "keyevent", "4"); time.sleep(1)
tap("Clientes", exact=True, wait=2, last=True); shot("clientes")
tap("Notas", exact=True, wait=2, last=True); shot("notas")
tap("Inicio", exact=True, wait=2, last=True); shot("inicio_con_datos")
# menú del perfil: Pagos · Estadísticas · Ajustes
for dest in ["Registro de pagos", "Estadísticas", "Ajustes"]:
    if tap("Más", wait=1.2) and tap(dest, wait=2.5): shot(dest.lower().replace(" ", "_"))
    adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
# lo que salió del menú sigue accesible desde Ajustes → Datos y seguridad
for dest, row in [("respaldo", "Respaldo e importación"), ("seguridad", "Seguridad"), ("sincronización", "Sincronización (Telegram)")]:
    if tap("Más", wait=1.2) and tap("Ajustes", wait=2.5):
        for _ in range(5):
            nd = find(row, exact=True)
            if nd is not None and center(nd)[1] < 1550: break  # por encima de la barra inferior
            adb("shell", "input", "swipe", "540", "1500", "540", "700", "400"); time.sleep(1)
        time.sleep(1.5)  # deja que termine el desplazamiento por inercia antes de tocar la fila
        if tap(row, exact=True, wait=2.5):
            shot(dest)
            # al volver atrás, Ajustes debe seguir desplazado donde estaba (la fila sigue a la vista)
            hide_kb(); adb("shell", "input", "keyevent", "4"); time.sleep(1.8); shot(f"volver_de_{dest}")
            say(f"  al volver de {dest}, Ajustes conserva su posición: " + ("sí" if find(row, exact=True) is not None else "NO"))
    home()
if tap("Más", wait=1.5): shot("menu_perfil"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
if tap("Más", wait=1.2) and tap("Ajustes", wait=2.5):
    if tap("Precio por libra", wait=2): shot("ajustes_hoja_tarifas"); tap("Cerrar", exact=True, wait=1.2)
    adb("shell", "input", "swipe", "540", "1500", "540", "500", "500"); time.sleep(1.2); shot("ajustes_abajo")
    adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
if tap("Más", wait=1.2) and tap("Registro de pagos", wait=2.5):
    if tap("Pendientes y fondos a favor", wait=2.5): shot("pendientes"); adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
    adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
tap("Encargos", exact=True, wait=2, last=True)
if tap("Opciones del encargo", wait=1.2):
    shot("menu_encargo")
    if tap("Enviar recibo", exact=True, wait=1.8): shot("ventana_recibo"); tap("Cerrar", exact=True, wait=1.2)
    else: adb("shell", "input", "keyevent", "4"); time.sleep(1)
tap("Inicio", exact=True, wait=2, last=True)
tap("Centro de atención", wait=2); shot("atencion"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
tap("Notificaciones", wait=2); shot("notificaciones")
# ---------- segunda vuelta: MODO DÍA ----------
say("— modo día —")
adb("shell", "am", "start", "-S", "-W", "-n", f"{PKG}/.MainActivity", "--es", "tema", "dia"); time.sleep(8)
shot("dia_acceso")
if find("Iniciar sesión") is not None:
    type_at("Contraseña", "clave123", exact=True); hide_kb(); tap("Iniciar sesión", wait=5)   # el usuario ya viene recordado
shot("dia_inicio")
adb("shell", "input", "swipe", "540", "1500", "540", "500", "500"); time.sleep(1.5); shot("dia_inicio_abajo")
for _ in range(4):
    if find("Últimos pagos") is not None and center(find("Últimos pagos"))[1] < 1000: break
    adb("shell", "input", "swipe", "540", "1500", "540", "700", "450"); time.sleep(1.2)
shot("dia_ultimos_pagos")
tap("Encargos", exact=True, wait=2, last=True); shot("dia_encargos")
if tap("Cliente Prueba", wait=2): shot("dia_ficha"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
tap("Clientes", exact=True, wait=2, last=True); shot("dia_clientes")
tap("Nuevo", exact=True, wait=2, last=True); shot("dia_nuevo")
tap("Notas", exact=True, wait=2, last=True); shot("dia_notas")
if tap("Más", wait=1.5): shot("dia_menu_perfil"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
for dest in ["Estadísticas", "Registro de pagos", "Ajustes"]:
    if tap("Más", wait=1.2) and tap(dest, wait=2.5): shot("dia_" + dest.lower().replace(" ", "_"))
    adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
tap("Notificaciones", wait=2); shot("dia_notificaciones"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
tap("Centro de atención", wait=2); shot("dia_atencion")
# ---------- gráficas: captura a mitad del llenado y al final ----------
say("— gráficas —")
if tap("Más", wait=1.2) and tap("Registro de pagos", wait=0.55): shot("pagos_llenandose"); time.sleep(2.5); shot("pagos_lleno")
adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
if tap("Más", wait=1.2) and tap("Estadísticas", wait=0.6): shot("estadisticas_llenandose"); time.sleep(2.5); shot("estadisticas_llenas")
adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
# ---------- tamaño de la interfaz ----------
say("— tamaño de la interfaz —")
if tap("Más", wait=1.2) and tap("Ajustes", wait=2.5):
    for _ in range(3):
        if find("Tamaño de la interfaz", exact=True) is not None: break
        adb("shell", "input", "swipe", "540", "1500", "540", "700", "400"); time.sleep(1)
    if tap("Tamaño de la interfaz", exact=True, wait=2): shot("hoja_tamano"); tap("Cerrar", exact=True, wait=1.2)
    adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
def relaunch(extra):
    adb("shell", "am", "start", "-S", "-W", "-n", f"{PKG}/.MainActivity", *extra); time.sleep(8)
    if find("Iniciar sesión") is not None:
        type_at("Contraseña", "clave123", exact=True); hide_kb(); tap("Iniciar sesión", wait=5)
# ---------- fotos completas (fotos de muestra con marco rojo y esquinas marcadas; solo en la copia de prueba) ----------
say("— fotos —")
def swipe_to(text, n=6):
    # desplaza hasta que el elemento quede bien visible (por encima de la barra inferior)
    for _ in range(n):
        nd = find(text)
        if nd is not None and center(nd)[1] < 1550: return True
        adb("shell", "input", "swipe", "540", "1500", "540", "700", "450"); time.sleep(1.1)
    nd = find(text); return nd is not None and center(nd)[1] < 1550
relaunch(["--es", "tema", "noche", "--es", "fotos", "si", "--es", "escala", "1.0"])
tap("Encargos", exact=True, wait=2, last=True)
if tap("Cliente Prueba", wait=2):
    if swipe_to("Foto 1 de 2"): shot("ficha_encargo_fotos")
    if tap("Foto 1 de 2", wait=2):
        shot("visor_foto_vertical")
        adb("shell", "input", "swipe", "900", "1100", "150", "1100", "300"); time.sleep(1.5); shot("visor_foto_horizontal")
        adb("shell", "input", "tap", "540", "1100"); time.sleep(0.12); adb("shell", "input", "tap", "540", "1100"); time.sleep(1.2); shot("visor_ampliado")
        tap("Cerrar", exact=True, wait=1.2)
home()
tap("Clientes", exact=True, wait=2, last=True)
if tap("Cliente Prueba", wait=2.5):
    shot("ficha_cliente_arriba")
    adb("shell", "input", "swipe", "540", "1500", "540", "600", "450"); time.sleep(1.2); shot("ficha_cliente_medio")
    if swipe_to("Capturas de pedidos"): adb("shell", "input", "swipe", "540", "1400", "540", "900", "450"); time.sleep(1); shot("ficha_cliente_capturas")
home()
tap("Notas", exact=True, wait=2, last=True); shot("notas_con_fotos")
if tap("Foto 1 de 3", wait=2): shot("visor_nota"); tap("Cerrar", exact=True, wait=1.2)
home()
# ---------- foto de perfil y ventanas con el estilo de la app (foto de prueba, solo en la copia de prueba) ----------
say("— perfil y ventanas —")
if tap("Más", wait=1.5): shot("menu_con_foto"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
if tap("Más", wait=1.2) and tap("Ajustes", wait=2.5):
    shot("ajustes_perfil_foto")
    if tap("Quitar foto", exact=True, wait=1.5): shot("ventana_confirmar"); tap("Cancelar", exact=True, wait=1.2)
    if tap("Nombre del negocio", exact=True, wait=2): shot("ventana_nombre_negocio"); tap("Cerrar", exact=True, wait=1.2)
    adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
if tap("Más", wait=1.2) and tap("Registro de pagos", wait=2.5):
    if tap("Meta ›", wait=2): shot("ventana_meta"); tap("Cancelar", exact=True, wait=1.2)
    adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
home()
for esc, tag in [("1.05", "105"), ("0.8", "80")]:
    say(f"— escala {tag} % —")
    relaunch(["--es", "tema", "noche", "--es", "escala", esc])
    shot(f"escala{tag}_inicio")
    tap("Encargos", exact=True, wait=2, last=True); shot(f"escala{tag}_encargos")
    if tap("Cliente Prueba", wait=2): shot(f"escala{tag}_ficha"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
    tap("Nuevo", exact=True, wait=2, last=True); shot(f"escala{tag}_nuevo")
    if tap("Más", wait=1.5): shot(f"escala{tag}_menu"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
    if tap("Más", wait=1.2) and tap("Ajustes", wait=2.5): shot(f"escala{tag}_ajustes"); adb("shell", "input", "keyevent", "4"); time.sleep(1.2)
# ¿se recuerda el tamaño al cerrar y volver a abrir (sin forzarlo)?
relaunch([])
ok = False
if tap("Más", wait=1.2) and tap("Ajustes", wait=2.5):
    for _ in range(3):
        if find("80 %") is not None: ok = True; break
        adb("shell", "input", "swipe", "540", "1500", "540", "700", "400"); time.sleep(1)
    shot("escala_recordada")
say("  tamaño recordado tras reiniciar (80 %): " + ("sí" if ok else "NO"))
relaunch(["--es", "escala", "1.0"])
# ---------- acceso con PIN (PIN de prueba 1234, solo en la copia de prueba) ----------
say("— acceso con PIN —")
adb("shell", "am", "start", "-S", "-W", "-n", f"{PKG}/.MainActivity", "--es", "pin", "1234", "--es", "tema", "noche"); time.sleep(8)
shot("pin_vacio")
for k in ["1", "1"]: tap(k, exact=True, wait=0.4)
shot("pin_escribiendo")
for k in ["1", "1"]: tap(k, exact=True, wait=0.4)
time.sleep(1.5); shot("pin_incorrecto")
# misma tarjeta con usuario y contraseña (debe tener el mismo tamaño que la del PIN)
if tap("Clave", exact=True, wait=1.5):
    shot("acceso_contrasena"); hide_kb()
    tap("Entrar con PIN ›", exact=True, wait=1.5)
for k in ["1", "2", "3", "4"]: tap(k, exact=True, wait=0.4)
time.sleep(4); say("  entró con el PIN: " + ("sí" if find("Inicio", exact=True) is not None else "NO")); shot("pin_dentro")
if tap("Más", wait=1.5): shot("menu_perfil_final"); adb("shell", "input", "keyevent", "4"); time.sleep(1)
alive = adb("shell", "pidof", PKG).strip()
say("RESULTADO: " + ("SE CERRÓ (crash)" if crashed() or not alive else f"la app sigue abierta (pid {alive})"))
with open(os.path.join(OUT, "crash.txt"), "w", encoding="utf-8") as f: f.write(adb("logcat", "-d", "-b", "crash"))
sys.exit(1 if crashed() or not alive else 0)
