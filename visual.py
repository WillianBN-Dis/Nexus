"""Visual table designer and record editor for the same SQLite databases as SQL mode."""
import math
import tkinter as tk
from tkinter import ttk,messagebox
import customtkinter as ctk
from ui import BG,PANEL,SURFACE,TEXT,MUTED,ACCENT,frame,label,button,brand,center_window,style_window,Tabs

TYPES=['TEXT','INTEGER','REAL','BLOB']

def field_entry(parent,**kwargs):
    style=dict(fg_color=BG,border_color='#36324f',corner_radius=11,text_color=TEXT,placeholder_text_color=MUTED)
    style.update(kwargs)
    return ctk.CTkEntry(parent,**style)

def option_menu(parent,**kwargs):
    style=dict(fg_color=SURFACE,button_color='#453960',button_hover_color='#5a4a7c',
        dropdown_fg_color=PANEL,dropdown_hover_color=SURFACE,text_color=TEXT)
    style.update(kwargs)
    return ctk.CTkOptionMenu(parent,**style)

class FormDialog(ctk.CTkToplevel):
    def __init__(self,parent,title,width=700,height=620):
        super().__init__(parent)
        self.title('Nexus  —  '+title)
        self.configure(fg_color=BG)
        self.geometry(f'{width}x{height}');self.minsize(650 if width>=700 else 550,460)
        self.transient(parent)
        top=frame(self,PANEL,22);top.pack(fill='x',padx=22,pady=22)
        brand(top,28).pack(anchor='w',padx=22,pady=(16,8))
        label(top,title,23,bold=True).pack(anchor='w',padx=22,pady=(0,18))
        footer=frame(self);footer.pack(side='bottom',fill='x',padx=22,pady=(8,18))
        self.feedback=label(footer,'',11,'#ffabb9',wraplength=620);self.feedback.pack(fill='x',pady=(0,8))
        actions=frame(footer);actions.pack(fill='x')
        button(actions,'Cancelar',self.close).pack(side='left')
        self.save_button=button(actions,'Salvar alterações  →',self.submit,primary=True,width=175);self.save_button.pack(side='right')
        self.body=ctk.CTkScrollableFrame(self,fg_color=PANEL,corner_radius=18)
        self.body.pack(fill='both',expand=True,padx=18,pady=(0,8))
        self.protocol('WM_DELETE_WINDOW',self.close)
        self.bind('<Escape>',lambda event:self.close())
        self.grab_set()
        self.after(100,lambda:center_window(self,parent=parent))
        self.after(250,lambda:style_window(self) if self.winfo_exists() else None)
    def submit(self):
        try: self.save()
        except Exception as error:
            self.feedback.config(text=str(error));return
        self.close()
    def close(self):
        self.grab_release();self.destroy()

