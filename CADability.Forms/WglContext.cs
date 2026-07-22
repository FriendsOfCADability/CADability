using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace CADability.Forms
{
    internal static class WglContext
    {
        #region GDI / WGL P/Invoke (context lifecycle only)

        [DllImport("gdi32.dll")] static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR ppfd);
        [DllImport("gdi32.dll")] static extern bool SetPixelFormat(IntPtr hdc, int iPixelFormat, ref PIXELFORMATDESCRIPTOR ppfd);
        [DllImport("gdi32.dll")] static extern bool SwapBuffers(IntPtr hdc);
        [DllImport("opengl32.dll")] static extern IntPtr wglCreateContext(IntPtr hdc);
        [DllImport("opengl32.dll")] static extern bool wglDeleteContext(IntPtr hglrc);
        [DllImport("opengl32.dll")] static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
        [DllImport("opengl32.dll")] static extern IntPtr wglGetProcAddress(string lpszProc);
        [DllImport("opengl32.dll")] static extern IntPtr wglGetCurrentContext();
        [DllImport("opengl32.dll")] static extern bool wglShareLists(IntPtr hglrc1, IntPtr hglrc2);

        [StructLayout(LayoutKind.Sequential)]
        struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize;
            public ushort nVersion;
            public uint dwFlags;
            public byte iPixelType;
            public byte cColorBits;
            public byte cRedBits, cRedShift;
            public byte cGreenBits, cGreenShift;
            public byte cBlueBits, cBlueShift;
            public byte cAlphaBits, cAlphaShift;
            public byte cAccumBits;
            public byte cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
            public byte cDepthBits;
            public byte cStencilBits;
            public byte cAuxBuffers;
            public byte iLayerType;
            public byte bReserved;
            public uint dwLayerMask;
            public uint dwVisibleMask;
            public uint dwDamageMask;
        }

        const uint PFD_DRAW_TO_WINDOW = 0x00000004;
        const uint PFD_SUPPORT_OPENGL = 0x00000020;
        const uint PFD_DOUBLEBUFFER = 0x00000001;
        const byte PFD_TYPE_RGBA = 0;
        const byte PFD_MAIN_PLANE = 0;

        #endregion

        /// <summary>
        /// Gets the DC of the window and sets the standard pixel format on it, so that the
        /// shared rendering context can be made current against it.
        /// </summary>
        public static IntPtr PrepareDC(IntPtr hwnd)
        {
            IntPtr hdc = GetDC(hwnd);
            if (hdc == IntPtr.Zero)
                throw new InvalidOperationException("GetDC failed");

            var pfd = new PIXELFORMATDESCRIPTOR
            {
                nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
                nVersion = 1,
                dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
                iPixelType = PFD_TYPE_RGBA,
                cColorBits = 32,
                cDepthBits = 24,
                cStencilBits = 8,
                iLayerType = PFD_MAIN_PLANE
            };

            int pixelFormat = ChoosePixelFormat(hdc, ref pfd);
            if (pixelFormat == 0)
                throw new InvalidOperationException("ChoosePixelFormat failed");

            if (!SetPixelFormat(hdc, pixelFormat, ref pfd))
                throw new InvalidOperationException("SetPixelFormat failed");

            return hdc;
        }

        public static IntPtr CreateContext(IntPtr hdc)
        {
            IntPtr hglrc = wglCreateContext(hdc);
            if (hglrc == IntPtr.Zero)
                throw new InvalidOperationException("wglCreateContext failed");
            return hglrc;
        }

        public static void MakeCurrent(IntPtr hdc, IntPtr hglrc)
        {
            if (!wglMakeCurrent(hdc, hglrc))
                throw new InvalidOperationException("wglMakeCurrent failed");
        }

        public static void Present(IntPtr hdc)
        {
            SwapBuffers(hdc);
        }

        public static void ReleaseDeviceContext(IntPtr hdc, IntPtr hwnd)
        {
            ReleaseDC(hwnd, hdc);
        }

        public static GL CreateSilkGL()
        {
            return GL.GetApi(name =>
            {
                IntPtr ptr = wglGetProcAddress(name);
                if (ptr == IntPtr.Zero || (long)ptr is 1 or 2 or 3 or -1)
                {
                    // fall back to opengl32.dll for core 1.x functions
                    IntPtr lib = NativeLibrary.Load("opengl32.dll");
                    NativeLibrary.TryGetExport(lib, name, out ptr);
                }
                return ptr;
            });
        }

        public static void ShareLists(IntPtr src, IntPtr dst)
        {
            wglShareLists(src, dst);
        }
    }
}
