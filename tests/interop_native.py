"""Pruebas cruzadas entre los dos núcleos nativos: Android (Kotlin) ↔ PC (C#), a través del simulador de Telegram.
Nunca usa el bot real: todo pasa por tests/mock_tg.py en 127.0.0.1.

Variables de entorno:
  KT_CP   classpath del dispositivo Kotlin (core + clases de prueba + kotlin-stdlib)
  CS_DLL  ruta a Encargos.Tests.dll (dispositivo C#). Si falta, el segundo dispositivo también es Kotlin (solo para depurar el guion).
"""
import json, os, subprocess, sys, tempfile, time, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
API_PORT = 9510; API = f'http://127.0.0.1:{API_PORT}'
TOKEN = '123456789:AAEncargosTestTokenXXXXXXXXXXXXXXXX'
R = []

def check(name, ok, info=''):
    R.append((name, ok)); print(('✅' if ok else '❌'), name, ('· ' + str(info) if info != '' else ''), flush=True)

def ctl(p): return json.loads(urllib.request.urlopen(API + p).read())['result']
def backups(): return [m for m in ctl('/__list') if m['name'] == 'encargos-latest.enc']

class Dev:
    def __init__(self, port, plat, kind):
        self.port = port; d = tempfile.mkdtemp(); self.kind = kind
        if kind == 'cs':
            cmd = ['dotnet', os.environ['CS_DLL'], 'device', str(port), API, d, plat]
        else:
            cmd = ['java', '-Dfile.encoding=UTF-8', '-cp', os.environ['KT_CP'], 'com.agencia.encargos.core.DeviceServer', str(port), API, d, plat]
        self.p = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
        for _ in range(150):
            try: self.q('/state'); return
            except Exception: time.sleep(0.2)
        raise Exception(f'el dispositivo {kind} no arrancó')
    def q(self, path, **kw):
        req = urllib.request.Request(f'http://127.0.0.1:{self.port}{path}', data=json.dumps(kw).encode(), method='POST')
        r = json.loads(urllib.request.urlopen(req, timeout=120).read())['r']
        if isinstance(r, dict) and 'error' in r and len(r) == 1: raise Exception(r['error'])
        return r
    def sig(self):
        rows = [[o[0], o[1], o[2], round(o[3], 2), round(o[4], 2), round(o[5], 2), round(o[6], 2)] for o in self.q('/state')['orders']]
        return sorted(rows, key=lambda r: json.dumps(r, ensure_ascii=False, separators=(',', ':')))
    def full(self, name): return [o for o in self.q('/state')['full'] if o['name'] == name][0]
    def stop(self): self.p.kill()

def settle(*ds, rounds=2):
    for _ in range(rounds):
        for d in ds: d.q('/sync')

