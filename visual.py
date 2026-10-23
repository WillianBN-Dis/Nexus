"""Visual table designer and record editor for the same SQLite databases as SQL mode."""
import math
import tkinter as tk
from tkinter import ttk,messagebox
import customtkinter as ctk
from ui import BG,PANEL,SURFACE,TEXT,MUTED,ACCENT,frame,label,button,brand,center_window,style_window,Tabs

TYPES=['TEXT','INTEGER','REAL','BLOB']

class FormDialog(ctk.CTkToplevel):
    def __init__(self,parent,title,width=700,height=620):
        super().__init__(parent)
        self.title('Nexus  —  '+title)
        self.configure(fg_color=BG)
        self.geometry(f'{width}x{height}');self.minsize(550,420)
        self.transient(parent)
        top=frame(self,PANEL,20);top.pack(fill='x',padx=18,pady=18)
        brand(top,28).pack(anchor='w',padx=22,pady=(16,8))
        label(top,title,23,bold=True).pack(anchor='w',padx=22,pady=(0,18))
        footer=frame(self);footer.pack(side='bottom',fill='x',padx=22,pady=(8,18))
        self.feedback=label(footer,'',11,'#ffabb9',wraplength=620);self.feedback.pack(fill='x',pady=(0,8))
        actions=frame(footer);actions.pack(fill='x')
        button(actions,'Cancelar',self.close).pack(side='left')
        self.save_button=button(actions,'Salvar',self.submit,primary=True);self.save_button.pack(side='right')
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
        super().__init__(parent,'Criar uma tabela',780,660)
        self.save_callback=save
        label(self.body,'Nome da tabela',12,bold=True).pack(anchor='w',padx=14,pady=(12,8))
        self.name=ctk.CTkEntry(self.body,placeholder_text='Ex.: clientes',height=42)
        self.name.pack(fill='x',padx=14)
        label(self.body,'A coluna id é criada automaticamente como chave primária.',11,MUTED).pack(anchor='w',padx=14,pady=(12,18))
        self.columns_frame=frame(self.body);self.columns_frame.pack(fill='x',padx=10)
        self.rows=[]
        button(self.body,'＋ Adicionar coluna',self.add_field).pack(anchor='w',padx=14,pady=14)
        self.add_field()
    def add_field(self):
        row=frame(self.columns_frame,SURFACE,12);row.pack(fill='x',pady=5)
        name=ctk.CTkEntry(row,placeholder_text='Nome da coluna',height=36,width=220);name.pack(side='left',fill='x',expand=True,padx=8,pady=10)
        kind=ctk.CTkOptionMenu(row,values=TYPES,width=115,fg_color=PANEL,button_color='#453960');kind.pack(side='left',padx=6)
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
        self.name=ctk.CTkEntry(self.body,height=42);self.name.pack(fill='x',padx=14)
        label(self.body,'Tipo de dado',12,bold=True).pack(anchor='w',padx=14,pady=(16,8))
        self.kind=ctk.CTkOptionMenu(self.body,values=TYPES);self.kind.pack(anchor='w',padx=14)
        label(self.body,'Os registros existentes receberão NULL nesta coluna.',11,MUTED).pack(anchor='w',padx=14,pady=18)
    def save(self): self.save_callback(self.name.get(),self.kind.get())

class RecordDialog(FormDialog):
    def __init__(self,parent,info,save,record=None):
        super().__init__(parent,'Editar registro' if record else 'Novo registro',720,650)
        self.save_callback=save;self.fields=[];self.record=record
        label(self.body,'Escolha Valor, NULL ou Padrão para cada campo. BLOB usa hexadecimal.',11,MUTED,wraplength=620).pack(anchor='w',padx=14,pady=12)
        for index,col in enumerate(info['columns']):
            if col['hidden']: continue
            box=frame(self.body,SURFACE,12);box.pack(fill='x',padx=10,pady=6)
            label(box,col['name']+'  ·  '+(col['type'] or 'Sem tipo')+('  ·  chave' if col['pk'] else ''),12,bold=True).pack(anchor='w',padx=12,pady=(10,6))
            entry=ctk.CTkEntry(box,height=38);entry.pack(fill='x',padx=12)
            states=['Valor','NULL'] if record else ['Valor','NULL','Padrão']
            mode=ctk.CTkOptionMenu(box,values=states,width=130);mode.pack(anchor='w',padx=12,pady=10)
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
        self.name=ctk.CTkEntry(self.body,height=42);self.name.pack(fill='x',padx=14)
        self.save_button.configure(text='Excluir tabela',fg_color='#6c3049',hover_color='#8a3c5b',text_color=TEXT)
    def save(self):
        if self.name.get()!=self.table: raise ValueError('Digite exatamente o nome da tabela.')
        self.save_callback()

