import os
import tkinter as tk
from tkinter import messagebox, filedialog
from send2trash import send2trash
import functools
from tkinterdnd2 import TkinterDnD, DND_FILES

def scan_empty_files(root_dir):
    empty_files = []
    for dirpath, dirnames, filenames in os.walk(root_dir):
        for filename in filenames:
            filepath = os.path.join(dirpath, filename)
            filepath = os.path.abspath(filepath)
            try:
                if os.path.getsize(filepath) == 0:
                    empty_files.append(filepath)
            except OSError:
                # 跳过无法访问的文件
                pass
    return empty_files

def delete_file(filepath, label, button):
    try:
        send2trash(filepath)
        label.config(text=f"[已删除到回收站] {filepath}")
        button.config(state=tk.DISABLED)
    except Exception as e:
        messagebox.showerror("错误", f"删除失败: {str(e)}\n路径: {filepath}")

def add_folder(listbox):
    folder = filedialog.askdirectory(title="选择要添加的文件夹")
    if folder:
        listbox.insert(tk.END, folder)

def on_drop(event, listbox):
    data = event.data
    paths = event.widget.tk.splitlist(data)
    for path in paths:
        if os.path.isdir(path):
            listbox.insert(tk.END, path)

def open_folder(filepath):
    folder = os.path.dirname(filepath)
    os.startfile(folder)

def rescan(selection_frame, results_frame, folders_list):
    results_frame.pack_forget()
    folders_list.delete(0, tk.END)
    selection_frame.pack()

def scan_folders(listbox, selection_frame, results_frame):
    folders = list(listbox.get(0, tk.END))
    if not folders:
        messagebox.showwarning("警告", "请先添加文件夹")
        return

    all_empty_files = []
    for folder in folders:
        empty_files = scan_empty_files(folder)
        all_empty_files.extend(empty_files)

    # 切换到结果界面
    selection_frame.pack_forget()
    results_frame.pack(fill="both", expand=True)

    # 清空结果frame
    for widget in results_frame.winfo_children():
        widget.destroy()

    if not all_empty_files:
        tk.Label(results_frame, text="未找到0KB大小的文件。").pack(pady=20)
        rescan_button = tk.Button(results_frame, text="重新扫描", command=lambda: rescan(selection_frame, results_frame, listbox))
        rescan_button.pack(pady=10)
    else:
        tk.Label(results_frame, text="找到以下0KB文件:").pack(pady=10)

        canvas = tk.Canvas(results_frame)
        scrollbar = tk.Scrollbar(results_frame, orient="vertical", command=canvas.yview)
        scrollable_frame = tk.Frame(canvas)

        scrollable_frame.bind(
            "<Configure>",
            lambda e: canvas.configure(scrollregion=canvas.bbox("all"))
        )

        canvas.create_window((0, 0), window=scrollable_frame, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)

        # 绑定鼠标滚轮
        def on_mousewheel(event):
            canvas.yview_scroll(int(-1*(event.delta/120)), "units")
        canvas.bind("<MouseWheel>", on_mousewheel)

        for filepath in all_empty_files:
            frame = tk.Frame(scrollable_frame)
            frame.pack(fill="x", pady=2)

            label = tk.Label(frame, text=filepath, anchor="w")
            label.pack(side="left", fill="x", expand=True)

            open_button = tk.Button(frame, text="打开文件夹")
            open_button.pack(side="right", padx=5)
            open_button.config(command=functools.partial(open_folder, filepath))

            delete_button = tk.Button(frame, text="删除")
            delete_button.pack(side="right")
            delete_button.config(command=functools.partial(delete_file, filepath, label, delete_button))

        canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")

        rescan_button = tk.Button(results_frame, text="重新扫描", command=lambda: rescan(selection_frame, results_frame, listbox))
        rescan_button.pack(pady=10)

def main():
    root = TkinterDnD.Tk()
    root.title("0KB文件扫描器")
    root.geometry("600x400")

    selection_frame = tk.Frame(root)
    selection_frame.pack(fill="both", expand=True)

    # 选定文件夹列表
    tk.Label(selection_frame, text="选定的文件夹:").pack(pady=5)
    folders_list = tk.Listbox(selection_frame, height=5)
    folders_list.pack(pady=5, fill="x")

    # 按钮
    button_frame = tk.Frame(selection_frame)
    button_frame.pack(pady=5)
    add_button = tk.Button(button_frame, text="添加文件夹", command=lambda: add_folder(folders_list))
    add_button.pack(side="left", padx=5)
    scan_button = tk.Button(button_frame, text="扫描", command=lambda: scan_folders(folders_list, selection_frame, results_frame))
    scan_button.pack(side="left", padx=5)

    # 设置拖拽到selection_frame
    selection_frame.drop_target_register(DND_FILES)
    selection_frame.dnd_bind('<<Drop>>', lambda e: on_drop(e, folders_list))

    results_frame = tk.Frame(root)

    root.mainloop()

if __name__ == "__main__":
    main()