class TableDialog(FormDialog):
    def __init__(self,parent,save):
        super().__init__(parent,'Uma nova tabela para suas ideias',840,720)
        self.save_button.configure(text='Criar tabela  →')
        self.save_callback=save
        label(self.body,'Nome da tabela',12,bold=True).pack(anchor='w',padx=14,pady=(12,8))
        self.name=field_entry(self.body,placeholder_text='Ex.: clientes',height=42)
        self.name.pack(fill='x',padx=14)
        info=frame(self.body,'#24213b',12);info.pack(fill='x',padx=14,pady=(16,12))
        label(info,'◆  id  ·  INTEGER  ·  Automático',12,ACCENT,True).pack(anchor='w',padx=14,pady=(10,3))
        label(info,'Cada registro recebe um identificador único ao salvar.',11,MUTED).pack(anchor='w',padx=14,pady=(0,10))
        label(self.body,'SUAS COLUNAS',10,MUTED,True).pack(anchor='w',padx=14,pady=(8,6))
        self.columns_frame=frame(self.body);self.columns_frame.pack(fill='x',padx=10)
        self.rows=[]
        button(self.body,'＋ Adicionar outra coluna',self.add_field).pack(anchor='w',padx=14,pady=14)
        label(self.body,'TEXT: texto  ·  INTEGER: inteiro  ·  REAL: decimal  ·  BLOB: arquivo binário',10,MUTED,wraplength=660).pack(anchor='w',padx=14,pady=(0,10))
        self.add_field()
    def add_field(self):
        row=frame(self.columns_frame,SURFACE,14);row.pack(fill='x',pady=6)
        name=field_entry(row,placeholder_text='Ex.: nome, email, quantidade',height=42,width=230);name.pack(side='left',fill='x',expand=True,padx=8,pady=10)
        kind=option_menu(row,values=TYPES,width=120,height=42,fg_color=PANEL,button_color='#453960');kind.pack(side='left',padx=6)
        required=ctk.CTkCheckBox(row,text='Obrigatória',width=115,checkbox_width=18,checkbox_height=18);required.pack(side='left',padx=8)
        item=(row,name,kind,required)
        def remove():
            self.rows.remove(item);row.destroy()
        button(row,'×',remove,width=34).pack(side='left',padx=8)
        self.rows.append(item)
    def save(self):
        columns=[{'name':name.get(),'type':kind.get(),'required':bool(required.get())} for _,name,kind,required in self.rows]
        self.save_callback(self.name.get(),columns)

class ColumnDialog(FormDialog):
    def __init__(self,parent,save):
        super().__init__(parent,'Adicionar coluna',620,500)
        self.save_callback=save
        label(self.body,'Nome da coluna',12,bold=True).pack(anchor='w',padx=14,pady=(16,8))
        self.name=field_entry(self.body,height=42);self.name.pack(fill='x',padx=14)
        label(self.body,'Tipo de dado',12,bold=True).pack(anchor='w',padx=14,pady=(16,8))
        self.kind=option_menu(self.body,values=TYPES);self.kind.pack(anchor='w',padx=14)
        label(self.body,'Os registros existentes receberão NULL nesta coluna.',11,MUTED).pack(anchor='w',padx=14,pady=18)
    def save(self): self.save_callback(self.name.get(),self.kind.get())

class RecordDialog(FormDialog):
    def __init__(self,parent,info,save,record=None):
        super().__init__(parent,'Editar registro' if record else 'Novo registro',720,650)
        self.save_callback=save;self.fields=[];self.record=record
        self.save_button.configure(text='Salvar registro  →')
        label(self.body,'Preencha os campos abaixo. Escolha NULL para vazio ou Padrão para automático.',11,MUTED,wraplength=620).pack(anchor='w',padx=14,pady=12)
        for index,col in enumerate(info['columns']):
            if col['hidden']: continue
            box=frame(self.body,SURFACE,12);box.pack(fill='x',padx=10,pady=6)
            label(box,col['name']+('  *' if col['required'] else ''),13,bold=True).pack(anchor='w',padx=14,pady=(12,4))
            label(box,(col['type'] or 'Sem tipo')+('  ·  Identificador' if col['pk'] else '  ·  Obrigatório' if col['required'] else '  ·  Opcional')+('  ·  Hexadecimal' if col['type'].upper()=='BLOB' else ''),10,MUTED).pack(anchor='w',padx=14,pady=(0,8))
            entry=field_entry(box,height=38);entry.pack(fill='x',padx=12)
            states=['Valor','NULL'] if record else ['Valor','NULL','Padrão']
            mode=option_menu(box,values=states,width=130);mode.pack(anchor='w',padx=12,pady=10)
            if record:
                value=record['values'][index]
                mode.set('NULL' if value is None else 'Valor')
                if value is not None: entry.insert(0,value.hex() if isinstance(value,bytes) else str(value))
            elif col['default'] is not None or (col['pk'] and col['type'].upper()=='INTEGER'):
                mode.set('Padrão')
            def update_mode(value,target=entry): target.configure(state='normal' if value=='Valor' else 'disabled')
            mode.configure(command=update_mode);update_mode(mode.get())
            self.fields.append((col,entry,mode))
    def save(self):
        values={}
        for col,entry,mode in self.fields:
            if mode.get()=='Padrão': continue
            if mode.get()=='NULL': values[col['name']]=None;continue
            value=entry.get();kind=col['type'].upper()
            try:
                if 'INT' in kind: value=int(value)
                elif any(part in kind for part in ('REAL','FLOA','DOUB')):
                    value=float(value)
                    if not math.isfinite(value): raise ValueError()
                elif kind=='BLOB': value=bytes.fromhex(value)
            except ValueError: raise ValueError('Valor inválido para '+col['name']+' ('+kind+').')
            values[col['name']]=value
        self.save_callback(values)

