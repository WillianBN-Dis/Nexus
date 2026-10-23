import tkinter as tk
from tkinter import ttk, messagebox, simpledialog, filedialog
from core import Store

class Nexus(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title('Nexus • Database Studio')
        self.geometry('1160x760'); self.minsize(900,620)
        self.configure(bg='#101522')
        self.store = Store(); self.failures=0
        style=ttk.Style(self); style.theme_use('clam')
        style.configure('.',font=('Segoe UI',11),background='#161e30',foreground='#e5edf9')
        style.configure('TButton',padding=9,background='#273957')
        style.map('TButton',background=[('active','#38577f')])
        style.configure('TEntry',fieldbackground='#25334b',foreground='#ffffff',padding=8)
        style.configure('Treeview',background='#161e30',fieldbackground='#161e30',foreground='#e5edf9',rowheight=31)
        style.configure('Treeview.Heading',background='#273957',foreground='white')
        self.login_screen()
    def clear(self):
        for w in self.winfo_children(): w.destroy()
    def label(self,parent,text,size=12):
        return tk.Label(parent,text=text,bg='#101522',fg='#dce8ff',font=('Segoe UI',size))
    def guard(self, action):
        try: return action()
        except Exception as e: messagebox.showerror('Nexus',str(e),parent=self)
    def login_screen(self):
        self.store.user=None; self.clear()
        box=tk.Frame(self,bg='#101522'); box.pack(expand=True)
        self.label(box,'◈ NEXUS',34).pack(pady=15)
        self.label(box,'Seus dados. Seu espaço.',15).pack(pady=8)
        self.label(box,'Usuário').pack(anchor='w',pady=(24,4))
        name=ttk.Entry(box,width=34); name.pack()
        self.label(box,'Senha').pack(anchor='w',pady=(12,4))
        password=ttk.Entry(box,width=34,show='•'); password.pack()
        def authenticate():
            try:
                self.store.login(name.get(),password.get()); self.failures=0; self.dashboard()
            except Exception as e:
                self.failures+=1; messagebox.showerror('Login',str(e))
                if self.failures>=5:
                    enter.state(['disabled']); self.after(30000,lambda: enter.state(['!disabled']) if enter.winfo_exists() else None)
        enter=ttk.Button(box,text='Entrar no Nexus',command=authenticate); enter.pack(fill='x',pady=(22,8))
        def register():
            def action():
                self.store.register(name.get(),password.get()); messagebox.showinfo('Nexus','Conta criada. Agora clique em Entrar.')
            self.guard(action)
        ttk.Button(box,text='Criar conta local',command=register).pack(fill='x')
        self.label(box,'Senha: mínimo de 10 caracteres.\nContas locais neste computador.',10).pack(pady=20)
        name.focus_set()
    def dashboard(self):
        self.clear()
        header=tk.Frame(self,bg='#101522'); header.pack(fill='x',padx=24,pady=18)
        self.label(header,'◈ NEXUS',24).pack(side='left')
        ttk.Button(header,text='Sair da conta',command=self.login_screen).pack(side='right')
        self.label(self,'DATABASE STUDIO  /  Meus bancos',14).pack(anchor='w',padx=24,pady=8)
        toolbar=tk.Frame(self,bg='#101522'); toolbar.pack(fill='x',padx=24,pady=10)
        for text,cmd in [('＋ Criar banco',self.create),('Renomear',self.rename),('Mover para outro disco',self.move),('Excluir',self.delete),('Atualizar',self.refresh)]:
            ttk.Button(toolbar,text=text,command=lambda c=cmd:self.guard(c)).pack(side='left',padx=(0,8))
        self.banks=ttk.Treeview(self,columns=('name','path'),show='headings',height=5,selectmode='browse')
        self.banks.heading('name',text='Banco de dados'); self.banks.heading('path',text='Localização no disco')
        self.banks.column('name',width=240); self.banks.column('path',width=760)
        self.banks.pack(fill='x',padx=24)
        self.label(self,'Editor SQL  •  Uma instrução por execução',12).pack(anchor='w',padx=24,pady=(16,6))
        self.sql=tk.Text(self,height=6,bg='#161e30',fg='#9dc9ff',insertbackground='white',font=('Consolas',12),undo=True)
        self.sql.pack(fill='x',padx=24); self.sql.insert('1.0',"SELECT name, type FROM sqlite_master ORDER BY name;")
        bar=tk.Frame(self,bg='#101522'); bar.pack(fill='x',padx=24,pady=8)
        ttk.Button(bar,text='▶ Executar SQL',command=lambda:self.guard(self.run)).pack(side='left')
        self.status=self.label(bar,'Selecione um banco para começar.'); self.status.pack(side='left',padx=16)
        results=ttk.Frame(self); results.pack(fill='both',expand=True,padx=24,pady=(0,18))
        self.result=ttk.Treeview(results,show='headings'); self.result.grid(row=0,column=0,sticky='nsew')
        sy=ttk.Scrollbar(results,orient='vertical',command=self.result.yview); sy.grid(row=0,column=1,sticky='ns')
        sx=ttk.Scrollbar(results,orient='horizontal',command=self.result.xview); sx.grid(row=1,column=0,sticky='ew')
        self.result.configure(yscrollcommand=sy.set,xscrollcommand=sx.set)
        results.rowconfigure(0,weight=1); results.columnconfigure(0,weight=1)
        self.refresh()
    def selected(self):
        rows=self.banks.selection()
        if not rows: raise ValueError('Selecione um banco na lista.')
        return rows[0]
    def refresh(self):
        self.banks.delete(*self.banks.get_children())
        for ident,name,path in self.store.list(): self.banks.insert('', 'end',iid=ident,values=(name,path))
    def create(self):
        name=simpledialog.askstring('Novo banco','Nome do banco:',parent=self)
        if name:
            ident=self.store.create(name); self.refresh(); self.banks.selection_set(ident)
    def rename(self):
        ident=self.selected(); name=simpledialog.askstring('Renomear','Novo nome:',parent=self,initialvalue=self.store.get(ident)[1])
        if name: self.store.rename(ident,name); self.refresh()
    def move(self):
        ident=self.selected(); folder=filedialog.askdirectory(title='Selecione a pasta no disco de destino')
        if folder and messagebox.askyesno('Mover banco','Feche outros programas que estejam usando este banco. Continuar?'):
            messagebox.showinfo('Nexus',self.store.move(ident,folder)); self.refresh()
    def delete(self):
        ident=self.selected(); name=self.store.get(ident)[1]
        if messagebox.askyesno('Excluir banco',f'Enviar o banco “{name}” e todos os seus dados para a Lixeira?'):
            self.store.delete(ident); self.refresh()
    def run(self):
        ident=self.selected(); sql=self.sql.get('1.0','end').strip()
        if not sql: return
        if not sql.upper().startswith(('SELECT','EXPLAIN')) and not messagebox.askyesno('Executar SQL','Esta instrução pode alterar ou excluir dados. Executar?'): return
        columns,rows,truncated=self.store.execute(ident,sql)
        self.result.delete(*self.result.get_children()); keys=[str(i) for i in range(len(columns))]; self.result['columns']=keys
        for key,title in zip(keys,columns): self.result.heading(key,text=title); self.result.column(key,width=180,stretch=False)
        for row in rows: self.result.insert('','end',values=['NULL' if v is None else str(v) for v in row])
        self.status.config(text=f'{len(rows)} linha(s)'+(' • limitado a 1.000 resultados' if truncated else ' • concluído'))

if __name__=='__main__': Nexus().mainloop()
