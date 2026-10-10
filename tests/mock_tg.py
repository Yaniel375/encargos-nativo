# Simulador de la Telegram Bot API con los métodos reales que usa Encargos:
# getMe, getUpdates, getChat (pinned_message), sendMessage, sendDocument, getFile, /file/bot…, editMessageText,
# editMessageMedia, pinChatMessage, deleteMessage (con el límite real de 48 h).
# Controles de prueba: /__down /__up /__stats /__list /__age (todo >48h) /__fail/<metodo> /__corrupt /__reset
import json, sys, uuid, threading, time
from email.parser import BytesParser
from email.policy import HTTP
class FileField:
    def __init__(self, data, filename):
        import io; self.file = io.BytesIO(data); self.filename = filename
def parse_multipart(ct, body):
    msg = BytesParser(policy=HTTP).parsebytes(b'Content-Type: ' + ct.encode() + b'\r\n\r\n' + body)
    out = {}
    for part in msg.iter_parts():
        name = part.get_param('name', header='content-disposition'); fn = part.get_filename(); data = part.get_payload(decode=True) or b''
        out[name] = FileField(data, fn) if fn else data.decode('utf-8')
    return out
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse
TOKEN = '123456789:AAEncargosTestTokenXXXXXXXXXXXXXXXX'
CHAT = 777000111
S = {}
def reset():
    S.update(FILES={}, MSGS={}, PIN=None, DOWN=False, MID=100, FAIL={}, CORRUPT=0, CALLS={}, DL=0, UP=0)
reset()
LOCK = threading.Lock()
def store(data, name):
    fid = 'F' + uuid.uuid4().hex; path = 'documents/' + fid + '.bin'
    S['FILES'][path] = data; S['FILES'][fid] = path
    return {'file_id': fid, 'file_unique_id': fid[:8], 'file_name': name, 'file_size': len(data)}
