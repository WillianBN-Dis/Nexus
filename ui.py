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
