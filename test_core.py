import tempfile, unittest, sqlite3
from pathlib import Path
from core import Store
class Tests(unittest.TestCase):
 def test_lifecycle_and_isolation(self):
  with tempfile.TemporaryDirectory() as tmp:
   s=Store(Path(tmp)/'app'); s.register('alice','password12345'); s.login('alice','password12345')
   ident=s.create('Clientes'); s.execute(ident,'CREATE TABLE clients(name TEXT)'); s.execute(ident,"INSERT INTO clients VALUES ('Ana')")
   s.execute(ident,"UPDATE clients SET name='Maria'")
   old=Path(s.get(ident)[2]); target=Path(tmp)/'disk'; target.mkdir(); s.move(ident,target)
   self.assertFalse(old.exists()); self.assertEqual(s.execute(ident,'SELECT * FROM clients')[1],[('Maria',)])
   s.rename(ident,'Pessoas'); self.assertEqual(s.get(ident)[1],'Pessoas')
   with self.assertRaises(sqlite3.DatabaseError): s.execute(ident,"ATTACH DATABASE ':memory:' AS other")
   s.register('bobby','password12345'); s.login('bobby','password12345'); self.assertEqual(s.list(),[])
   with self.assertRaises(ValueError): s.get(ident)
   with self.assertRaises(ValueError): s.login('alice','wrong')
   self.assertIsNone(s.user)
if __name__=='__main__': unittest.main()