class VisualEditor(ctk.CTkFrame):
    def __init__(self,parent,store,guard):
        super().__init__(parent,fg_color='transparent')
        self.store=store;self.guard=guard;self.ident=None;self.table=None
        self.info=None;self.offset=0;self.sort=None;self.descending=False;self.dialog=None
        top=frame(self);top.pack(fill='x',pady=(8,12))
        label(top,'Tabelas do banco',12,bold=True).pack(side='left',padx=(0,12))
        self.table_choice=ctk.CTkOptionMenu(top,values=['Selecione um banco'],width=240,command=self.choose_table,fg_color=SURFACE,button_color='#453960')
        self.table_choice.pack(side='left')
        self.create_button=button(top,'＋ Criar tabela',lambda:self.guard(self.create_table),primary=True)
        self.create_button.pack(side='right')
        tools=frame(self);tools.pack(fill='x',pady=(0,10))
        self.actions=[]
        for text,action in [('＋ Registro',self.insert),('Editar',self.edit),('Excluir linha',self.delete),('＋ Coluna',self.add_column),('Excluir tabela',self.drop)]:
            item=button(tools,text,lambda func=action:self.guard(func),width=105,danger=text.startswith('Excluir'))
            item.pack(side='left',padx=(0,6));self.actions.append(item)
        self.tabs=Tabs(self,fg_color=PANEL,segmented_button_fg_color=SURFACE,segmented_button_selected_color='#453960',anchor='nw')
        self.tabs.pack(fill='both',expand=True)
        self.tabs.add('Dados');self.tabs.add('Estrutura')
        data=self.tabs.tab('Dados');data.rowconfigure(0,weight=1);data.columnconfigure(0,weight=1)
        self.data_grid=ttk.Treeview(data,show='headings',selectmode='browse')
        self.data_grid.grid(row=0,column=0,sticky='nsew')
        self.data_grid.bind('<Double-1>',self.double_click)
        self.data_grid.tag_configure('alternate',background='#1c2235')
        sy=ctk.CTkScrollbar(data,command=self.data_grid.yview);sy.grid(row=0,column=1,sticky='ns')
        sx=ctk.CTkScrollbar(data,orientation='horizontal',command=self.data_grid.xview);sx.grid(row=1,column=0,sticky='ew')
        self.data_grid.configure(yscrollcommand=sy.set,xscrollcommand=sx.set)
        structure=self.tabs.tab('Estrutura');structure.rowconfigure(0,weight=1);structure.columnconfigure(0,weight=1)
        self.schema=ttk.Treeview(structure,columns=('name','type','required','key'),show='headings')
        self.schema.grid(row=0,column=0,sticky='nsew')
        for key,title in [('name','Coluna'),('type','Tipo'),('required','Obrigatória'),('key','Chave primária')]:
            self.schema.heading(key,text=title);self.schema.column(key,width=160,stretch=True)
        scroll=ctk.CTkScrollbar(structure,command=self.schema.yview);scroll.grid(row=0,column=1,sticky='ns');self.schema.configure(yscrollcommand=scroll.set)
        bottom=frame(self);bottom.pack(fill='x',pady=(10,0))
        self.status=label(bottom,'Selecione um banco para começar.',11,MUTED);self.status.pack(side='left')
        self.next_button=button(bottom,'Próxima →',lambda:self.guard(lambda:self.page(200)),width=105);self.next_button.pack(side='right')
        self.previous_button=button(bottom,'← Anterior',lambda:self.guard(lambda:self.page(-200)),width=105);self.previous_button.pack(side='right',padx=8)
        self.set_database(None)
    def set_database(self,ident):
        self.ident=ident;self.table=None;self.offset=0;self.sort=None;self.descending=False
        self.refresh_tables()
    def refresh_tables(self):
        tables=self.store.tables(self.ident) if self.ident else []
        self.table_choice.configure(values=tables or ['Nenhuma tabela'])
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
        self.data_grid['columns']=();self.records={};self.info=None
        for item in self.actions:item.state(['disabled'])
        self.previous_button.state(['disabled']);self.next_button.state(['disabled'])
        if not self.ident or not self.table:
            self.status.config(text='Crie sua primeira tabela para organizar os dados.' if self.ident else 'Selecione um banco para começar.');return
        self.info=self.store.browse_table(self.ident,self.table,self.sort,self.descending,self.offset)
        columns=self.info['columns'];keys=[str(i) for i in range(len(columns))];self.data_grid['columns']=keys
        for key,col in zip(keys,columns):
            arrow=(' ↓' if self.descending else ' ↑') if self.sort==col['name'] else ''
            self.data_grid.heading(key,text=col['name']+arrow,command=lambda name=col['name']:self.guard(lambda:self.order_by(name)))
            self.data_grid.column(key,width=180,stretch=False)
            self.schema.insert('','end',values=(col['name'],col['type'] or 'Sem tipo','Sim' if col['required'] else 'Não','Sim' if col['pk'] else 'Não'))
        for index,record in enumerate(self.info['rows']):
            key=str(index);self.records[key]=record
            values=['NULL' if value is None else '0x'+value.hex() if isinstance(value,bytes) else str(value) for value in record['values']]
            self.data_grid.insert('','end',iid=key,values=values,tags=('alternate',) if index%2 else ())
        for item in self.actions:item.state(['!disabled'])
        if not self.info['identity']:
            self.actions[1].state(['disabled']);self.actions[2].state(['disabled'])
        self.previous_button.state(['!disabled'] if self.offset else ['disabled'])
        self.next_button.state(['!disabled'] if self.info['more'] else ['disabled'])
        start=self.offset+1 if self.records else 0
        self.status.config(text=f'{start}–{self.offset+len(self.records)}  ·  Clique na coluna para ordenar')
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
