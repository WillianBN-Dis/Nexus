import tkinter as tk
from tkinter import ttk, messagebox, simpledialog, filedialog
from core import Store

BG = '#0c111b'
PANEL = '#121b29'
SURFACE = '#192537'
TEXT = '#e5edf6'
MUTED = '#8fa2ba'
ACCENT = '#57dfb5'

class Nexus(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title('Nexus • Database Studio')
        self.geometry('1240x820')
        self.minsize(1000,700)
        self.configure(bg=BG)
        self.store=Store()
        self.failures=0
        self.username=''
        self.cooldown=False
        self.setup_styles()
        self.bind('<Control-Return>',lambda event:self.guard(self.run) if self.store.user else None)
        self.login_screen()

    def setup_styles(self):
        style=ttk.Style(self)
        style.theme_use('clam')
        style.configure('.',font=('Segoe UI',10),background=PANEL,foreground=TEXT)
        style.configure('TButton',padding=(15,10),background=SURFACE,foreground=TEXT,borderwidth=0,focusthickness=0)
        style.map('TButton',background=[('active','#273b53'),('disabled',PANEL)],foreground=[('disabled','#65758a')])
        style.configure('Primary.TButton',background=ACCENT,foreground='#08271f',font=('Segoe UI Semibold',10))
        style.map('Primary.TButton',background=[('active','#85edcd'),('disabled','#31594f')],foreground=[('disabled','#849c95')])
        style.configure('Danger.TButton',foreground='#ff9d9d')
        style.configure('TEntry',fieldbackground=SURFACE,foreground=TEXT,insertcolor=TEXT,padding=11,borderwidth=0)
        style.configure('Treeview',background=PANEL,fieldbackground=PANEL,foreground=TEXT,rowheight=38,borderwidth=0,font=('Segoe UI',10))
        style.map('Treeview',background=[('selected','#244b49')],foreground=[('selected','#8ff5d7')])
        style.configure('Treeview.Heading',background=SURFACE,foreground=MUTED,font=('Segoe UI Semibold',10),padding=9,borderwidth=0)
        style.map('Treeview.Heading',background=[('active',SURFACE)])
        style.configure('Vertical.TScrollbar',background=SURFACE,troughcolor=PANEL,borderwidth=0,arrowsize=12)
        style.configure('Horizontal.TScrollbar',background=SURFACE,troughcolor=PANEL,borderwidth=0,arrowsize=12)

    def clear(self):
        for widget in self.winfo_children(): widget.destroy()

    def label(self,parent,text,size=11,color=TEXT,bold=False,bg=None):
        return tk.Label(parent,text=text,bg=bg or parent.cget('bg'),fg=color,
                        font=('Segoe UI Semibold' if bold else 'Segoe UI',size),anchor='w',justify='left')

    def guard(self,action):
        try: return action()
        except Exception as error: messagebox.showerror('Nexus',str(error),parent=self)

    def login_screen(self):
        self.store.user=None
        self.clear()
        shell=tk.Frame(self,bg=BG)
        shell.pack(fill='both',expand=True,padx=48,pady=48)
        shell.columnconfigure(0,weight=1)
        shell.columnconfigure(1,weight=1)
        shell.rowconfigure(0,weight=1)
        brand=tk.Frame(shell,bg=PANEL,padx=38,pady=38)
        brand.grid(row=0,column=0,sticky='nsew',padx=(0,36))
        self.label(brand,'◈  NEXUS',23,ACCENT,True).pack(anchor='w')
        self.label(brand,'DATABASE STUDIO',10,MUTED).pack(anchor='w',pady=(8,0))
        middle=tk.Frame(brand,bg=PANEL)
        middle.pack(fill='both',expand=True)
        copy=tk.Frame(middle,bg=PANEL)
        copy.pack(expand=True,anchor='w')
        self.label(copy,'Um lugar para\nconectar suas ideias.',29,bold=True).pack(anchor='w')
        self.label(copy,'Crie, organize e explore seus bancos de dados\ncom tudo ao seu alcance.',11,MUTED).pack(anchor='w',pady=(18,30))
        for title,detail in [('01   Organize','Bancos separados por conta local.'),('02   Explore','SQL e resultados no mesmo espaço.'),('03   Mova','Transfira seus bancos entre discos.')]:
            self.label(copy,title,12,ACCENT,True).pack(anchor='w',pady=(12,4))
            self.label(copy,detail,10,MUTED).pack(anchor='w')
        self.label(brand,'SQLITE  /  LOCAL  /  WINDOWS',9,MUTED).pack(anchor='w')
        formhost=tk.Frame(shell,bg=BG)
        formhost.grid(row=0,column=1,sticky='nsew')
        form=tk.Frame(formhost,bg=BG)
        form.pack(expand=True,fill='x',padx=24)
        self.label(form,'SEU ESPAÇO DE TRABALHO',10,ACCENT,True).pack(anchor='w')
        self.label(form,'Bem-vindo ao Nexus',25,bold=True).pack(anchor='w',pady=(12,8))
        self.label(form,'Entre com sua conta ou crie uma para começar.',11,MUTED).pack(anchor='w',pady=(0,28))
        self.label(form,'Usuário',10,bold=True).pack(anchor='w',pady=(0,8))
        name=ttk.Entry(form)
        name.pack(fill='x')
        self.label(form,'Senha',10,bold=True).pack(anchor='w',pady=(20,8))
        password=ttk.Entry(form,show='●')
        password.pack(fill='x')
        show=tk.BooleanVar(value=False)
        tk.Checkbutton(form,text='Mostrar senha',variable=show,command=lambda:password.configure(show='' if show.get() else '●'),
                       bg=BG,fg=MUTED,selectcolor=SURFACE,activebackground=BG,activeforeground=TEXT,font=('Segoe UI',10),borderwidth=0).pack(anchor='w',pady=(8,20))
        feedback=self.label(form,'',10,'#ffaaaa')
        feedback.pack(anchor='w',pady=(0,8))
        def unlock():
            self.cooldown=False
            if enter.winfo_exists():
                enter.state(['!disabled'])
                feedback.config(text='Você já pode tentar novamente.')
        def authenticate():
            if self.cooldown: return
            try:
                self.store.login(name.get(),password.get())
                self.username=name.get().strip()
                self.failures=0
                self.dashboard()
            except Exception as error:
                self.failures+=1
                feedback.config(text=str(error))
                if self.failures>=5:
                    self.cooldown=True
                    enter.state(['disabled'])
                    feedback.config(text='Aguarde 30 segundos antes de tentar novamente.')
                    self.after(30000,unlock)
        enter=ttk.Button(form,text='Entrar no Nexus  →',style='Primary.TButton',command=authenticate)
        enter.pack(fill='x')
        password.bind('<Return>',lambda event:authenticate())
        def register():
            try:
                self.store.register(name.get(),password.get())
                feedback.config(text='Conta criada. Entre para começar.',fg=ACCENT)
            except Exception as error: feedback.config(text=str(error),fg='#ffaaaa')
        ttk.Button(form,text='Criar conta local',command=register).pack(fill='x',pady=(12,22))
        self.label(form,'Use pelo menos 10 caracteres na senha.\nSua conta fica neste computador.',10,MUTED).pack(anchor='w')
        name.focus_set()

    def dashboard(self):
        self.clear()
        sidebar=tk.Frame(self,bg=PANEL,width=280)
        sidebar.pack(side='left',fill='y')
        sidebar.pack_propagate(False)
        self.label(sidebar,'◈  NEXUS',22,ACCENT,True).pack(anchor='w',padx=24,pady=(26,4))
        self.label(sidebar,'DATABASE STUDIO',9,MUTED).pack(anchor='w',padx=26,pady=(0,30))
        self.label(sidebar,'ESPAÇO PESSOAL',9,MUTED,True).pack(anchor='w',padx=24)
        ttk.Button(sidebar,text='＋  Criar banco de dados',style='Primary.TButton',command=lambda:self.guard(self.create)).pack(fill='x',padx=20,pady=(14,24))
        self.bank_count=self.label(sidebar,'MEUS BANCOS',10,MUTED,True)
        self.bank_count.pack(anchor='w',padx=24,pady=(0,10))
        search=ttk.Entry(sidebar)
        search.pack(fill='x',padx=20,pady=(0,8))
        self.search_var=tk.StringVar()
        search.configure(textvariable=self.search_var)
        self.search_var.trace_add('write',lambda *args:self.refresh())
        self.label(sidebar,'Filtrar bancos pelo nome',9,MUTED).pack(anchor='w',padx=24,pady=(0,8))
        footer=tk.Frame(sidebar,bg=PANEL)
        footer.pack(side='bottom',fill='x',padx=20,pady=20)
        self.label(footer,'●  '+self.username,11,ACCENT,True).pack(anchor='w')
        self.label(footer,'Conta local  •  Nexus 0.2.0',9,MUTED).pack(anchor='w',pady=(5,12))
        ttk.Button(footer,text='Sair da conta',command=self.login_screen).pack(fill='x')
        self.banks=ttk.Treeview(sidebar,columns=('name','path'),displaycolumns=('name',),show='tree',selectmode='browse')
        self.banks.column('#0',width=230,stretch=True)
        self.banks.pack(fill='both',expand=True,padx=12,pady=(0,12))
        self.banks.bind('<<TreeviewSelect>>',self.on_selection)
        main=tk.Frame(self,bg=BG)
        main.pack(side='left',fill='both',expand=True,padx=28,pady=24)
        heading=tk.Frame(main,bg=BG)
        heading.pack(fill='x')
        self.label(heading,'WORKSPACE  /  SQLITE',9,MUTED,True).pack(side='left')
        self.label(heading,'●  Armazenamento local',9,ACCENT).pack(side='right')
        self.active_title=self.label(main,'Seu próximo projeto começa aqui',24,bold=True)
        self.active_title.pack(anchor='w',pady=(20,6))
        self.active_path=self.label(main,'Crie um banco ou selecione um na barra lateral.',10,MUTED)
        self.active_path.pack(fill='x',pady=(0,20))
        tools=tk.Frame(main,bg=BG)
        tools.pack(fill='x',pady=(0,20))
        self.selection_buttons=[]
        for text,cmd,style in [('Renomear',self.rename,'TButton'),('Mover para outro disco',self.move,'TButton'),('Excluir banco',self.delete,'Danger.TButton')]:
            button=ttk.Button(tools,text=text,style=style,command=lambda c=cmd:self.guard(c))
            button.pack(side='left',padx=(0,8))
            self.selection_buttons.append(button)
        ttk.Button(tools,text='↻ Atualizar',command=lambda:self.guard(self.refresh)).pack(side='right')
        editor=tk.Frame(main,bg=PANEL,highlightbackground='#263447',highlightthickness=1)
        editor.pack(fill='x')
        editorbar=tk.Frame(editor,bg=PANEL)
        editorbar.pack(fill='x',padx=18,pady=12)
        self.label(editorbar,'SQL  /  Editor de consulta',11,bold=True).pack(side='left')
        self.label(editorbar,'Ctrl + Enter para executar',9,MUTED).pack(side='right')
        self.sql=tk.Text(editor,height=7,bg='#0e1723',fg='#a3d9fa',insertbackground=ACCENT,font=('Consolas',12),undo=True,relief='flat',padx=18,pady=14,selectbackground='#28535d',wrap='none')
        self.sql.pack(fill='x',padx=1)
        self.sql.insert('1.0','SELECT name, type\nFROM sqlite_master\nORDER BY name;')
        actions=tk.Frame(editor,bg=PANEL)
        actions.pack(fill='x',padx=16,pady=12)
        execute=ttk.Button(actions,text='▶  Executar consulta',style='Primary.TButton',command=lambda:self.guard(self.run))
        execute.pack(side='left')
        self.selection_buttons.append(execute)
        self.label(actions,'Uma instrução por vez  ·  Até 1.000 linhas',9,MUTED).pack(side='right')
        resultbar=tk.Frame(main,bg=BG)
        resultbar.pack(fill='x',pady=(22,10))
        self.label(resultbar,'Resultados',13,bold=True).pack(side='left')
        self.status=self.label(resultbar,'Nenhuma consulta executada',9,MUTED)
        self.status.pack(side='right')
        results=tk.Frame(main,bg=PANEL,highlightbackground='#263447',highlightthickness=1)
        results.pack(fill='both',expand=True)
        self.result=ttk.Treeview(results,show='headings')
        self.result.grid(row=0,column=0,sticky='nsew')
        sy=ttk.Scrollbar(results,orient='vertical',command=self.result.yview)
        sy.grid(row=0,column=1,sticky='ns')
        sx=ttk.Scrollbar(results,orient='horizontal',command=self.result.xview)
        sx.grid(row=1,column=0,sticky='ew')
        self.result.configure(yscrollcommand=sy.set,xscrollcommand=sx.set)
        self.result.tag_configure('alternate',background='#172333')
        results.rowconfigure(0,weight=1)
        results.columnconfigure(0,weight=1)
        self.result_empty=self.label(results,'Os resultados da sua consulta aparecerão aqui.',11,MUTED)
        self.result_empty.place(relx=.5,rely=.5,anchor='center')
        self.refresh()

    def on_selection(self,event=None):
        rows=self.banks.selection()
        for button in self.selection_buttons: button.state(['!disabled'] if rows else ['disabled'])
        if rows:
            _,name,path=self.store.get(rows[0])
            self.active_title.config(text=name)
            self.active_path.config(text=path)
        else:
            self.active_title.config(text='Seu próximo projeto começa aqui')
            self.active_path.config(text='Crie um banco ou selecione um na barra lateral.')
        self.result.delete(*self.result.get_children())
        self.result['columns']=()
        self.result_empty.place(relx=.5,rely=.5,anchor='center')
        self.status.config(text='Pronto para uma nova consulta')

    def selected(self):
        rows=self.banks.selection()
        if not rows: raise ValueError('Selecione um banco na lista.')
        return rows[0]
    def refresh(self):
        selected=self.banks.selection()
        self.banks.delete(*self.banks.get_children())
        rows=self.store.list()
        self.bank_count.config(text=f'MEUS BANCOS  /  {len(rows):02d}')
        query=self.search_var.get().strip().casefold()
        for ident,name,path in rows:
            if query in name.casefold(): self.banks.insert('', 'end',iid=ident,text='  ◇  '+name,values=(name,path))
        if selected and self.banks.exists(selected[0]): self.banks.selection_set(selected[0])
        self.on_selection()
    def create(self):
        name=simpledialog.askstring('Novo banco','Nome do banco:',parent=self)
        if name:
            ident=self.store.create(name); self.search_var.set(''); self.refresh(); self.banks.selection_set(ident); self.on_selection()
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
        if not sql.lstrip().upper().startswith(('SELECT','EXPLAIN')) and not messagebox.askyesno('Executar SQL','Esta instrução pode alterar ou excluir dados. Executar?'): return
        columns,rows,truncated=self.store.execute(ident,sql)
        self.result_empty.place_forget()
        self.result.delete(*self.result.get_children()); keys=[str(i) for i in range(len(columns))]; self.result['columns']=keys
        for key,title in zip(keys,columns): self.result.heading(key,text=title); self.result.column(key,width=180,stretch=False)
        for index,row in enumerate(rows): self.result.insert('','end',values=['NULL' if v is None else str(v) for v in row],tags=('alternate',) if index%2 else ())
        self.status.config(text=f'{len(rows)} linha(s)'+(' • limitado a 1.000 resultados' if truncated else ' • concluído'))

if __name__=='__main__': Nexus().mainloop()
