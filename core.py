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

    @staticmethod
    def quote_name(name):
        if not isinstance(name,str) or not name.strip() or '\x00' in name or len(name)>128:
            raise ValueError('Use um nome de 1 a 128 caracteres.')
        return '"'+name.replace('"','""')+'"'

    @contextmanager
    def visual_connection(self,ident):
        path=Path(self.get(ident)[2])
        if not path.is_file(): raise ValueError('Conecte o disco onde este banco está salvo.')
        with closing(sqlite3.connect(path.resolve().as_uri()+'?mode=rw',uri=True)) as connection:
            connection.execute('PRAGMA foreign_keys=ON')
            import time
            deadline=time.monotonic()+5
            connection.set_progress_handler(lambda: int(time.monotonic()>deadline),1000)
            with connection: yield connection

    def tables(self,ident):
        with self.visual_connection(ident) as c:
            return [row[0] for row in c.execute("SELECT name FROM sqlite_schema WHERE type='table' AND substr(name,1,7)!='sqlite_' ORDER BY name")]

    def table_info(self,ident,table):
        with self.visual_connection(ident) as c: return self._table_info(c,table)

    def _table_info(self,c,table):
        quoted=self.quote_name(table)
        if not c.execute("SELECT 1 FROM sqlite_schema WHERE type='table' AND name=? AND substr(name,1,7)!='sqlite_'",(table,)).fetchone():
            raise ValueError('Tabela não encontrada. Atualize a lista.')
        columns=[dict(zip(('cid','name','type','required','default','pk','hidden'),row)) for row in c.execute(f'PRAGMA table_xinfo({quoted})')]
        identity=[]
        names={col['name'].casefold() for col in columns}
        for alias in ('rowid','_rowid_','oid'):
            if alias in names: continue
            try: c.execute(f'SELECT {alias} FROM {quoted} LIMIT 0')
            except sqlite3.OperationalError: break
            identity=[alias]; break
        if not identity:
            identity=[col['name'] for col in sorted(columns,key=lambda col:col['pk']) if col['pk']]
        return {'columns':[col for col in columns if col['hidden']!=1], 'identity':identity}

    def create_table(self,ident,table,columns):
        quoted=self.quote_name(table.strip())
        if table.strip().casefold().startswith('sqlite_'): raise ValueError('O prefixo sqlite_ é reservado.')
        definitions=['"id" INTEGER PRIMARY KEY']
        names={'id'}
        if not columns: raise ValueError('Adicione pelo menos uma coluna além do ID automático.')
        for column in columns:
            name=column['name'].strip(); kind=column['type'].upper()
            if name.casefold() in names: raise ValueError('Existem nomes de coluna repetidos: '+name)
            if kind not in ('TEXT','INTEGER','REAL','BLOB'): raise ValueError('Tipo inválido.')
            names.add(name.casefold())
            definitions.append(self.quote_name(name)+' '+kind+(' NOT NULL' if column.get('required') else ''))
        with self.visual_connection(ident) as c: c.execute(f'CREATE TABLE {quoted} ({", ".join(definitions)})')

    def add_column(self,ident,table,name,kind):
        if kind not in ('TEXT','INTEGER','REAL','BLOB'): raise ValueError('Tipo inválido.')
        with self.visual_connection(ident) as c:
            self._table_info(c,table)
            c.execute(f'ALTER TABLE {self.quote_name(table)} ADD COLUMN {self.quote_name(name.strip())} {kind}')

    def drop_table(self,ident,table):
        with self.visual_connection(ident) as c:
            self._table_info(c,table)
            c.execute(f'DROP TABLE {self.quote_name(table)}')

    def browse_table(self,ident,table,sort=None,descending=False,offset=0):
        with self.visual_connection(ident) as c:
            info=self._table_info(c,table)
            names=[col['name'] for col in info['columns']]
            if sort is not None and sort not in names: raise ValueError('Coluna de ordenação inválida.')
            identities=info['identity']
            fields=[self.quote_name(name) for name in identities+names]
            order=f' ORDER BY {self.quote_name(sort)} '+('DESC' if descending else 'ASC') if sort else ''
            rows=c.execute(f'SELECT {", ".join(fields)} FROM {self.quote_name(table)}{order} LIMIT 201 OFFSET ?',(max(0,int(offset)),)).fetchall()
            info.update(rows=[{'key':row[:len(identities)],'values':row[len(identities):]} for row in rows[:200]],more=len(rows)>200)
            return info

    def _checked_values(self,info,values):
        editable={col['name'] for col in info['columns'] if not col['hidden']}
        if not set(values)<=editable: raise ValueError('Coluna desconhecida ou calculada.')
        return values

    def insert_record(self,ident,table,values):
        with self.visual_connection(ident) as c:
            self._checked_values(self._table_info(c,table),values)
            if values:
                names=', '.join(self.quote_name(name) for name in values)
                marks=', '.join('?' for _ in values)
                c.execute(f'INSERT INTO {self.quote_name(table)} ({names}) VALUES ({marks})',list(values.values()))
            else: c.execute(f'INSERT INTO {self.quote_name(table)} DEFAULT VALUES')

    def change_record(self,ident,table,record,values=None,delete=False):
        with self.visual_connection(ident) as c:
            info=self._table_info(c,table)
            if not info['identity']: raise ValueError('Esta tabela não tem uma identificação segura para editar linhas.')
            names=[col['name'] for col in info['columns']]
            if len(record['key'])!=len(info['identity']) or len(record['values'])!=len(names):
                raise ValueError('A estrutura mudou. Atualize a tabela.')
            # Include the original values to reject stale edits from another window/program.
            where=' AND '.join(self.quote_name(name)+' IS ?' for name in info['identity']+names)
            arguments=list(record['key'])+list(record['values'])
            if delete: sql=f'DELETE FROM {self.quote_name(table)} WHERE {where}'
            else:
                self._checked_values(info,values)
                if not values: return
                assignments=', '.join(self.quote_name(name)+'=?' for name in values)
                sql=f'UPDATE {self.quote_name(table)} SET {assignments} WHERE {where}'
                arguments=list(values.values())+arguments
            result=c.execute(sql,arguments)
            if result.rowcount!=1: raise ValueError('O registro mudou ou foi removido. Atualize antes de tentar novamente.')
