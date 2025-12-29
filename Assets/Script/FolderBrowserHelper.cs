using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class FolderBrowserHelper
{
    public static string GetFolder()
    {
        string resultPath = null;
        
        // 1. Siapkan memori buffer untuk nama display (Wajib dialokasikan)
        // Kita pakai IntPtr (Pointer) bukan string agar memori tidak geser
        IntPtr bufferDisplayName = Marshal.AllocHGlobal(MAX_PATH * 2); // *2 karena Unicode (2 byte per char)

        try
        {
            BROWSEINFO bi = new BROWSEINFO();
            bi.hwndOwner = GetActiveWindow();
            bi.pidlRoot = IntPtr.Zero;
            bi.pszDisplayName = bufferDisplayName; // Pointer ke buffer yang kita buat
            bi.lpszTitle = "Pilih Folder Input/Output";
            bi.ulFlags = BIF_RETURNONLYFSDIRS | BIF_USENEWUI | BIF_NOCREATEDIRS;
            bi.lpfn = IntPtr.Zero;
            bi.lParam = IntPtr.Zero;
            bi.iImage = 0;

            // 2. Buka Jendela Browse
            IntPtr pidl = SHBrowseForFolder(ref bi);

            if (pidl != IntPtr.Zero)
            {
                // 3. Siapkan memori untuk Path Hasil
                IntPtr bufferPath = Marshal.AllocHGlobal(MAX_PATH * 2);

                try
                {
                    // 4. Minta Windows tulis Path ke buffer memori kita
                    if (SHGetPathFromIDList(pidl, bufferPath))
                    {
                        // 5. Baca memori tersebut menjadi String C# (Unicode)
                        resultPath = Marshal.PtrToStringUni(bufferPath);
                    }
                }
                finally
                {
                    // Bersihkan memori path
                    Marshal.FreeHGlobal(bufferPath);
                }

                // Bersihkan pointer hasil Windows (PIDL)
                Marshal.FreeCoTaskMem(pidl);
            }
        }
        finally
        {
            // Bersihkan memori display name
            Marshal.FreeHGlobal(bufferDisplayName);
        }

        return resultPath;
    }

    // --- DEFINISI STRUKTUR SESUAI STANDAR WINDOWS 64-BIT ---
    
    private const int MAX_PATH = 260;
    private const uint BIF_RETURNONLYFSDIRS = 0x0001;
    private const uint BIF_USENEWUI = 0x0040;
    private const uint BIF_NOCREATEDIRS = 0x0200;

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    // Gunakan CharSet.Unicode dan ref struct
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

    // Gunakan IntPtr untuk buffer path agar kita bisa kontrol manual
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName; // Kita ubah jadi IntPtr (Pointer) agar stabil
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }
}