class H(BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def cors(self):
        self.send_header('Access-Control-Allow-Origin', '*'); self.send_header('Access-Control-Allow-Headers', '*'); self.send_header('Access-Control-Allow-Methods', 'GET,POST,OPTIONS')
    def js(self, code, obj):
        b = json.dumps(obj).encode(); self.send_response(code); self.cors(); self.send_header('Content-Type', 'application/json'); self.end_headers(); self.wfile.write(b)
    def ok(self, r): self.js(200, {'ok': True, 'result': r})
    def err(self, c, d): self.js(c, {'ok': False, 'error_code': c, 'description': d})
    def do_OPTIONS(self): self.send_response(204); self.cors(); self.end_headers()
    def do_GET(self):
        p = urlparse(self.path).path
        if p == '/__down': S['DOWN'] = True; return self.ok(1)
        if p == '/__up': S['DOWN'] = False; return self.ok(1)
        if p == '/__reset': reset(); return self.ok(1)
        if p == '/__corrupt': S['CORRUPT'] += 1; return self.ok(1)
        if p.startswith('/__fail/'): m = p[8:]; S['FAIL'][m] = S['FAIL'].get(m, 0) + 1; return self.ok(1)
        if p == '/__age':
            for m in S['MSGS'].values(): m['date'] -= 3 * 86400
            return self.ok(1)
        if p == '/__stats': return self.ok({'messages': len(S['MSGS']), 'pinned': S['PIN'], 'calls': S['CALLS'], 'downloads': S['DL'], 'uploads': S['UP']})
        if p == '/__list':
            out = []
            for mid, m in sorted(S['MSGS'].items()):
                out.append({'id': mid, 'kind': 'doc' if 'document' in m else 'text', 'name': m.get('document', {}).get('file_name'), 'text': m.get('text'), 'caption': m.get('caption'), 'pinned': mid == S['PIN'], 'from_user': m.get('from_user', False)})
            return self.ok(out)
        if S['DOWN']: self.close_connection = True; return
        if p.startswith('/file/bot'):
            tok, path = p[len('/file/bot'):].split('/', 1)
            if tok != TOKEN: return self.err(401, 'Unauthorized')
            data = S['FILES'].get(path)
            if data is None: return self.err(404, 'Not Found')
            S['DL'] += 1
            if S['CORRUPT']: S['CORRUPT'] -= 1; data = data[:-1] + bytes([data[-1] ^ 1])
            self.send_response(200); self.cors(); self.send_header('Content-Type', 'application/octet-stream'); self.end_headers(); self.wfile.write(data); return
        self.err(404, 'Not Found')
    def do_POST(self):
        if S['DOWN']: self.close_connection = True; return
        p = urlparse(self.path).path
        if not p.startswith('/bot'): return self.err(404, 'Not Found')
        tok, method = p[4:].split('/', 1)
        if tok != TOKEN: return self.err(401, 'Unauthorized')
        ct = self.headers.get('Content-Type', '')
        if ct.startswith('multipart/form-data'):
            n = int(self.headers.get('Content-Length') or 0); params = parse_multipart(ct, self.rfile.read(n))
        else:
            n = int(self.headers.get('Content-Length') or 0); params = json.loads(self.rfile.read(n) or b'{}')
        with LOCK:
            S['CALLS'][method] = S['CALLS'].get(method, 0) + 1
            if S['FAIL'].get(method):
                S['FAIL'][method] -= 1; return self.err(400, 'Bad Request: simulated failure of ' + method)
            MSGS = S['MSGS']
            def new_msg(**kw):
                S['MID'] += 1; m = {'message_id': S['MID'], 'date': int(time.time()), 'chat': {'id': CHAT, 'type': 'private'}}; m.update(kw); MSGS[S['MID']] = m; return m
            if method == 'getMe': return self.ok({'id': 123456789, 'is_bot': True, 'username': 'EncargosTestBot'})
            if method == 'getUpdates': return self.ok([{'update_id': 1, 'message': {'message_id': 1, 'chat': {'id': CHAT, 'type': 'private', 'first_name': 'Yaniel'}, 'text': '/start'}}])
            if method == 'getChat':
                if int(params.get('chat_id')) != CHAT: return self.err(400, 'Bad Request: chat not found')
                r = {'id': CHAT, 'type': 'private'}
                if S['PIN'] and S['PIN'] in MSGS: r['pinned_message'] = MSGS[S['PIN']]
                return self.ok(r)
            if method == 'sendMessage':
                return self.ok(new_msg(text=params['text']))
            if method == 'sendDocument':
                f = params['document']; data = f.file.read(); S['UP'] += 1
                return self.ok(new_msg(caption=params.get('caption'), document=store(data, f.filename)))
            if method == 'getFile':
                fid = params['file_id']; path = S['FILES'].get(fid)
                if not path: return self.err(400, 'Bad Request: invalid file_id')
                return self.ok({'file_id': fid, 'file_path': path, 'file_size': len(S['FILES'][path])})
            if method == 'editMessageText':
                m = MSGS.get(int(params['message_id']))
                if not m: return self.err(400, 'Bad Request: message to edit not found')
                if 'text' not in m: return self.err(400, 'Bad Request: there is no text in the message to edit')
                if m['text'] == params['text']: return self.err(400, 'Bad Request: message is not modified')
                m['text'] = params['text']; m['edit_date'] = int(time.time()); return self.ok(m)
            if method == 'editMessageMedia':
                m = MSGS.get(int(params['message_id']))
                if not m or 'document' not in m: return self.err(400, 'Bad Request: message to edit not found')
                media = json.loads(params['media']); att = media['media'].split('attach://')[1]; f = params[att]
                data = f.file.read(); S['UP'] += 1
                m['document'] = store(data, f.filename); m['caption'] = media.get('caption'); m['edit_date'] = int(time.time()); return self.ok(m)
            if method == 'pinChatMessage':
                mid = int(params['message_id'])
                if mid not in MSGS: return self.err(400, 'Bad Request: message to pin not found')
                S['PIN'] = mid; return self.ok(True)
            if method == 'deleteMessage':
                mid = int(params['message_id'])
                if mid not in MSGS: return self.err(400, 'Bad Request: message to delete not found')
                if time.time() - MSGS[mid]['date'] > 48 * 3600: return self.err(400, "Bad Request: message can't be deleted for everyone")
                MSGS.pop(mid)
                if S['PIN'] == mid: S['PIN'] = None
                return self.ok(True)
        self.err(404, 'Not Found: method')
ThreadingHTTPServer(('127.0.0.1', int(sys.argv[1])), H).serve_forever()
