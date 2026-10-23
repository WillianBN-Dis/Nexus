import tempfile
from pathlib import Path
from core import Store
from nexus import Nexus

def settle(app):
    app.after(700,app.quit)
    app.mainloop()

def main():
    with tempfile.TemporaryDirectory() as tmp:
        app=Nexus(store=Store(Path(tmp)/'data'))
        for geometry in ['1040x740','1280x850','1920x1080','2560x1440','1280x850']:
            app.geometry(geometry)
            settle(app)
            print(geometry,'scale',app.layout_scale,'form',app.login_form.winfo_width(),app.login_form.winfo_height(),flush=True)
            assert app.login_form.winfo_rooty()>=app.winfo_rooty()
            assert app.login_form.winfo_rooty()+app.login_form.winfo_height()<=app.winfo_rooty()+app.winfo_height()
            assert app.login_form.winfo_width()<=700
        assert app.layout_scale==1
        app.state('zoomed');settle(app)
        assert app.layout_scale>=1
        if '--dashboard' in __import__('sys').argv:
            app.store.register('layout','temporary12345'); app.store.login('layout','temporary12345')
            app.username='Layout'
            ident=app.store.create('Projeto Nexus')
            app.dashboard();app.workspace.set('Editor SQL');settle(app)
            app.banks.selection_set(ident);app.on_selection();app.update()
            app.sql.delete('1.0','end');app.sql.insert('1.0',"SELECT 'Nexus' AS projeto, 42 AS resultado")
            app.run();app.result_tabs.set('Tabela');settle(app)
            assert app.result.winfo_height()>150
        if '--preview' in __import__('sys').argv:
            app.after(90000,app.destroy);app.mainloop()
        else: app.destroy()
if __name__=='__main__': main()
