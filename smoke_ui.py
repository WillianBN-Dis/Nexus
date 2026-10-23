import tempfile
from pathlib import Path
from core import Store
from nexus import Nexus

def main():
    with tempfile.TemporaryDirectory() as tmp:
        app=Nexus(store=Store(Path(tmp)/'data'))
        app.update()
        assert app.state()=='normal'
        assert app.winfo_x()>=0 and app.winfo_y()>=0
        app.store.register('interface','temporary12345')
        app.store.login('interface','temporary12345')
        app.username='interface'
        ident=app.store.create('Meu primeiro banco')
        app.dashboard(); app.update()
        app.banks.selection_set(ident); app.on_selection(); app.update()
        app.sql.delete('1.0','end'); app.sql.insert('1.0','SELECT 42 AS resposta')
        app.highlight_sql(); app.run(); app.update()
        assert len(app.result.get_children())==1
        assert 'resposta: 42' in app.result_text.get('1.0','end')
        app.result_tabs.set('Tabela'); app.update()
        assert app.result.item(app.result.get_children()[0])['values']==[42]
        app.result_tabs.set('Resultado')
        dialog=app.create(); app.update()
        original_count=len(app.store.list())
        dialog.submit(); app.update()
        assert dialog.winfo_exists() and len(app.store.list())==original_count
        assert 'Informe' in dialog.feedback.cget('text')
        dialog.name.insert(0,'Banco criado pelo formulário')
        dialog.submit(); app.update()
        assert not dialog.winfo_exists() and len(app.store.list())==original_count+1
        cancelled=app.create(); app.update(); cancelled.close(); app.update()
        assert len(app.store.list())==original_count+1
        app.banks.selection_set(ident); app.on_selection(); app.update()
        app.sql.delete('1.0','end'); app.sql.insert('1.0','SELECT * FROM missing_table')
        try: app.run()
        except Exception: pass
        else: raise AssertionError('SQL inválido deveria falhar')
        assert 'Falha ao executar' in app.result_text.get('1.0','end')
        assert not app.result.get_children()
        app.sql.delete('1.0','end'); app.sql.insert('1.0','SELECT 42 AS resposta')
        app.highlight_sql(); app.run(); app.update()
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
            if '--dialog' in __import__('sys').argv: app.create()
            else:
                app.run(); app.result_tabs.set('Tabela')
            app.after(90000,app.destroy)
            app.mainloop()
            return
        app.destroy()
        print('UI OK: abertura, criação/cancelamento/validação, consulta, abas, falha SQL, filtro e janela mínima')
if __name__=='__main__': main()
