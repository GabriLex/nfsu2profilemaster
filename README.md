# NFSU2 Profile Master 🏁

A lightweight, user-friendly Python utility designed to edit savegames in **Need for Speed Underground 2**. It handles the technical heavy lifting like finding hidden save paths and bypassing Windows folder permissions.

## 🖼️ Screenshots

| Italian Interface | English Interface |
| :---: | :---: |
| ![Italian UI](patcher_it.png) | ![English UI](patcher.png) |

## ✨ Features

* **Auto-Detection**: Automatically finds save games in `%LOCALAPPDATA%`, `VirtualStore`, and common installation paths (including Steam versions).
* **Safety First**: Built-in "Extract" and "Restore" system to create backups before any modification.
* **Admin Privileges**: Automatically requests UAC elevation to ensure it can write save data in protected directories.
* **Bilingual**: Full support for both **Italian** and **English**.
* **Deep Scan**: Recursively searches for profile-specific files (save files with no extension) across multiple drives.
* **Rename Option**
* **VEHICLE SWAP !(SLOT 1)**
* **UNLOCK PARTS OPTION**
* **SAVEGAME CAR EXTRACTION AND PATCHING**
* **EVERY CUSTOM CAR SWAPPING**

## 🛠️ How it Works

The tool modifies the save file at a specific hex offset (**41322**) where NFSU2 stores the career credit value using a little-endian 4-byte integer.


## 📦 Compiling to .exe

If you want to create a standalone executable for Windows, use **PyInstaller**:


pyinstaller --noconfirm --onefile --windowed --add-data "icona.ico;." --icon "icona.ico" --uac-admin nfsu2_profile_master.py


## License
GPL 3 
