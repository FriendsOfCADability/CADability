using System;
using System.Runtime.InteropServices;

namespace CADability
{
    public static class Gdi
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct KERNINGPAIR
        {
            public short wFirst;
            public short wSecond;
            public int iKernAmount;
        }

        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);

        [DllImport("gdi32.dll", EntryPoint = "GetKerningPairsW")]
        public static extern int GetKerningPairs(IntPtr hdc, int nPairs, KERNINGPAIR[] lpkrnpair);
    }
}
