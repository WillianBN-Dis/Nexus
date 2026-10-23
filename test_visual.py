import tempfile,unittest,sqlite3
from pathlib import Path
from core import Store

class VisualDataTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.store=Store(Path(self.tmp.name)/'data');self.store.register('visual','temporary12345');self.store.login('visual','temporary12345')
        self.ident=self.store.create('Teste');self.table='clientes " especiais'
        self.store.create_table(self.ident,self.table,[{'name':'nome','type':'TEXT','required':True},{'name':'idade','type':'INTEGER'}])
    def test_crud_sort_and_conflicts(self):
        s=self.store;i=self.ident;t=self.table
        s.insert_record(i,t,{'nome':"Ana'); DROP TABLE clientes; --",'idade':30})
        s.insert_record(i,t,{'nome':'Bia','idade':20})
        info=s.browse_table(i,t,'idade');self.assertEqual(info['rows'][0]['values'][1],'Bia')
        original=info['rows'][0];s.change_record(i,t,original,{'nome':'Beatriz','idade':21})
        with self.assertRaises(ValueError):s.change_record(i,t,original,{'nome':'stale'})
        row=s.browse_table(i,t,'idade')['rows'][0];s.change_record(i,t,row,delete=True)
        self.assertEqual(len(s.browse_table(i,t)['rows']),1)
        s.add_column(i,t,'email " contato','TEXT')
        self.assertEqual(s.table_info(i,t)['columns'][-1]['name'],'email " contato')
        s.drop_table(i,t);self.assertEqual(s.tables(i),[])
    def test_pagination_and_isolation(self):
        s=self.store;i=self.ident;t=self.table
        with s.visual_connection(i) as c:c.executemany('INSERT INTO '+s.quote_name(t)+'(nome,idade) VALUES (?,?)',[(str(n),n) for n in range(205)])
        first=s.browse_table(i,t,'idade',True);second=s.browse_table(i,t,'idade',True,200)
        self.assertEqual(len(first['rows']),200);self.assertTrue(first['more']);self.assertEqual(len(second['rows']),5)
        self.assertEqual(first['rows'][0]['values'][2],204)
        s.register('other','temporary12345');s.login('other','temporary12345')
        with self.assertRaises(ValueError):s.tables(i)
    def test_keys_and_foreign_keys(self):
        s=self.store;i=self.ident
        s.execute(i,'CREATE TABLE pairs(a TEXT,b TEXT,PRIMARY KEY(a,b)) WITHOUT ROWID')
        s.insert_record(i,'pairs',{'a':'x','b':'y'})
        row=s.browse_table(i,'pairs')['rows'][0];s.change_record(i,'pairs',row,{'b':'z'})
        self.assertEqual(s.browse_table(i,'pairs')['rows'][0]['values'],('x','z'))
        s.execute(i,'CREATE TABLE child(parent INTEGER REFERENCES '+s.quote_name(self.table)+'(id))')
        with self.assertRaises(sqlite3.IntegrityError):s.insert_record(i,'child',{'parent':999})
        with self.assertRaises(sqlite3.IntegrityError):s.insert_record(i,self.table,{'nome':None})
    def test_quoted_identifiers_and_invalid_types(self):
        with self.assertRaises(ValueError):self.store.create_table(self.ident,'unsafe',[{'name':'x','type':'TEXT); DROP TABLE x;--'}])
        self.assertIn(self.table,self.store.tables(self.ident))
        with self.assertRaises(ValueError):self.store.browse_table(self.ident,self.table,'missing')
if __name__=='__main__':unittest.main()