class DropDialog(FormDialog):
    def __init__(self,parent,table,save):
        super().__init__(parent,'Excluir tabela',650,470)
        self.table=table;self.save_callback=save
        label(self.body,'Todos os registros desta tabela serão excluídos.',13,'#ffabb9',True).pack(anchor='w',padx=14,pady=16)
        label(self.body,'Para confirmar, digite: '+table,12,wraplength=580).pack(anchor='w',padx=14,pady=10)
        self.name=field_entry(self.body,height=42);self.name.pack(fill='x',padx=14)
        self.save_button.configure(text='Excluir tabela',fg_color='#6c3049',hover_color='#8a3c5b',text_color=TEXT)
    def save(self):
        if self.name.get()!=self.table: raise ValueError('Digite exatamente o nome da tabela.')
        self.save_callback()

class VisualEditor(ctk.CTkFrame):
    def __init__(self,parent,store,guard):
        super().__init__(parent,fg_color='transparent')
        self.store=store;self.guard=guard;self.ident=None;self.table=None
        self.info=None;self.offset=0;self.sort=None;self.descending=False;self.dialog=None
        header=frame(self,PANEL,20)
        header.pack(fill='x',pady=(8,12))
        heading=frame(header);heading.pack(fill='x',padx=20,pady=(16,10))
        copy=frame(heading);copy.pack(side='left')
        label(copy,'EXPLORADOR DE DADOS',9,ACCENT,True).pack(anchor='w')
        label(copy,'Tabelas e registros',22,bold=True).pack(anchor='w',pady=(4,0))
        self.create_button=button(heading,'＋ Nova tabela',lambda:self.guard(self.create_table),primary=True,width=145)
        self.create_button.pack(side='right',padx=(12,0))
        picker=frame(header);picker.pack(fill='x',padx=20,pady=(0,18))
        self.table_choice=option_menu(picker,values=['Selecione um banco'],width=240,height=38,
            corner_radius=11,command=self.choose_table,fg_color=SURFACE,button_color='#453960',
            button_hover_color='#5a4a7c',dropdown_fg_color=PANEL,dropdown_hover_color=SURFACE)
        self.table_choice.pack(side='left')
        self.table_summary=label(picker,'Escolha onde organizar seus dados.',10,MUTED)
        self.table_summary.pack(side='left',padx=16)
        bottom=frame(self);bottom.pack(side='bottom',fill='x',pady=(10,4))
        self.status=label(bottom,'Selecione um banco para começar.',10,MUTED);self.status.pack(side='left',padx=8)
        self.next_button=button(bottom,'→',lambda:self.guard(lambda:self.page(200)),width=42);self.next_button.pack(side='right')
        self.page_label=label(bottom,'01',11,ACCENT,True);self.page_label.pack(side='right',padx=14)
        self.previous_button=button(bottom,'←',lambda:self.guard(lambda:self.page(-200)),width=42);self.previous_button.pack(side='right')
        self.tabs=Tabs(self,fg_color=PANEL,corner_radius=18,segmented_button_fg_color=SURFACE,
            segmented_button_selected_color='#453960',segmented_button_unselected_color=SURFACE,anchor='nw')
        self.tabs.pack(fill='both',expand=True)
        self.tabs.add('Dados');self.tabs.add('Estrutura')
        self.actions=[None]*5
        data=self.tabs.tab('Dados')
        tools=frame(data);tools.pack(fill='x',padx=8,pady=(10,12))
        for index,text,action,primary,danger in [(0,'＋ Novo registro',self.insert,True,False),(1,'Editar',self.edit,False,False),(2,'Excluir',self.delete,False,True)]:
            item=button(tools,text,lambda func=action:self.guard(func),width=145 if primary else 85,primary=primary,danger=danger)
            item.pack(side='left',padx=(0,8));self.actions[index]=item
        self.record_summary=label(tools,'Nenhuma linha selecionada',10,MUTED)
        self.record_summary.pack(side='right',padx=6)
        self.data_shell=frame(data,'#101522',14)
        self.data_shell.pack(fill='both',expand=True,padx=6,pady=(0,8))
        self.data_shell.rowconfigure(0,weight=1);self.data_shell.columnconfigure(0,weight=1)
        self.data_grid=ttk.Treeview(self.data_shell,show=('tree','headings'),selectmode='browse')
        self.data_grid.heading('#0',text='#');self.data_grid.column('#0',width=84,minwidth=70,stretch=False,anchor='center')
        self.data_grid.grid(row=0,column=0,sticky='nsew',padx=(8,0),pady=(8,0))
        self.data_grid.bind('<Double-1>',self.double_click)
        self.data_grid.bind('<<TreeviewSelect>>',lambda event:self.update_selection())
        self.data_grid.tag_configure('alternate',background='#1b2235')
        sy=ctk.CTkScrollbar(self.data_shell,command=self.data_grid.yview)
        sy.grid(row=0,column=1,sticky='ns',pady=8)
        sx=ctk.CTkScrollbar(self.data_shell,orientation='horizontal',command=self.data_grid.xview)
        sx.grid(row=1,column=0,sticky='ew',padx=12,pady=(0,4))
        self.data_grid.configure(yscrollcommand=sy.set,xscrollcommand=sx.set)
        self.empty=frame(self.data_shell,PANEL,20)
        self.empty_icon=label(self.empty,'◇',28,ACCENT)
        self.empty_icon.pack(pady=(12,2))
        self.empty_title=label(self.empty,'Comece pela sua primeira tabela',17,bold=True)
        self.empty_title.pack(padx=24)
        self.empty_hint=label(self.empty,'',11,MUTED,wraplength=380)
        self.empty_hint.pack(padx=24,pady=(6,10))
        self.empty_button=button(self.empty,'＋ Criar tabela',lambda:self.guard(self.empty_action),primary=True)
        self.empty_button.pack(pady=(0,14),padx=36)
        self.empty_requested=False
        self.data_shell.bind('<Configure>',lambda event:self.fit_empty())
        structure=self.tabs.tab('Estrutura')
        schema_top=frame(structure);schema_top.pack(fill='x',padx=10,pady=(10,12))
        label(schema_top,'Colunas e tipos de dados',13,bold=True).pack(side='left')
        self.actions[3]=button(schema_top,'＋ Nova coluna',lambda:self.guard(self.add_column),width=145)
        self.actions[3].pack(side='right')
        schema_body=frame(structure);schema_body.pack(fill='both',expand=True,padx=8)
        schema_body.rowconfigure(0,weight=1);schema_body.columnconfigure(0,weight=1)
        self.schema=ttk.Treeview(schema_body,columns=('name','type','required','key'),show='headings')
        self.schema.grid(row=0,column=0,sticky='nsew')
        self.schema.tag_configure('alternate',background='#1b2235')
        for key,title in [('name','Coluna'),('type','Tipo de dado'),('required','Obrigatória'),('key','Chave primária')]:
            self.schema.heading(key,text=title);self.schema.column(key,width=160,minwidth=110,stretch=True)
        scroll=ctk.CTkScrollbar(schema_body,command=self.schema.yview)
        scroll.grid(row=0,column=1,sticky='ns');self.schema.configure(yscrollcommand=scroll.set)
        schema_bottom=frame(structure,'#251b2a',12);schema_bottom.pack(fill='x',padx=8,pady=12)
        label(schema_bottom,'Excluir remove a tabela e seus registros.',10,'#d7a5b7').pack(side='left',padx=14,pady=12)
        self.actions[4]=button(schema_bottom,'Excluir tabela',lambda:self.guard(self.drop),width=140,danger=True)
        self.actions[4].pack(side='right',padx=10,pady=8)
        self.set_database(None)

    def update_selection(self):
        selected=bool(self.data_grid.selection()) and bool(self.info and self.info['identity'])
        self.actions[1].state(['!disabled'] if selected else ['disabled'])
        self.actions[2].state(['!disabled'] if selected else ['disabled'])
        self.record_summary.config(text='1 linha selecionada' if selected else 'Selecione uma linha para editar')

    def empty_action(self):
        if self.table: return self.insert()
        return self.create_table()

    def show_empty(self):
        if not self.ident:
            title='Seus dados começam aqui'
            hint='Selecione um banco na barra lateral para explorar as tabelas.'
            text='Selecione um banco'
        elif not self.table:
            title='Um espaço para suas ideias'
            hint='Crie uma tabela, defina as colunas e comece a organizar seus dados.'
            text='＋ Criar primeira tabela'
        else:
            title='Sua tabela está pronta'
            hint='Adicione o primeiro registro. Cada campo terá seu próprio espaço.'
            text='＋ Adicionar registro'
        self.empty_title.config(text=title);self.empty_hint.config(text=hint)
        self.empty_button.configure(text=text)
        self.empty_button.state(['!disabled'] if self.ident else ['disabled'])
        self.empty_requested=True
        self.fit_empty()

    def fit_empty(self):
        if self.empty_requested and self.data_shell.winfo_height()>=175:
            self.empty.place(relx=.5,rely=.5,anchor='center')
        else: self.empty.place_forget()

    def set_database(self,ident):
        self.ident=ident;self.table=None;self.offset=0;self.sort=None;self.descending=False
        self.refresh_tables()
    def refresh_tables(self):
        tables=self.store.tables(self.ident) if self.ident else []
        self.table_choice.configure(values=tables or ['Nenhuma tabela'],state='normal' if tables else 'disabled')
        self.create_button.state(['!disabled'] if self.ident else ['disabled'])
        if self.table not in tables: self.table=tables[0] if tables else None;self.offset=0;self.sort=None
        self.table_choice.set(self.table or 'Nenhuma tabela')
        if self.table and self.sort not in [col['name'] for col in self.store.table_info(self.ident,self.table)['columns']]: self.sort=None
        self.reload()
    def choose_table(self,table):
        if not self.ident or table=='Nenhuma tabela': return
        self.table=table;self.offset=0;self.sort=None;self.descending=False
        self.guard(self.reload)
    def reload(self):
        self.data_grid.delete(*self.data_grid.get_children());self.schema.delete(*self.schema.get_children())
        self.empty_requested=False
        self.empty.place_forget()
        self.data_grid['columns']=();self.records={};self.info=None
        self.table_summary.config(text='Nenhuma tabela selecionada')
        self.page_label.config(text=f'{self.offset//200+1:02d}')
        self.update_selection()
        for item in self.actions:item.state(['disabled'])
        self.previous_button.state(['disabled']);self.next_button.state(['disabled'])
        if not self.ident or not self.table:
            self.status.config(text='Crie uma tabela para começar.' if self.ident else 'Nenhum banco selecionado')
            self.show_empty();return
        self.info=self.store.browse_table(self.ident,self.table,self.sort,self.descending,self.offset)
        columns=self.info['columns'];keys=[str(i) for i in range(len(columns))];self.data_grid['columns']=keys
        for key,col in zip(keys,columns):
            arrow=(' ↓' if self.descending else ' ↑') if self.sort==col['name'] else ''
            self.data_grid.heading(key,text=col['name']+arrow,anchor='w',command=lambda name=col['name']:self.guard(lambda:self.order_by(name)))
            self.data_grid.column(key,width=100 if col['pk'] else 180,minwidth=90 if col['pk'] else 120,stretch=not bool(col['pk']))
            self.schema.insert('','end',values=(col['name'],col['type'] or 'Sem tipo','Sim' if col['required'] else 'Opcional','◆ Sim' if col['pk'] else '—'),tags=('alternate',) if int(key)%2 else ())
        for index,record in enumerate(self.info['rows']):
            key=str(index);self.records[key]=record
            values=['NULL' if value is None else '0x'+value.hex() if isinstance(value,bytes) else str(value) for value in record['values']]
            self.data_grid.insert('','end',iid=key,text=f'{self.offset+index+1:03d}',values=values,tags=('alternate',) if index%2 else ())
        for item in self.actions:item.state(['!disabled'])
        self.update_selection()
        self.table_summary.config(text=f"{len(columns)} colunas  ·  SQLite")
        if not self.records: self.show_empty()
        self.previous_button.state(['!disabled'] if self.offset else ['disabled'])
        self.next_button.state(['!disabled'] if self.info['more'] else ['disabled'])
        start=self.offset+1 if self.records else 0
        sorting=f'  ·  {self.sort} '+('↓' if self.descending else '↑') if self.sort else ''
        self.status.config(text=f'{start}–{self.offset+len(self.records)} nesta página'+sorting)
    def order_by(self,name):
        self.descending=not self.descending if self.sort==name else False
        self.sort=name;self.offset=0;self.reload()
    def page(self,delta):self.offset=max(0,self.offset+delta);self.reload()
    def show_dialog(self,factory):
        if self.dialog is not None and self.dialog.winfo_exists():self.dialog.lift();return self.dialog
        self.dialog=factory();return self.dialog
    def create_table(self):
        if not self.ident:raise ValueError('Selecione um banco primeiro.')
        ident=self.ident
        def save(name,columns):
            self.store.create_table(ident,name,columns)
            self.table=name.strip();self.refresh_tables()
        return self.show_dialog(lambda:TableDialog(self.winfo_toplevel(),save))
    def add_column(self):
        ident,table=self.ident,self.table
        def save(name,kind):self.store.add_column(ident,table,name,kind);self.reload()
        return self.show_dialog(lambda:ColumnDialog(self.winfo_toplevel(),save))
    def insert(self):
        ident,table=self.ident,self.table
        def save(values):self.store.insert_record(ident,table,values);self.reload()
        return self.show_dialog(lambda:RecordDialog(self.winfo_toplevel(),self.info,save))
    def selected_record(self):
        selected=self.data_grid.selection()
        if not selected:raise ValueError('Selecione uma linha para continuar.')
        return self.records[selected[0]]
    def edit(self):
        record=self.selected_record();ident,table=self.ident,self.table
        def save(values):self.store.change_record(ident,table,record,values);self.reload()
        return self.show_dialog(lambda:RecordDialog(self.winfo_toplevel(),self.info,save,record))
    def double_click(self,event):
        if self.data_grid.identify_region(event.x,event.y)=='cell':self.guard(self.edit)
    def delete(self):
        record=self.selected_record()
        if messagebox.askyesno('Excluir registro','Excluir somente a linha selecionada da tabela '+self.table+'?',parent=self):
            self.store.change_record(self.ident,self.table,record,delete=True);self.reload()
    def drop(self):
        ident,table=self.ident,self.table
        def save():self.store.drop_table(ident,table);self.table=None;self.refresh_tables()
        return self.show_dialog(lambda:DropDialog(self.winfo_toplevel(),table,save))
