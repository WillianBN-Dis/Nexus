from pathlib import Path
import os, sqlite3, hashlib, secrets, hmac, uuid, re
from contextlib import closing, contextmanager

class Store:
    def __init__(self, root=None):
        self.root = Path(root or Path(os.getenv('LOCALAPPDATA', Path.home())) / 'Nexus')
        self.root.mkdir(parents=True, exist_ok=True)
        self.catalog = self.root / 'catalog.sqlite'
        self.user = None
        with self.meta() as c:
            c.executescript('CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY,name TEXT UNIQUE,salt BLOB,hash BLOB); CREATE TABLE IF NOT EXISTS databases(id TEXT PRIMARY KEY,owner TEXT,name TEXT,path TEXT UNIQUE);')
    @contextmanager
    def meta(self):
        with closing(sqlite3.connect(self.catalog)) as connection:
            with connection:
                yield connection
    def register(self, name, password):
        name = name.strip()
        if not re.fullmatch(r'[\w.@-]{3,64}', name): raise ValueError('Usuário: 3 a 64 letras, números ou . @ _ -')
        if len(password) < 10: raise ValueError('Use uma senha com pelo menos 10 caracteres.')
        salt = secrets.token_bytes(32)
        digest = hashlib.scrypt(password.encode(),salt=salt,n=16384,r=8,p=1)
        with self.meta() as c:
            try: c.execute('INSERT INTO users VALUES(?,?,?,?)',(uuid.uuid4().hex,name,salt,digest))
            except sqlite3.IntegrityError: raise ValueError('Esse usuário já existe.')
    def login(self, name, password):
        self.user = None
        with self.meta() as c: row = c.execute('SELECT id,salt,hash FROM users WHERE name=?',(name.strip(),)).fetchone()
        salt = row[1] if row else bytes(32)
        digest = hashlib.scrypt(password.encode(),salt=salt,n=16384,r=8,p=1)
        if not row or not hmac.compare_digest(digest,row[2]): raise ValueError('Usuário ou senha incorretos.')
        self.user = row[0]
    def require(self):
        if not self.user: raise ValueError('Faça login para continuar.')
    def list(self):
        self.require()
        with self.meta() as c: return c.execute('SELECT id,name,path FROM databases WHERE owner=? ORDER BY name',(self.user,)).fetchall()
    def get(self, ident):
        self.require()
        with self.meta() as c: row = c.execute('SELECT id,name,path FROM databases WHERE id=? AND owner=?',(ident,self.user)).fetchone()
        if not row: raise ValueError('Banco não encontrado para este usuário.')
        return row
    def create(self, name):
        self.require()
        if not name.strip() or len(name)>100: raise ValueError('Informe um nome com até 100 caracteres.')
        ident = uuid.uuid4().hex
        folder = self.root / 'databases' / self.user
        folder.mkdir(parents=True,exist_ok=True)
        path = folder / (ident+'.sqlite')
        with closing(sqlite3.connect(path)) as c:
            c.execute('PRAGMA user_version=1')
        with self.meta() as c: c.execute('INSERT INTO databases VALUES(?,?,?,?)',(ident,self.user,name.strip(),str(path)))
        return ident
    def rename(self, ident, name):
        self.get(ident)
        if not name.strip() or len(name)>100: raise ValueError('Nome inválido.')
        with self.meta() as c: c.execute('UPDATE databases SET name=? WHERE id=?',(name.strip(),ident))
    def execute(self, ident, sql):
        path = Path(self.get(ident)[2])
        if not path.is_file(): raise ValueError('Arquivo indisponível. Conecte o disco do banco.')
        with closing(sqlite3.connect(f'file:{path.as_posix()}?mode=rw',uri=True)) as c:
            # Confine SQL to the selected database; never allow access to the account catalog.
            denied = {sqlite3.SQLITE_ATTACH,sqlite3.SQLITE_DETACH,sqlite3.SQLITE_PRAGMA}
            c.set_authorizer(lambda action,*args: sqlite3.SQLITE_DENY if action in denied else sqlite3.SQLITE_OK)
            c.set_progress_handler(lambda: 1 if __import__('time').monotonic()>deadline else 0,1000)
            deadline = __import__('time').monotonic()+5
            with c:
                cursor = c.execute(sql)
                if cursor.description:
                    rows = cursor.fetchmany(1001)
                    return [d[0] for d in cursor.description],rows[:1000],len(rows)>1000
                return ['Resultado'],[(f'{cursor.rowcount if cursor.rowcount>=0 else 0} registro(s) afetado(s).',)],False
    def move(self, ident, folder):
        source = Path(self.get(ident)[2])
        if not source.is_file(): raise ValueError('Disco de origem indisponível.')
        folder = Path(folder).resolve(strict=True)
        target = folder / source.name
        if target.exists(): raise ValueError('Já existe um arquivo com esse identificador no destino.')
        # Copy with SQLite backup, check integrity, then publish the catalog path before cleanup.
        target.touch(exist_ok=False)
        try:
            with closing(sqlite3.connect(source)) as src, closing(sqlite3.connect(target)) as dst:
                src.backup(dst)
                if dst.execute('PRAGMA integrity_check').fetchone()[0]!='ok': raise ValueError('Falha de integridade na cópia.')
            with self.meta() as c: c.execute('UPDATE databases SET path=? WHERE id=?',(str(target),ident))
        except Exception:
            target.unlink(missing_ok=True)
            raise
        try: source.unlink()
        except OSError: return 'Banco movido. A cópia antiga não pôde ser removida: '+str(source)
        return 'Banco movido e verificado com sucesso.'
    def delete(self, ident):
        source = Path(self.get(ident)[2])
        if not source.is_file(): raise ValueError('Conecte o disco antes de excluir este banco.')
        # Trash keeps accidental deletion recoverable through the OS.
        from send2trash import send2trash
        send2trash(str(source))
        with self.meta() as c: c.execute('DELETE FROM databases WHERE id=?',(ident,))
