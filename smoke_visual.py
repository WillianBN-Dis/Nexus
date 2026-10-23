import tempfile
from pathlib import Path
from unittest.mock import patch
from core import Store
from nexus import Nexus

def main():
    with tempfile.TemporaryDirectory() as tmp:
        app=Nexus(store=Store(Path(tmp)/'data'))
        app.store.register('visual','temporary12345');app.store.login('visual','temporary12345');app.username='Visual'
        ident=app.store.create('Projeto visual');app.dashboard();app.update()
        app.banks.selection_set(ident);app.on_selection();app.update()
        v=app.visual
        dialog=v.create_table();app.update()
        dialog.name.insert(0,'clientes')
        dialog.rows[0][1].insert(0,'nome')
        dialog.add_field();dialog.rows[1][1].insert(0,'idade');dialog.rows[1][2].set('INTEGER')
        dialog.submit();app.update()
        assert v.table=='clientes'
        dialog=v.insert();app.update()
        for col,entry,mode in dialog.fields:
            if col['name']=='nome':entry.insert(0,'Ana')
            if col['name']=='idade':entry.insert(0,'inválido')
        dialog.submit();app.update()
        assert dialog.winfo_exists() and not v.records
        for col,entry,mode in dialog.fields:
            if col['name']=='idade':entry.delete(0,'end');entry.insert(0,'30')
        dialog.submit();app.update()
        assert len(v.records)==1
        assert v.actions[1].cget('state')=='disabled'
        assert v.actions[2].cget('state')=='disabled'
        v.data_grid.selection_set('0');app.update()
        assert v.actions[1].cget('state')=='normal'
        dialog=v.edit();app.update()
        for col,entry,mode in dialog.fields:
            if col['name']=='nome':entry.delete(0,'end');entry.insert(0,'Ana Maria')
        dialog.submit();app.update()
        assert v.records['0']['values'][1]=='Ana Maria'
        dialog=v.add_column();app.update();dialog.name.insert(0,'email');dialog.submit();app.update()
        assert v.info['columns'][-1]['name']=='email'
        v.order_by('idade');assert not v.descending
        v.order_by('idade');assert v.descending
        app.workspace.set('Editor SQL');app.sql.delete('1.0','end');app.sql.insert('1.0','SELECT nome FROM clientes');app.run();app.update()
        assert 'Ana Maria' in app.result_text.get('1.0','end')
        app.workspace.set('Visual');v.refresh_tables();app.update()
        app.after(200,app.quit);app.mainloop()
        assert v.data_grid.winfo_viewable(), 'Visual deve permanecer visível após troca rápida de abas'
        if '--preview' in __import__('sys').argv:
            app.geometry('1280x850') if '--compact' in __import__('sys').argv else app.state('zoomed')
            if '--form' in __import__('sys').argv:v.create_table()
            app.after(120000,app.destroy);app.mainloop();return
        v.data_grid.selection_set('0')
        with patch('visual.messagebox.askyesno',return_value=True):v.delete()
        assert not v.records
        dialog=v.drop();app.update();dialog.name.insert(0,'clientes');dialog.submit();app.update()
        assert app.store.tables(ident)==[]
        app.destroy()
        print('Visual UI OK: tabela, coluna, formulário, validação, edição, ordenação, SQL e exclusões')
if __name__=='__main__':main()
