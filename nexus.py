import tkinter as tk
from tkinter import ttk, messagebox, simpledialog, filedialog
import customtkinter as ctk
from core import Store
from ui import BG,PANEL,SURFACE,TEXT,MUTED,ACCENT,frame,label,button,brand,load_logo,style_window,center_window,NameDialog

ctk.set_appearance_mode('dark')
ctk.set_default_color_theme('blue')

class Nexus(ctk.CTk):
    def __init__(self,store=None):
        super().__init__()
        self.title('Nexus  —  Database Studio')
        self.geometry('1280x850')
        self.minsize(1040,740)
        self.configure(fg_color=BG)
        self.store=store or Store()
        self.failures=0
        self.username=''
        self.cooldown=False
        self.screen=None
        self.layout_job=None
        self.layout_fonts=[]
        self.layout_controls=[]
        self.layout_images=[]
        self.layout_scale=None
        self.layout_mode=None
        self.setup_styles()
        self.bind('<Configure>',self.schedule_layout,add='+')
        self.bind('<Control-Return>',lambda event:self.guard(self.run) if self.store.user else None)
        self.login_screen()
        self.after(150,self.finish_startup)

    def finish_startup(self):
        # Position only after Tk has mapped the native window.
        try:
            center_window(self)
            style_window(self)
        except Exception:
            import logging
            logging.exception('Falha ao personalizar janela; mantendo aparência padrão')
        finally:
            self.deiconify()
            self.lift()

    def setup_styles(self):
        style=ttk.Style(self)
        style.theme_use('clam')
        style.configure('Treeview',background=PANEL,fieldbackground=PANEL,foreground=TEXT,rowheight=40,borderwidth=0,font=('Segoe UI',10))
        style.map('Treeview',background=[('selected','#34304f')],foreground=[('selected','#e2d9ff')])
        style.configure('Treeview.Heading',background=SURFACE,foreground=MUTED,font=('Segoe UI Semibold',10),padding=10,borderwidth=0)
        style.map('Treeview.Heading',background=[('active',SURFACE)])
        style.layout('Treeview',[('Treeview.treearea',{'sticky':'nswe'})])

    def schedule_layout(self,event=None):
        if event is not None and event.widget is not self: return
        if self.layout_job is not None: self.after_cancel(self.layout_job)
        self.layout_job=self.after(90,self.apply_layout)

    def track_layout(self,mode):
        self.layout_mode=mode
        self.layout_fonts=[]
        self.layout_controls=[]
        self.layout_images=[]
        def collect(widget):
            if isinstance(widget,(ctk.CTkLabel,ctk.CTkButton,ctk.CTkEntry,ctk.CTkCheckBox,ctk.CTkTextbox)):
                value=widget.cget('font')
                if isinstance(value,tuple):
                    family,size,*extra=value
                else:
                    actual=value.actual()
                    family,size=actual['family'],actual['size']
                    extra=['bold'] if actual['weight']=='bold' else []
                self.layout_fonts.append((widget,family,abs(size),extra))
                if isinstance(widget,ctk.CTkLabel):
                    image=widget.cget('image')
                    if isinstance(image,ctk.CTkImage): self.layout_images.append((image,image.cget('size')))
                if isinstance(widget,(ctk.CTkButton,ctk.CTkEntry)):
                    self.layout_controls.append((widget,widget.cget('height')))
                return
            for child in widget.winfo_children(): collect(child)
        collect(self.screen)
        self.layout_scale=None
        self.schedule_layout()

    def apply_layout(self):
        self.layout_job=None
        if not self.screen or not self.screen.winfo_exists(): return
        # Tk reports physical pixels; the layout uses CustomTkinter's logical units.
        widget_scale=self.screen._get_widget_scaling()
        width=self.screen.winfo_width()/widget_scale
        height=self.screen.winfo_height()/widget_scale
        scale=min(1.65,max(1.0,min(width/1280,height/850)))
        scale=round(scale/0.05)*0.05
        if self.layout_mode=='login':
            self.login_shell.configure(width=min(width-56,2050),height=height-56)
            form_width=min(680,max(340,(min(width-56,2050)/2)-80))
            self.login_host.grid_columnconfigure(1,minsize=int(form_width))
            self.login_visual.configure(height=int(245*scale))
        else:
            self.sidebar.configure(width=int(252*min(scale,1.3)))
            self.sql.configure(height=max(6,min(14,int(6*height/850))),font=('Consolas',round(12*scale)))
            self.result_tabs.configure(height=max(170,int(height*.28)),segmented_button_font=('Segoe UI',round(11*scale)))
        if scale==self.layout_scale: return
        self.layout_scale=scale
        for widget,family,size,extra in self.layout_fonts:
            widget.configure(font=(family,round(size*scale),*extra))
        for widget,base in self.layout_controls:
            widget.configure(height=round(base*scale))
        for image,(image_width,image_height) in self.layout_images:
            image.configure(size=(round(image_width*scale),round(image_height*scale)))
        style=ttk.Style(self)
        style.configure('Treeview',font=('Segoe UI',round(10*scale)),rowheight=round(40*scale))
        style.configure('Treeview.Heading',font=('Segoe UI Semibold',round(10*scale)))
        if self.layout_mode=='login':
            self.login_visual.event_generate('<Configure>')

    def clear(self):
        if self.layout_job is not None:
            self.after_cancel(self.layout_job)
            self.layout_job=None
        self.layout_mode=None
        if self.screen is not None: self.screen.destroy()
        self.screen=frame(self,BG)
        self.screen.pack(fill='both',expand=True)

    def guard(self,action):
        try: return action()
        except Exception as error: messagebox.showerror('Nexus',str(error),parent=self)

    def login_screen(self):
        self.store.user=None
        self.clear()
        shell=frame(self.screen)
        self.login_shell=shell
        shell.configure(width=1224,height=794)
        shell.place(relx=.5,rely=.5,anchor='center')
        shell.grid_propagate(False)
        shell.columnconfigure(0,weight=1,uniform='login')
        shell.columnconfigure(1,weight=1,uniform='login')
        shell.rowconfigure(0,weight=1)
        story=frame(shell,PANEL,28)
        story.grid(row=0,column=0,sticky='nsew',padx=(0,20))
        brand(story,38).pack(anchor='w',padx=34,pady=(30,0))
        story_body=frame(story)
        story_body.pack(fill='x',expand=True)
        visual=ctk.CTkCanvas(story_body,bg=PANEL,highlightthickness=0,height=245)
        self.login_visual=visual
        visual.pack(fill='x',padx=24,pady=(22,0))
        def orbit(event):
            visual.delete('all')
            visual_scale=min(1.65,max(1,visual.winfo_height()/245))
            cx,cy=visual.winfo_width()/2,visual.winfo_height()/2
            for radius,color in [(110,'#20253c'),(82,'#2d2b4a'),(52,'#454065')]:
                radius*=visual_scale
                visual.create_oval(cx-radius,cy-radius,cx+radius,cy+radius,outline=color,width=1)
            visual.create_line(cx-88*visual_scale,cy+48*visual_scale,cx,cy,cx+92*visual_scale,cy-42*visual_scale,fill='#65578e',width=2,smooth=True)
            for x,y,r,color in [(cx-88,cy+48,8,'#8cceeb'),(cx+92,cy-42,10,ACCENT),(cx,cy,29,'#302c4b')]:
                x=cx+(x-cx)*visual_scale; y=cy+(y-cy)*visual_scale; r*=visual_scale
                visual.create_oval(x-r,y-r,x+r,y+r,fill=color,outline='')
            visual.create_text(cx,cy,text='N',font=('Segoe UI',round(25*visual_scale),'bold'),fill='#dfd5ff')
        visual.bind('<Configure>',orbit)
        label(story_body,'Tudo conectado.\nTudo no seu controle.',30,bold=True).pack(anchor='w',padx=34,pady=(12,12))
        label(story_body,'Um espaço tranquilo para explorar, organizar\ne dar vida aos seus dados.',12,MUTED).pack(anchor='w',padx=34)
        badges=frame(story_body)
        badges.pack(anchor='w',padx=34,pady=26)
        for text in ['SQLite','Local','Seu espaço']:
            chip=frame(badges,SURFACE,14)
            chip.pack(side='left',padx=(0,8))
            label(chip,text,10,ACCENT).pack(padx=12,pady=5)
        label(story,'NEXUS  /  DATABASE STUDIO',9,MUTED).pack(side='bottom',anchor='w',padx=34,pady=24)
        host=frame(shell)
        host.grid(row=0,column=1,sticky='nsew')
        self.login_host=host
        host.grid_columnconfigure(0,weight=1)
        host.grid_columnconfigure(1,minsize=460)
        host.grid_columnconfigure(2,weight=1)
        host.grid_rowconfigure(0,weight=1)
        host.grid_rowconfigure(2,weight=1)
        form=frame(host)
        self.login_form=form
        form.grid(row=1,column=1,sticky='ew')
        label(form,'BEM-VINDO DE VOLTA',10,ACCENT,bold=True).pack(anchor='w')
        label(form,'Seu espaço começa aqui.',27,bold=True).pack(anchor='w',pady=(12,8))
        label(form,'Entre para continuar suas ideias.',12,MUTED).pack(anchor='w',pady=(0,30))
        label(form,'Usuário',11,bold=True).pack(anchor='w',pady=(0,8))
        name=ctk.CTkEntry(form,height=48,corner_radius=13,fg_color=PANEL,border_color='#30344c',placeholder_text='Seu nome de usuário')
        name.pack(fill='x')
        label(form,'Senha',11,bold=True).pack(anchor='w',pady=(20,8))
        password=ctk.CTkEntry(form,height=48,corner_radius=13,fg_color=PANEL,border_color='#30344c',show='●',placeholder_text='Sua senha')
        password.pack(fill='x')
        show=tk.BooleanVar(value=False)
        ctk.CTkCheckBox(form,text='Mostrar senha',variable=show,command=lambda:password.configure(show='' if show.get() else '●'),checkbox_width=18,checkbox_height=18,corner_radius=6,fg_color=ACCENT,hover_color='#9582e2',text_color=MUTED,checkmark_color=BG).pack(anchor='w',pady=(14,18))
        feedback=label(form,'',10,'#ffa4ba',wraplength=360)
        feedback.pack(fill='x',pady=(0,8))
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
                feedback.config(text=str(error),fg='#ffa4ba')
                if self.failures>=5:
                    self.cooldown=True
                    enter.state(['disabled'])
                    feedback.config(text='Aguarde 30 segundos antes de tentar novamente.')
                    self.after(30000,unlock)
        enter=button(form,'Entrar no Nexus   →',authenticate,primary=True)
        enter.configure(height=48)
        enter.pack(fill='x')
        password.bind('<Return>',lambda event:authenticate())
        def register():
            try:
                self.store.register(name.get(),password.get())
                feedback.config(text='Conta criada. Entre para começar.',fg='#9de3ce')
            except Exception as error: feedback.config(text=str(error),fg='#ffa4ba')
        button(form,'Criar uma conta local',register).pack(fill='x',pady=(12,24))
        label(form,'Sua conta fica neste computador.\nUse pelo menos 10 caracteres na senha.',10,MUTED).pack(anchor='w')
        self.track_layout('login')
        name.focus_set()

    def dashboard(self):
        self.clear()
        sidebar=frame(self.screen,PANEL,24,width=252)
        sidebar.pack(side='left',fill='y',padx=(18,0),pady=18)
        sidebar.pack_propagate(False)
        self.sidebar=sidebar
        brand(sidebar,34).pack(anchor='w',padx=22,pady=(22,24))
        label(sidebar,'SEU ESPAÇO',9,MUTED,True).pack(anchor='w',padx=24)
        button(sidebar,'＋  Novo banco de dados',lambda:self.guard(self.create),primary=True).pack(fill='x',padx=18,pady=(12,24))
        self.bank_count=label(sidebar,'MEUS BANCOS',10,MUTED,True)
        self.bank_count.pack(anchor='w',padx=24,pady=(0,12))
        self.search_var=tk.StringVar()
        search=ctk.CTkEntry(sidebar,textvariable=self.search_var,height=38,corner_radius=12,fg_color=SURFACE,border_width=0,placeholder_text='Buscar banco...')
        search.pack(fill='x',padx=18,pady=(0,12))
        self.search_var.trace_add('write',lambda *args:self.refresh())
        footer=frame(sidebar,SURFACE,16)
        footer.pack(side='bottom',fill='x',padx=14,pady=14)
        label(footer,self.username or 'Minha conta',11,TEXT,True).pack(anchor='w',padx=14,pady=(10,0))
        label(footer,'Conta local  ·  v0.5.0',9,MUTED).pack(anchor='w',padx=14,pady=(0,8))
        button(footer,'Sair da conta',self.login_screen).pack(fill='x',padx=10,pady=(0,10))
        self.banks=ttk.Treeview(sidebar,columns=('name','path'),displaycolumns=(),show='tree',selectmode='browse')
        self.banks.column('#0',width=205,stretch=True)
        self.banks.pack(fill='both',expand=True,padx=16,pady=(0,8))
        self.banks.bind('<<TreeviewSelect>>',self.on_selection)
        main=frame(self.screen)
        main.pack(side='left',fill='both',expand=True,padx=24,pady=22)
        top=frame(main)
        top.pack(fill='x')
        label(top,'ESTÚDIO  /  BANCOS DE DADOS',9,MUTED,True).pack(side='left')
        pill=frame(top,'#1a2e2d',13)
        pill.pack(side='right')
        label(pill,'●  SQLite local',9,'#9de3ce').pack(padx=12,pady=4)
        hero=frame(main,PANEL,22)
        hero.pack(fill='x',pady=(20,18))
        self.active_title=label(hero,'Vamos criar algo novo?',25,bold=True)
        self.active_title.pack(anchor='w',padx=22,pady=(18,4))
        self.active_path=label(hero,'Selecione um banco na lateral ou crie seu primeiro projeto.',10,MUTED)
        self.active_path.pack(fill='x',padx=22,pady=(0,18))
        tools=frame(main)
        tools.pack(fill='x',pady=(0,16))
        self.selection_buttons=[]
        for text,cmd,danger in [('Renomear',self.rename,False),('Mover banco',self.move,False),('Excluir',self.delete,True)]:
            item=button(tools,text,lambda c=cmd:self.guard(c),danger=danger,width=100)
            item.pack(side='left',padx=(0,8))
            self.selection_buttons.append(item)
        button(tools,'↻  Atualizar',lambda:self.guard(self.refresh),width=105).pack(side='right')
        editor=frame(main,PANEL,22)
        editor.pack(fill='x')
        editorbar=frame(editor)
        editorbar.pack(fill='x',padx=20,pady=(14,10))
        label(editorbar,'Editor SQL',13,bold=True).pack(side='left')
        label(editorbar,'Ctrl + Enter',9,MUTED).pack(side='right')
        code=frame(editor,'#101522',14)
        code.pack(fill='x',padx=12)
        self.sql=tk.Text(code,height=6,bg='#101522',fg='#c1c8f8',insertbackground=ACCENT,font=('Consolas',12),undo=True,relief='flat',padx=12,pady=12,selectbackground='#403862',wrap='none')
        self.sql.pack(fill='x',padx=8,pady=(8,0))
        self.sql.tag_configure('keyword',foreground='#bd9bff')
        self.sql.tag_configure('comment',foreground='#7185a4')
        self.sql.tag_configure('string',foreground='#92d9bd')
        self.sql.bind('<KeyRelease>',lambda event:self.highlight_sql())
        sx=ctk.CTkScrollbar(code,orientation='horizontal',command=self.sql.xview,height=12)
        sx.pack(fill='x',padx=14,pady=(0,4))
        self.sql.configure(xscrollcommand=sx.set)
        self.sql.insert('1.0','SELECT name, type\nFROM sqlite_master\nORDER BY name;')
        self.highlight_sql()
        actions=frame(editor)
        actions.pack(fill='x',padx=16,pady=12)
        execute=button(actions,'▶  Executar',lambda:self.guard(self.run),primary=True,width=140)
        execute.pack(side='left')
        self.selection_buttons.append(execute)
        label(actions,'Uma instrução  ·  Até 1.000 linhas',9,MUTED).pack(side='right')
        resultbar=frame(main)
        resultbar.pack(fill='x',pady=(18,10))
        label(resultbar,'Resultados',13,bold=True).pack(side='left')
        self.status=label(resultbar,'Pronto para explorar',9,MUTED)
        self.status.pack(side='right')
        results=frame(main,PANEL,20)
        results.pack(fill='both',expand=True)
        self.result_tabs=ctk.CTkTabview(results,fg_color=PANEL,corner_radius=16,
            segmented_button_fg_color=SURFACE,segmented_button_selected_color='#453960',
            segmented_button_selected_hover_color='#554572',segmented_button_unselected_color=SURFACE,
            segmented_button_unselected_hover_color='#2b324a',text_color=TEXT,anchor='nw',height=170)
        self.result_tabs.pack(fill='both',expand=True,padx=10,pady=(4,10))
        self.result_tabs.add('Resultado')
        self.result_tabs.add('Tabela')
        self.result_text=ctk.CTkTextbox(self.result_tabs.tab('Resultado'),fg_color='#101522',
            text_color='#c1c8f8',font=('Consolas',11),corner_radius=12,wrap='none')
        self.result_text.pack(fill='both',expand=True,padx=3,pady=3)
        self.set_result_text('Execute uma consulta para visualizar o resultado.\nOs mesmos dados estarão disponíveis na aba Tabela.')
        inner=frame(self.result_tabs.tab('Tabela'),PANEL)
        inner.pack(fill='both',expand=True,padx=3,pady=3)
        self.result=ttk.Treeview(inner,show='headings')
        self.result.grid(row=0,column=0,sticky='nsew')
        sy=ctk.CTkScrollbar(inner,orientation='vertical',command=self.result.yview)
        sy.grid(row=0,column=1,sticky='ns')
        sx=ctk.CTkScrollbar(inner,orientation='horizontal',command=self.result.xview)
        sx.grid(row=1,column=0,sticky='ew')
        self.result.configure(yscrollcommand=sy.set,xscrollcommand=sx.set)
        self.result.tag_configure('alternate',background='#1c2235')
        inner.rowconfigure(0,weight=1)
        inner.columnconfigure(0,weight=1)
        self.result_empty=label(inner,'◇   Seus dados ganham forma aqui.\nExecute uma consulta para ver os resultados.',11,MUTED)
        self.result_empty.place(relx=.5,rely=.5,anchor='center')
        self.refresh()
        self.track_layout('dashboard')

    def highlight_sql(self):
        import re
        text=self.sql.get('1.0','end-1c')
        for tag in ('keyword','comment','string'): self.sql.tag_remove(tag,'1.0','end')
        pattern=r"(--[^\n]*|'(?:''|[^'])*'|\b(?:SELECT|FROM|WHERE|ORDER|BY|AS|INSERT|INTO|VALUES|UPDATE|SET|DELETE|CREATE|TABLE|DROP|ALTER|LIMIT|JOIN|ON|AND|OR|NULL|PRIMARY|KEY|INTEGER|TEXT)\b)"
        for match in re.finditer(pattern,text,re.IGNORECASE):
            token=match.group()
            tag='comment' if token.startswith('--') else 'string' if token.startswith("'") else 'keyword'
            self.sql.tag_add(tag,f'1.0+{match.start()}c',f'1.0+{match.end()}c')

    def on_selection(self,event=None):
        rows=self.banks.selection()
        for item in self.selection_buttons: item.state(['!disabled'] if rows else ['disabled'])
        if rows:
            _,name,path=self.store.get(rows[0])
            self.active_title.config(text=name)
            self.active_path.config(text=path)
        else:
            self.active_title.config(text='Vamos criar algo novo?')
            self.active_path.config(text='Selecione um banco na lateral ou crie seu primeiro projeto.')
        self.result.delete(*self.result.get_children())
        self.result['columns']=()
        self.result_empty.place(relx=.5,rely=.5,anchor='center')
        self.status.config(text='Pronto para explorar')
        self.set_result_text('Execute uma consulta para visualizar o resultado.\nOs mesmos dados estarão disponíveis na aba Tabela.')

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
        if getattr(self,'create_dialog',None) is not None and self.create_dialog.winfo_exists():
            self.create_dialog.lift()
            self.create_dialog.name.focus_set()
            return self.create_dialog
        def submit(name):
            ident=self.store.create(name)
            self.search_var.set('')
            self.refresh()
            self.banks.selection_set(ident)
            self.banks.see(ident)
            self.on_selection()
        self.create_dialog=NameDialog(self,submit)
        return self.create_dialog

    def set_result_text(self,text):
        self.result_text.configure(state='normal')
        self.result_text.delete('1.0','end')
        self.result_text.insert('1.0',text)
        self.result_text.configure(state='disabled')
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
        try:
            columns,rows,truncated=self.store.execute(ident,sql)
        except Exception as error:
            self.result.delete(*self.result.get_children())
            self.result['columns']=()
            self.result_empty.place(relx=.5,rely=.5,anchor='center')
            self.set_result_text('Falha ao executar a consulta.\n\n'+str(error))
            self.status.config(text='Consulta não concluída')
            self.result_tabs.set('Resultado')
            raise
        import json
        output=[f'Banco: {self.store.get(ident)[1]}',f'{len(rows)} linha(s) retornada(s).']
        if truncated: output.append('Exibição limitada às primeiras 1.000 linhas.')
        output.append('')
        for index,row in enumerate(rows,1):
            output.append(f'[{index}]')
            for column,value in zip(columns,row):
                output.append(f'{column}: {json.dumps(value,ensure_ascii=False,default=str)}')
            output.append('')
        if not rows: output.append('A consulta não retornou linhas.')
        self.set_result_text('\n'.join(output))
        self.result_empty.place_forget()
        self.result.delete(*self.result.get_children()); keys=[str(i) for i in range(len(columns))]; self.result['columns']=keys
        for key,title in zip(keys,columns): self.result.heading(key,text=title); self.result.column(key,width=180,stretch=False)
        for index,row in enumerate(rows): self.result.insert('','end',values=['NULL' if v is None else str(v) for v in row],tags=('alternate',) if index%2 else ())
        self.status.config(text=f'{len(rows)} linha(s)'+(' • limitado a 1.000 resultados' if truncated else ' • concluído'))

