"""Nexus visual components and native Windows window styling."""
from pathlib import Path
import ctypes
import sys
import customtkinter as ctk
from PIL import Image

BG='#0d101b'
PANEL='#151a2b'
SURFACE='#1e253b'
TEXT='#f1f0fb'
MUTED='#9ba6c3'
ACCENT='#b5a2ff'
ASSETS=Path(getattr(sys,'_MEIPASS',Path(__file__).parent))/'assets'

class Label(ctk.CTkLabel):
    def config(self,**kwargs):
        if 'fg' in kwargs: kwargs['text_color']=kwargs.pop('fg')
        return self.configure(**kwargs)

class Button(ctk.CTkButton):
    def state(self,states):
        self.configure(state='disabled' if 'disabled' in states else 'normal')

def label(parent,text,size=11,color=TEXT,bold=False,**kwargs):
    return Label(parent,text=text,text_color=color,font=('Segoe UI',size,'bold' if bold else 'normal'),anchor='w',justify='left',**kwargs)

def button(parent,text,command,primary=False,danger=False,**kwargs):
    return Button(parent,text=text,command=command,corner_radius=12,height=39,
                  fg_color=ACCENT if primary else SURFACE,
                  hover_color='#c6b8ff' if primary else '#2a3450',
                  text_color='#171127' if primary else '#ffabb9' if danger else TEXT,
                  font=('Segoe UI',11,'bold' if primary else 'normal'),**kwargs)

def frame(parent,color='transparent',radius=0,**kwargs):
    return ctk.CTkFrame(parent,fg_color=color,corner_radius=radius,**kwargs)

def load_logo(size):
    path=ASSETS/'nexus.png'
    if path.exists(): return ctk.CTkImage(Image.open(path),size=(size,size))
    return None

def brand(parent,size=30):
    row=frame(parent)
    logo=load_logo(size)
    if logo:
        icon=ctk.CTkLabel(row,text='',image=logo)
        icon.image=logo
        icon.pack(side='left',padx=(0,10))
    label(row,'nexus',size=22,bold=True).pack(side='left')
    return row

def style_window(window):
    """Keep system resize/snap/Alt+Tab behavior and color the native caption."""
    icon=ASSETS/'nexus.ico'
    if icon.exists(): window.iconbitmap(str(icon))
    if sys.platform!='win32': return
    try:
        user32=ctypes.windll.user32
        user32.GetParent.argtypes=[ctypes.c_void_p]
        user32.GetParent.restype=ctypes.c_void_p
        hwnd=user32.GetParent(window.winfo_id())
        dwm=ctypes.windll.dwmapi.DwmSetWindowAttribute
        dwm.argtypes=[ctypes.c_void_p,ctypes.c_uint,ctypes.c_void_p,ctypes.c_uint]
        def set_attribute(key,value):
            item=ctypes.c_uint(value)
            return dwm(hwnd,key,ctypes.byref(item),ctypes.sizeof(item))
        set_attribute(20,1)
        set_attribute(33,2)
        set_attribute(34,0x002B1A15)
        set_attribute(35,0x001B100D)
        set_attribute(36,0x00FBF0F1)
        ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID('Nexus.DatabaseStudio')
    except (OSError,AttributeError):
        pass  # Older Windows versions retain their supported native appearance.

