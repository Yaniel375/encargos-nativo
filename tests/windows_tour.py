"""Recorrido automático de la app de Windows (solo en GitHub Actions, con datos de PRUEBA).
Crea un acceso de prueba, registra un encargo ficticio, recorre el menú y guarda capturas."""
import os, sys, time, traceback
from pywinauto import Application, Desktop
from PIL import ImageGrab

EXE = sys.argv[1]; OUT = sys.argv[2]
os.makedirs(OUT, exist_ok=True)
log = open(os.path.join(OUT, "recorrido_windows.txt"), "w", encoding="utf-8")
n = [0]
def say(t): print(t); log.write(t + "\n"); log.flush()

for w in Desktop(backend="win32").windows():
    try:
        if w.window_text() in ("System Properties", "Propiedades del sistema"): w.close(); say("cerré un aviso del sistema")
    except Exception: pass

app = Application(backend="uia").start(EXE)
time.sleep(12)
win = app.window(title_re="Encargos.*")
win.wait("visible", timeout=40)
try: win.maximize()
except Exception: pass
win.set_focus(); time.sleep(2)

def shot(name):
    n[0] += 1
    try: win.set_focus()
    except Exception: pass
    time.sleep(0.8)
    r = win.rectangle()
    ImageGrab.grab(bbox=(max(r.left, 0), max(r.top, 0), r.right, r.bottom)).save(os.path.join(OUT, f"windows_{n[0]:02d}_{name}.png"))
    say(f"captura {n[0]:02d} {name}")

def el(name, ctype=None):
    kw = {"title": name}
    if ctype: kw["control_type"] = ctype
    return win.child_window(**kw)

def click(name, ctype=None, wait=1.5):
    try:
        e = el(name, ctype); e.wait("exists", timeout=6); e.click_input(); time.sleep(wait); say(f"  ✓ {name}"); return True
    except Exception as ex: say(f"  ✗ no encontré '{name}' ({type(ex).__name__})"); return False

def type_in(name, text):
    try:
        e = el(name, "Edit"); e.wait("exists", timeout=6); e.click_input(); e.type_keys("^a" + text, with_spaces=True); time.sleep(0.4); say(f"  ✓ escribí en {name}")
    except Exception as ex: say(f"  ✗ campo '{name}' ({type(ex).__name__})")

def exists(name):
    try: return el(name).exists(timeout=3)
    except Exception: return False

ok = True
try:
    shot("crear_acceso")
    type_in("Usuario", "prueba"); type_in("Contraseña (mínimo 6)", "clave123"); type_in("Repite la contraseña", "clave123")
    click("Crear acceso", "Button", wait=5)
    shot("inicio")
    click("Nuevo encargo", "ListItem", wait=2); shot("nuevo_encargo")
    type_in("Nombre del cliente", "Cliente Prueba"); type_in("Teléfono (WhatsApp)", "55512345")
    type_in("Costo del pedido ($)", "40"); type_in("Libras", "3")
    shot("nuevo_encargo_lleno")
    click("Guardar encargo", "Button", wait=3); shot("guardado")
    click("Encargos", "ListItem", wait=2); shot("encargos")
    say("  encargo visible: " + ("sí" if exists("Cliente Prueba") else "NO"))
    for item in ["Clientes", "Notas", "Pagos", "Pendientes", "Estadísticas", "Centro de atención", "Notificaciones", "Completados", "Respaldo", "Seguridad", "Sincronización", "Ajustes", "Inicio"]:
        if click(item, "ListItem", wait=2): shot(item.lower().replace(" ", "_"))
except Exception:
    ok = False; say("ERROR en el recorrido:\n" + traceback.format_exc())
alive = app.is_process_running()
say("RESULTADO: " + ("la app sigue abierta" if alive else "SE CERRÓ"))
try: app.kill()
except Exception: pass
sys.exit(0 if alive else 1)