def main():
    import os,sys,logging
    from pathlib import Path
    folder=Path(os.getenv('LOCALAPPDATA',Path.home()))/'Nexus'
    folder.mkdir(parents=True,exist_ok=True)
    logging.basicConfig(filename=folder/'nexus.log',level=logging.INFO,
                        format='%(asctime)s %(levelname)s %(message)s',encoding='utf-8')
    try:
        logging.info('Nexus 0.5.0: iniciando')
        app=Nexus()
        def callback_error(exc_type,value,tb):
            logging.error('Erro na interface',exc_info=(exc_type,value,tb))
            messagebox.showerror('Nexus','Ocorreu um erro. Consulte '+str(folder/'nexus.log'),parent=app)
        app.report_callback_exception=callback_error
        def ready():
            logging.info('Janela pronta: visible=%s geometry=%s',app.winfo_viewable(),app.geometry())
            if '--startup-check' in sys.argv:
                if not app.winfo_viewable():
                    app.destroy()
                    raise RuntimeError('Janela não visível no teste de abertura')
                app.destroy()
        app.after(2000,ready)
        app.mainloop()
        logging.info('Nexus encerrado')
    except Exception:
        logging.exception('Falha na inicialização')
        messagebox.showerror('Nexus','Não foi possível iniciar. Detalhes em '+str(folder/'nexus.log'))
        raise

if __name__=='__main__': main()