def center_window(window,width=None,height=None,parent=None):
    """Center a restored window within its monitor's usable desktop area."""
    if width is not None:
        window.state('normal')
        window.geometry(f'{width}x{height}')
    window.update_idletasks()
    left,top=0,0
    right,bottom=window.winfo_screenwidth(),window.winfo_screenheight()
    if sys.platform=='win32':
        try:
            from ctypes import wintypes
            class MonitorInfo(ctypes.Structure):
                _fields_=[('size',wintypes.DWORD),('monitor',wintypes.RECT),('work',wintypes.RECT),('flags',wintypes.DWORD)]
            user=ctypes.windll.user32
            user.MonitorFromWindow.argtypes=[wintypes.HWND,wintypes.DWORD]
            user.MonitorFromWindow.restype=wintypes.HANDLE
            user.GetMonitorInfoW.argtypes=[wintypes.HANDLE,ctypes.POINTER(MonitorInfo)]
            info=MonitorInfo(); info.size=ctypes.sizeof(info)
            handle=user.MonitorFromWindow((parent or window).winfo_id(),2)
            if user.GetMonitorInfoW(handle,ctypes.byref(info)):
                left,top,right,bottom=info.work.left,info.work.top,info.work.right,info.work.bottom
        except (OSError,AttributeError): pass
    actual_width,actual_height=window.winfo_width(),window.winfo_height()
    if parent:
        x=parent.winfo_rootx()+(parent.winfo_width()-actual_width)//2
        y=parent.winfo_rooty()+(parent.winfo_height()-actual_height)//2
    else:
        x=left+(right-left-actual_width)//2
        y=top+(bottom-top-actual_height)//2
    x=max(left,min(x,right-actual_width))
    y=max(top+30,min(y,bottom-actual_height-12))
    window.geometry(f'+{x}+{y}')

class NameDialog(ctk.CTkToplevel):
    """Modeless event loop, modal interaction, inline validation and themed controls."""
    def __init__(self,parent,on_submit):
        super().__init__(parent)
        self.withdraw()
        self.title('Nexus  —  Novo banco de dados')
        self.configure(fg_color=BG)
        self.resizable(False,False)
        self.transient(parent)
        self.on_submit=on_submit
        content=frame(self,PANEL,24)
        content.pack(fill='both',expand=True,padx=18,pady=18)
        brand(content,32).pack(anchor='w',padx=26,pady=(24,18))
        label(content,'UM NOVO COMEÇO',10,ACCENT,True).pack(anchor='w',padx=26)
        label(content,'Crie seu banco de dados',23,bold=True).pack(anchor='w',padx=26,pady=(6,8))
        label(content,'Escolha um nome para organizar seu próximo projeto.',11,MUTED).pack(anchor='w',padx=26,pady=(0,20))
        label(content,'Nome do banco',11,bold=True).pack(anchor='w',padx=26,pady=(0,8))
        self.name=ctk.CTkEntry(content,height=46,corner_radius=13,fg_color=BG,border_color='#36324f',placeholder_text='Ex.: Clientes, Estoque, Meu projeto')
        self.name.pack(fill='x',padx=26)
        label(content,'SQLite local  ·  Até 100 caracteres',10,MUTED).pack(anchor='w',padx=26,pady=(8,0))
        self.feedback=label(content,'',10,'#ffa4ba',wraplength=430)
        self.feedback.pack(fill='x',padx=26,pady=(6,8))
        actions=frame(content)
        actions.pack(fill='x',padx=26,pady=(0,24))
        button(actions,'Cancelar',self.close,width=120).pack(side='left')
        self.submit_button=button(actions,'Criar banco   →',self.submit,primary=True,width=165)
        self.submit_button.pack(side='right')
        self.bind('<Escape>',lambda event:self.close())
        self.name.bind('<Return>',lambda event:self.submit())
        self.protocol('WM_DELETE_WINDOW',self.close)
        self.geometry('540x500')
        self.deiconify()
        center_window(self,parent=parent)
        self.grab_set()
        self.after(250,lambda:style_window(self) if self.winfo_exists() else None)
        self.after(100,self.name.focus_set)

    def submit(self):
        name=self.name.get().strip()
        if not name or len(name)>100:
            self.feedback.config(text='Informe um nome com 1 a 100 caracteres.')
            self.name.focus_set()
            return
        self.submit_button.state(['disabled'])
        try: self.on_submit(name)
        except Exception as error:
            self.feedback.config(text=str(error))
            self.submit_button.state(['!disabled'])
            return
        self.close()

    def close(self):
        self.grab_release()
        self.destroy()
