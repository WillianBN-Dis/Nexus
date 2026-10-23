import tempfile
from pathlib import Path
from core import Store
from nexus import Nexus

def main():
    with tempfile.TemporaryDirectory() as tmp:
        app=Nexus(store=Store(Path(tmp)/'data'))
        app.update()
        app.store.register('interface','temporary12345')
        app.store.login('interface','temporary12345')
        app.username='interface'
        ident=app.store.create('Meu primeiro banco')
        app.dashboard(); app.update()
        app.banks.selection_set(ident); app.on_selection(); app.update()
        app.sql.delete('1.0','end'); app.sql.insert('1.0','SELECT 42 AS resposta')
        app.highlight_sql(); app.run(); app.update()
        assert len(app.result.get_children())==1
        app.search_var.set('inexistente'); app.update()
        assert not app.banks.get_children()
        app.geometry('1040x740'); app.update()
        assert app.sql.tag_ranges('keyword')
        from ui import ASSETS,style_window
        from PIL import Image
        assert Image.open(ASSETS/'nexus.ico').info['sizes'] >= {(16,16),(32,32),(256,256)}
        style_window(app)
        if '--preview' in __import__('sys').argv:
            app.search_var.set(''); app.update()
            app.banks.selection_set(ident); app.on_selection(); app.update()
            app.geometry('1280x850')
            app.after(90000,app.destroy)
            app.mainloop()
            return
        app.destroy()
        print('UI OK: login, painel, seleção, consulta, filtro e janela mínima')
if __name__=='__main__': main()
