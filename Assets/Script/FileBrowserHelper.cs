using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

public class FileBrowserHelper : MonoBehaviour
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    // Buffer harus BESAR untuk menampung banyak file
    private const int BUFFER_SIZE = 8192; 

    // Fungsi untuk MEMILIH BANYAK FILE (Return Array of Strings)
    public static string[] OpenFiles(string title, string filter)
    {
        OpenFileName ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = filter;
        ofn.file = new string(new char[BUFFER_SIZE]); // Buffer besar
        ofn.maxFile = ofn.file.Length;
        ofn.fileTitle = new string(new char[64]);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.initialDir = UnityEngine.Application.dataPath;
        ofn.title = title;
        // Flag 0x00000200 = OFN_ALLOWMULTISELECT (Penting!)
        // Flag 0x00080000 = OFN_EXPLORER
        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008; 
        ofn.ownerWindow = GetActiveWindow();

        if (GetOpenFileName(ofn))
        {
            // Trik Parsing Hasil Multi-Select Windows
            // Format Windows: "Directory Path" + \0 + "File1" + \0 + "File2" ...
            string raw = ofn.file;
            string[] parts = raw.Split('\0');

            if (parts.Length == 1 || (parts.Length > 1 && string.IsNullOrEmpty(parts[1])))
            {
                // Kasus 1: Cuma pilih 1 file
                return new string[] { parts[0] };
            }
            else
            {
                // Kasus 2: Pilih banyak file
                string dir = parts[0];
                List<string> finalPaths = new List<string>();
                
                // Loop mulai index 1 karena index 0 adalah folder
                for (int i = 1; i < parts.Length; i++)
                {
                    if (string.IsNullOrEmpty(parts[i])) break; // Berhenti jika ketemu null lagi
                    finalPaths.Add(Path.Combine(dir, parts[i]));
                }
                return finalPaths.ToArray();
            }
        }
        return null; 
    }

    // Fungsi Single File (Tetap dipertahankan untuk PAN)
    public static string OpenFile(string title, string filter)
    {
        string[] res = OpenFiles(title, filter);
        if (res != null && res.Length > 0) return res[0];
        return null;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public class OpenFileName
{
    public int structSize = 0;
    public IntPtr ownerWindow = IntPtr.Zero;
    public IntPtr instance = IntPtr.Zero;
    public String filter = null;
    public String customFilter = null;
    public int maxCustomFilter = 0;
    public int filterIndex = 0;
    public String file = null;
    public int maxFile = 0;
    public String fileTitle = null;
    public int maxFileTitle = 0;
    public String initialDir = null;
    public String title = null;
    public int flags = 0;
    public short fileOffset = 0;
    public short fileExtension = 0;
    public String defExt = null;
    public IntPtr custData = IntPtr.Zero;
    public IntPtr hook = IntPtr.Zero;
    public String templateName = null;
    public IntPtr reservedPtr = IntPtr.Zero;
    public int reservedInt = 0;
    public int flagsEx = 0;
}