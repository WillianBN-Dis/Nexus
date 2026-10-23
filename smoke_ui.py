import tempfile
from pathlib import Path
from core import Store
from nexus import Nexus

def main():
    with tempfile.TemporaryDirectory() as tmp:
        app=Nexus()
        app.store=Store(Path(tmp)/'data')
        app.update()
        app.store.register('interface','temporary12345')
        app.store.login('interface','temporary12345')
        app.username='interface'
        ident=app.store.create('Meu primeiro banco')
        app.dashboard(); app.update()
        app.banks.selection_set(ident); app.on_selection(); app.update()
        app.sql.delete('1.0','end'); app.sql.insert('1.0','SELECT 42 AS resposta')
        app.run(); app.update()
        assert len(app.result.get_children())==1
        app.search_var.set('inexistente'); app.update()
        assert not app.banks.get_children()
        app.geometry('1000x700'); app.update()
        app.destroy()
        print('UI OK: login, painel, seleção, consulta, filtro e janela mínima')
if __name__=='__main__': main()