mock = subprocess.Popen([sys.executable, os.path.join(HERE, 'mock_tg.py'), str(API_PORT)], stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
for _ in range(50):
    try: ctl('/__reset'); break
    except Exception: time.sleep(0.2)

second = 'cs' if os.environ.get('CS_DLL') else 'kt'
A = Dev(9601, 'ANDROID', 'kt'); W = Dev(9602, 'PC', second)
try:
    A.q('/order', name='Juan Pérez', cost=45, lb=2, fund=20, status='En tránsito'); A.q('/order', name='Rosa Díaz', cost=80, lb=3, fund=100)
    A.q('/note', title='Pendiente Carlos', body='Me debe', amount=35)
    r = A.q('/connect', token=TOKEN)
    check('Android (Kotlin) conecta el bot', r == 'Ok', r)
    code = A.q('/code'); check('Código ENC1 generado', isinstance(code, str) and code.startswith('ENC1.'))
    r = W.q('/link', code=code)
    check(f'PC ({second}) se enlaza con el código ENC1', r == 'Ok', r)
    check('PC recibe exactamente los mismos encargos', W.sig() == A.sig(), W.sig())
    st = W.q('/state'); check('PC recibe la nota con monto', st['notes'] and st['notes'][0].get('amount') == 35, st['notes'][:1])
    check('Telegram: un solo respaldo', len(backups()) == 1)

    W.q('/order', name='Cliente PC', cost=60, lb=2, fund=10); W.q('/sync'); A.q('/sync')
    check('Encargo creado en PC aparece en Android con los mismos números', W.sig() == A.sig())

    A.q('/pay', name='Juan Pérez', amt=7); W.q('/pay', name='Juan Pérez', amt=5); settle(A, W)
    jp = A.full('Juan Pérez'); amts = sorted(h['amount'] for h in jp['paymentHistory'])
    check('Pagos simultáneos (Android $7 + PC $5) se conservan los dos', amts == [5, 7, 20] and jp['paid'] == 32, amts)
    check('Saldos iguales en los dos', W.sig() == A.sig())

    W.q('/advance', name='Rosa Díaz'); settle(W, A)
    check('Cambio de estado PC → Android', A.full('Rosa Díaz')['status'] == 'En tránsito')
    check('statusDates llega a Android', 'En tránsito' in A.full('Rosa Díaz').get('statusDates', {}))
    A.q('/lb', name='Rosa Díaz', lb=2); settle(A, W)
    rd = [o for o in W.sig() if o[1] == 'Rosa Díaz'][0]
    check('Libras agregadas en Android → PC con el total correcto', rd[3] == 5 and rd[4] == 115, rd)

    A.q('/field', name='Juan Pérez', field='campoFuturo', value={'a': 1, 'b': 'ñ'}); settle(A, W)
    W.q('/lb', name='Juan Pérez', lb=1); settle(W, A)
    cf = A.full('Juan Pérez').get('campoFuturo'); cfw = W.full('Juan Pérez').get('campoFuturo')
    check('Campo desconocido sobrevive a una edición en el otro núcleo', cf == {'a': 1, 'b': 'ñ'} and cfw == cf, (cf, cfw))

    W.q('/clientinfo', name='Rosa Díaz', address='Calle 10 #45', note='Talla M'); W.q('/templates', arrival='Llegó {nombre} ✅', reminder='Debes {saldo}', thanks='Gracias'); settle(W, A)
    st = A.q('/state')
    check('Datos fijos del cliente PC → Android', 'Calle 10' in json.dumps(st['clientInfo'], ensure_ascii=False))
    check('Plantillas PC → Android', st['templates'].get('arrival') == 'Llegó {nombre} ✅')

    A.q('/paydel', name='Juan Pérez', idx=0); settle(A, W)
    check('Pago eliminado en Android → PC', W.sig() == A.sig(), [o for o in W.sig() if o[1] == 'Juan Pérez'])
    W.q('/delete', name='Cliente PC'); settle(W, A)
    check('Eliminar en PC → desaparece en Android', not any(o[1] == 'Cliente PC' for o in A.sig()))

    bid = backups()[0]['id']
    ctl('/__down'); W.q('/order', name='Sin Red', cost=9); r = W.q('/sync')
    check('Sin Internet: la PC informa "offline" y guarda la cola', r['state'] == 'offline' and W.q('/state')['pending'] > 0, r)
    W.q('/restart')
    check('Tras reiniciar sin Internet, cola y datos siguen ahí', W.q('/state')['pending'] > 0 and any(o[1] == 'Sin Red' for o in W.sig()))
    ctl('/__up')
    check('El respaldo anterior sigue en Telegram mientras no hubo Internet', bid in [b['id'] for b in backups()])
    settle(W, A)
    check('Al volver Internet, Android recibe lo hecho sin conexión', any(o[1] == 'Sin Red' for o in A.sig()) and W.q('/state')['pending'] == 0)

    for name, inj in [('Subida falla', '/__fail/sendDocument'), ('Checksum no coincide', '/__corrupt'), ('Telegram rechaza editar el fijado', '/__fail/editMessageText')]:
        bid = backups()[0]['id']; ctl(inj); W.q('/order', name='Fallo ' + name, cost=3); W.q('/sync')
        ids = [b['id'] for b in backups()]
        check(f'{name} (PC): respaldo anterior intacto y uno solo', ids == [bid], ids)
        settle(W, A)
        check(f'{name}: al reintentar Android lo recibe', any(o[1] == 'Fallo ' + name for o in A.sig()) and len(backups()) == 1)

    A.q('/field', name='Rosa Díaz', field='phone', value='5550001'); W.q('/field', name='Rosa Díaz', field='phone', value='5559999'); settle(A, W, rounds=3)
    pa = A.full('Rosa Díaz')['phone']; pw = W.full('Rosa Díaz')['phone']
    check('Conflicto: los dos terminan con el mismo valor', pa == pw, (pa, pw))

    r = W.q('/restore'); check('Restaurar en la PC', r == 'ok' and W.sig() == A.sig(), r)
    settle(A, W)
    check('Tras restaurar nada se duplica y hay un solo respaldo', W.sig() == A.sig() and len(backups()) == 1)
    L = ctl('/__list'); pinned = [m for m in L if m['pinned']]
    man = json.loads(pinned[0]['text'].split('#ENCARGOS_BACKUP_LATEST\n')[1])
    check('Mensaje fijado válido (lo lee cualquier núcleo)', man['type'] == 'ENCARGOS_BACKUP_LATEST' and man['schemaVersion'] == 1, man['deviceId'][:8])
    check('Notificaciones en la PC registran lo recibido', len(W.q('/state')['notifs']) > 0)
finally:
    A.stop(); W.stop(); mock.kill()
ok = sum(1 for _, o in R if o)
print('\nRESULTADO:', ok, 'de', len(R), 'pruebas correctas')
sys.exit(0 if ok == len(R) else 1)
