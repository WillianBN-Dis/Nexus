import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
from core import Store
root=Path(sys.argv[1]);s=Store(root)
if len(sys.argv)>2:
    s.login('csharp','Csharp-Teste-123!')
    print('PASS: conta C# validada pelo Python original.')
else:
    s.register('usuário.teste','Nexus-Teste-123!ç');s.login('usuário.teste','Nexus-Teste-123!ç')
    ident=s.create('Banco legado');s.execute(ident,'CREATE TABLE clientes(nome TEXT)')
    s.execute(ident,"INSERT INTO clientes VALUES ('Ana'),('Bia')")
    print('Fixture Python temporária criada.')
