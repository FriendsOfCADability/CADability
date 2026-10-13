using System;
using System.Runtime.InteropServices;
using System.Security;

namespace CADability
{
    public static class Gdi
    {
        private const CallingConvention CALLING_CONVENTION = CallingConvention.StdCall;
        private const string GDI_NATIVE_LIBRARY = "gdi32.dll";

        #region int PFD_TYPE_RGBA
        /// <summary>
        ///     RGBA pixels.  Each pixel has four components in this order: red, green, blue,
        ///     and alpha.
        /// </summary>
        // #define PFD_TYPE_RGBA        0
        public const int PFD_TYPE_RGBA = 0;
        #endregion int PFD_TYPE_RGBA

        #region int PFD_TYPE_COLORINDEX
        /// <summary>
        ///     Color-index pixels.  Each pixel uses a color-index value.
        /// </summary>
        // #define PFD_TYPE_COLORINDEX  1
        public const int PFD_TYPE_COLORINDEX = 1;
        #endregion int PFD_TYPE_COLORINDEX

        #region int PFD_MAIN_PLANE
        /// <summary>
        ///     The layer is the main plane.
        /// </summary>
        // #define PFD_MAIN_PLANE       0
        public const int PFD_MAIN_PLANE = 0;
        #endregion int PFD_MAIN_PLANE

        #region int PFD_DOUBLEBUFFER
        /// <summary>
        ///     <para>
        ///         The buffer is double-buffered.  This flag and <see cref="PFD_SUPPORT_GDI" />
        ///         are mutually exclusive in the current generic implementation.
        ///     </para>
        /// </summary>
        // #define PFD_DOUBLEBUFFER            0x00000001
        public const int PFD_DOUBLEBUFFER = 0x00000001;
        #endregion int PFD_DOUBLEBUFFER

        #region int PFD_STEREO
        /// <summary>
        ///     <para>
        ///         The buffer is stereoscopic.  This flag is not supported in the current
        ///         generic implementation.
        ///     </para>
        /// </summary>
        // #define PFD_STEREO                  0x00000002
        public const int PFD_STEREO = 0x00000002;
        #endregion int PFD_STEREO

        #region int PFD_DRAW_TO_WINDOW
        /// <summary>
        ///     <para>
        ///         The buffer can draw to a window or device surface.
        ///     </para>
        /// </summary>
        // #define PFD_DRAW_TO_WINDOW          0x00000004
        public const int PFD_DRAW_TO_WINDOW = 0x00000004;
        #endregion int PFD_DRAW_TO_WINDOW

        #region int PFD_DRAW_TO_BITMAP
        /// <summary>
        ///     <para>
        ///         The buffer can draw to a memory bitmap.
        ///     </para>
        /// </summary>
        // #define PFD_DRAW_TO_BITMAP          0x00000008
        public const int PFD_DRAW_TO_BITMAP = 0x00000008;
        #endregion int PFD_DRAW_TO_BITMAP

        #region int PFD_SUPPORT_GDI
        /// <summary>
        ///     <para>
        ///         The buffer supports GDI drawing.  This flag and
        ///         <see cref="PFD_DOUBLEBUFFER" /> are mutually exclusive in the current generic
        ///         implementation.
        ///     </para>
        /// </summary>
        // #define PFD_SUPPORT_GDI             0x00000010
        public const int PFD_SUPPORT_GDI = 0x00000010;
        #endregion int PFD_SUPPORT_GDI

        #region int PFD_SUPPORT_OPENGL
        /// <summary>
        ///     <para>
        ///         The buffer supports OpenGL drawing.
        ///     </para>
        /// </summary>
        // #define PFD_SUPPORT_OPENGL          0x00000020
        public const int PFD_SUPPORT_OPENGL = 0x00000020;
        #endregion int PFD_SUPPORT_OPENGL

        #region int PFD_GENERIC_FORMAT
        /// <summary>
        ///     <para>
        ///         The pixel format is supported by the GDI software implementation, which is
        ///         also known as the generic implementation.  If this bit is clear, the pixel
        ///         format is supported by a device driver or hardware.
        ///     </para>
        /// </summary>
        // #define PFD_GENERIC_FORMAT          0x00000040
        public const int PFD_GENERIC_FORMAT = 0x00000040;
        #endregion int PFD_GENERIC_FORMAT

        #region int PFD_NEED_PALETTE
        /// <summary>
        ///     <para>
        ///         The buffer uses RGBA pixels on a palette-managed device.  A logical palette
        ///         is required to achieve the best results for this pixel type.  Colors in the
        ///         palette should be specified according to the values of the <b>cRedBits</b>,
        ///         <b>cRedShift</b>, <b>cGreenBits</b>, <b>cGreenShift</b>, <b>cBluebits</b>,
        ///         and <b>cBlueShift</b> members.  The palette should be created and realized in
        ///         the device context before calling <see cref="Wgl.wglMakeCurrent" />.
        ///     </para>
        /// </summary>
        // #define PFD_NEED_PALETTE            0x00000080
        public const int PFD_NEED_PALETTE = 0x00000080;
        #endregion int PFD_NEED_PALETTE

        #region int PFD_NEED_SYSTEM_PALETTE
        /// <summary>
        ///     <para>
        ///         Defined in the pixel format descriptors of hardware that supports one
        ///         hardware palette in 256-color mode only.  For such systems to use
        ///         hardware acceleration, the hardware palette must be in a fixed order
        ///         (for example, 3-3-2) when in RGBA mode or must match the logical palette
        ///         when in color-index mode.
        ///     </para>
        ///     <para>
        ///         When this flag is set, you must call <c>SetSystemPaletteUse</c> in
        ///         your program to force a one-to-one mapping of the logical palette and the
        ///         system palette.  If your OpenGL hardware supports multiple hardware palettes
        ///         and the device driver can allocate spare hardware palettes for OpenGL, this
        ///         flag is typically clear.
        ///     </para>
        ///     <para>
        ///         This flag is not set in the generic pixel formats.
        ///     </para>
        /// </summary>
        // #define PFD_NEED_SYSTEM_PALETTE     0x00000100
        public const int PFD_NEED_SYSTEM_PALETTE = 0x00000100;
        #endregion int PFD_NEED_SYSTEM_PALETTE

        #region int PFD_SWAP_EXCHANGE
        /// <summary>
        ///     <para>
        ///         Specifies the content of the back buffer in the double-buffered main color
        ///         plane following a buffer swap.  Swapping the color buffers causes the
        ///         exchange of the back buffer's content with the front buffer's content.
        ///         Following the swap, the back buffer's content contains the front buffer's
        ///         content before the swap. <b>PFD_SWAP_EXCHANGE</b> is a hint only and might
        ///         not be provided by a driver.
        ///     </para>
        /// </summary>
        // #define PFD_SWAP_EXCHANGE           0x00000200
        public const int PFD_SWAP_EXCHANGE = 0x00000200;
        #endregion int PFD_SWAP_EXCHANGE

        #region int PFD_SWAP_COPY
        /// <summary>
        ///     <para>
        ///         Specifies the content of the back buffer in the double-buffered main color
        ///         plane following a buffer swap.  Swapping the color buffers causes the content
        ///         of the back buffer to be copied to the front buffer.  The content of the back
        ///         buffer is not affected by the swap.  <b>PFD_SWAP_COPY</b> is a hint only and
        ///         might not be provided by a driver.
        ///     </para>
        /// </summary>
        // #define PFD_SWAP_COPY               0x00000400
        public const int PFD_SWAP_COPY = 0x00000400;
        #endregion int PFD_SWAP_COPY

        #region int PFD_SWAP_LAYER_BUFFERS
        /// <summary>
        ///     <para>
        ///         Indicates whether a device can swap individual layer planes with pixel
        ///         formats that include double-buffered overlay or underlay planes.
        ///         Otherwise all layer planes are swapped together as a group.  When this
        ///         flag is set, <c>wglSwapLayerBuffers</c> is supported.
        ///     </para>
        /// </summary>
        // #define PFD_SWAP_LAYER_BUFFERS      0x00000800
        public const int PFD_SWAP_LAYER_BUFFERS = 0x00000800;
        #endregion int PFD_SWAP_LAYER_BUFFERS

        #region int PFD_GENERIC_ACCELERATED
        /// <summary>
        ///     <para>
        ///         The pixel format is supported by a device driver that accelerates the generic
        ///         implementation.  If this flag is clear and the
        ///         <see cref="PFD_GENERIC_FORMAT" /> flag is set, the pixel format is supported
        ///         by the generic implementation only.
        ///     </para>
        /// </summary>
        // #define PFD_GENERIC_ACCELERATED     0x00001000
        public const int PFD_GENERIC_ACCELERATED = 0x00001000;
        #endregion int PFD_GENERIC_ACCELERATED

        #region int PFD_SUPPORT_DIRECTDRAW
        /// <summary>
        ///     <para>
        ///         The buffer supports DirectDraw drawing.
        ///     </para>
        /// </summary>
        // #define PFD_SUPPORT_DIRECTDRAW      0x00002000
        public const int PFD_SUPPORT_DIRECTDRAW = 0x00002000;
        #endregion int PFD_SUPPORT_DIRECTDRAW
        [StructLayout(LayoutKind.Sequential)]
        public struct PIXELFORMATDESCRIPTOR
        {
            /// <summary>
            /// Specifies the size of this data structure. This value should be set to <c>sizeof(PIXELFORMATDESCRIPTOR)</c>.
            /// </summary>
            public Int16 nSize;

            /// <summary>
            /// Specifies the version of this data structure. This value should be set to 1.
            /// </summary>
            public Int16 nVersion;

            /// <summary>
            /// A set of bit flags that specify properties of the pixel buffer. The properties are generally not mutually exclusive;
            /// you can set any combination of bit flags, with the exceptions noted.
            /// </summary>
            /// <remarks>
            ///     <para>The following bit flag constants are defined:</para>
            ///     <list type="table">
            ///			<listheader>
            ///				<term>Value</term>
            ///				<description>Meaning</description>
            ///			</listheader>
            ///			<item>
            ///				<term>PFD_DRAW_TO_WINDOW</term>
            ///				<description>The buffer can draw to a window or device surface.</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_DRAW_TO_BITMAP</term>
            ///				<description>The buffer can draw to a memory bitmap.</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_SUPPORT_GDI</term>
            ///				<description>
            ///					The buffer supports GDI drawing. This flag and PFD_DOUBLEBUFFER are mutually exclusive
            ///					in the current generic implementation.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_SUPPORT_OPENGL</term>
            ///				<description>The buffer supports OpenGL drawing.</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_GENERIC_ACCELERATED</term>
            ///				<description>
            ///					The pixel format is supported by a device driver that accelerates the generic implementation.
            ///					If this flag is clear and the PFD_GENERIC_FORMAT flag is set, the pixel format is supported by
            ///					the generic implementation only.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_GENERIC_FORMAT</term>
            ///				<description>
            ///					The pixel format is supported by the GDI software implementation, which is also known as the
            ///					generic implementation. If this bit is clear, the pixel format is supported by a device
            ///					driver or hardware.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_NEED_PALETTE</term>
            ///				<description>
            ///					The buffer uses RGBA pixels on a palette-managed device. A logical palette is required to achieve
            ///					the best results for this pixel type. Colors in the palette should be specified according to the
            ///					values of the <b>cRedBits</b>, <b>cRedShift</b>, <b>cGreenBits</b>, <b>cGreenShift</b>,
            ///					<b>cBluebits</b>, and <b>cBlueShift</b> members. The palette should be created and realized in
            ///					the device context before calling <see cref="Wgl.wglMakeCurrent" />.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_NEED_SYSTEM_PALETTE</term>
            ///				<description>
            ///					Defined in the pixel format descriptors of hardware that supports one hardware palette in
            ///					256-color mode only. For such systems to use hardware acceleration, the hardware palette must be in
            ///					a fixed order (for example, 3-3-2) when in RGBA mode or must match the logical palette when in
            ///					color-index mode.
            ///
            ///					When this flag is set, you must call SetSystemPaletteUse in your program to force a one-to-one
            ///					mapping of the logical palette and the system palette. If your OpenGL hardware supports multiple
            ///					hardware palettes and the device driver can allocate spare hardware palettes for OpenGL, this
            ///					flag is typically clear.
            ///
            ///					This flag is not set in the generic pixel formats.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_DOUBLEBUFFER</term>
            ///				<description>
            ///					The buffer is double-buffered. This flag and PFD_SUPPORT_GDI are mutually exclusive in the
            ///					current generic implementation.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_STEREO</term>
            ///				<description>
            ///					The buffer is stereoscopic. This flag is not supported in the current generic implementation.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_SWAP_LAYER_BUFFERS</term>
            ///				<description>
            ///					Indicates whether a device can swap individual layer planes with pixel formats that include
            ///					double-buffered overlay or underlay planes. Otherwise all layer planes are swapped together
            ///					as a group. When this flag is set, <b>wglSwapLayerBuffers</b> is supported.
            ///				</description>
            ///			</item>
            ///		</list>
            ///		<para>You can specify the following bit flags when calling <see cref="ChoosePixelFormat" />.</para>
            ///		<list type="table">
            ///			<listheader>
            ///				<term>Value</term>
            ///				<description>Meaning</description>
            ///			</listheader>
            ///			<item>
            ///				<term>PFD_DEPTH_DONTCARE</term>
            ///				<description>
            ///					The requested pixel format can either have or not have a depth buffer. To select
            ///					a pixel format without a depth buffer, you must specify this flag. The requested pixel format
            ///					can be with or without a depth buffer. Otherwise, only pixel formats with a depth buffer
            ///					are considered.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_DOUBLEBUFFER_DONTCARE</term>
            ///				<description>The requested pixel format can be either single- or double-buffered.</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_STEREO_DONTCARE</term>
            ///				<description>The requested pixel format can be either monoscopic or stereoscopic.</description>
            ///			</item>
            ///		</list>
            ///		<para>
            ///			With the <b>glAddSwapHintRectWIN</b> extension function, two new flags are included for the
            ///			<b>PIXELFORMATDESCRIPTOR</b> pixel format structure.
            ///		</para>
            ///		<list type="table">
            ///			<listheader>
            ///				<term>Value</term>
            ///				<description>Meaning</description>
            ///			</listheader>
            ///			<item>
            ///				<term>PFD_SWAP_COPY</term>
            ///				<description>
            ///					Specifies the content of the back buffer in the double-buffered main color plane following
            ///					a buffer swap. Swapping the color buffers causes the content of the back buffer to be copied
            ///					to the front buffer. The content of the back buffer is not affected by the swap. PFD_SWAP_COPY
            ///					is a hint only and might not be provided by a driver.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_SWAP_EXCHANGE</term>
            ///				<description>
            ///					Specifies the content of the back buffer in the double-buffered main color plane following a
            ///					buffer swap. Swapping the color buffers causes the exchange of the back buffer's content
            ///					with the front buffer's content. Following the swap, the back buffer's content contains the
            ///					front buffer's content before the swap. PFD_SWAP_EXCHANGE is a hint only and might not be
            ///					provided by a driver.
            ///				</description>
            ///			</item>
            ///		</list>
            /// </remarks>
            public Int32 dwFlags;

            /// <summary>
            /// Specifies the type of pixel data. The following types are defined.
            /// </summary>
            /// <remarks>
            ///		<list type="table">
            ///			<listheader>
            ///				<term>Value</term>
            ///				<description>Meaning</description>
            ///			</listheader>
            ///			<item>
            ///				<term>PFD_TYPE_RGBA</term>
            ///				<description>
            ///					RGBA pixels. Each pixel has four components in this order: red, green, blue, and alpha.
            ///				</description>
            ///			</item>
            ///			<item>
            ///				<term>PFD_TYPE_COLORINDEX</term>
            ///				<description>Color-index pixels. Each pixel uses a color-index value.</description>
            ///			</item>
            ///		</list>
            /// </remarks>
            public Byte iPixelType;

            /// <summary>
            /// Specifies the number of color bitplanes in each color buffer. For RGBA pixel types, it is the size
            /// of the color buffer, excluding the alpha bitplanes. For color-index pixels, it is the size of the
            /// color-index buffer.
            /// </summary>
            public Byte cColorBits;

            /// <summary>
            /// Specifies the number of red bitplanes in each RGBA color buffer.
            /// </summary>
            public Byte cRedBits;

            /// <summary>
            /// Specifies the shift count for red bitplanes in each RGBA color buffer.
            /// </summary>
            public Byte cRedShift;

            /// <summary>
            /// Specifies the number of green bitplanes in each RGBA color buffer.
            /// </summary>
            public Byte cGreenBits;

            /// <summary>
            /// Specifies the shift count for green bitplanes in each RGBA color buffer.
            /// </summary>
            public Byte cGreenShift;

            /// <summary>
            /// Specifies the number of blue bitplanes in each RGBA color buffer.
            /// </summary>
            public Byte cBlueBits;

            /// <summary>
            /// Specifies the shift count for blue bitplanes in each RGBA color buffer.
            /// </summary>
            public Byte cBlueShift;

            /// <summary>
            /// Specifies the number of alpha bitplanes in each RGBA color buffer. Alpha bitplanes are not supported.
            /// </summary>
            public Byte cAlphaBits;

            /// <summary>
            /// Specifies the shift count for alpha bitplanes in each RGBA color buffer. Alpha bitplanes are not supported.
            /// </summary>
            public Byte cAlphaShift;

            /// <summary>
            /// Specifies the total number of bitplanes in the accumulation buffer.
            /// </summary>
            public Byte cAccumBits;

            /// <summary>
            /// Specifies the number of red bitplanes in the accumulation buffer.
            /// </summary>
            public Byte cAccumRedBits;

            /// <summary>
            /// Specifies the number of green bitplanes in the accumulation buffer.
            /// </summary>
            public Byte cAccumGreenBits;

            /// <summary>
            /// Specifies the number of blue bitplanes in the accumulation buffer.
            /// </summary>
            public Byte cAccumBlueBits;

            /// <summary>
            /// Specifies the number of alpha bitplanes in the accumulation buffer.
            /// </summary>
            public Byte cAccumAlphaBits;

            /// <summary>
            /// Specifies the depth of the depth (z-axis) buffer.
            /// </summary>
            public Byte cDepthBits;

            /// <summary>
            /// Specifies the depth of the stencil buffer.
            /// </summary>
            public Byte cStencilBits;

            /// <summary>
            /// Specifies the number of auxiliary buffers. Auxiliary buffers are not supported.
            /// </summary>
            public Byte cAuxBuffers;

            /// <summary>
            /// Ignored. Earlier implementations of OpenGL used this member, but it is no longer used.
            /// </summary>
            /// <remarks>Specifies the type of layer.</remarks>
            public Byte iLayerType;

            /// <summary>
            /// Specifies the number of overlay and underlay planes. Bits 0 through 3 specify up to 15 overlay planes and
            /// bits 4 through 7 specify up to 15 underlay planes.
            /// </summary>
            public Byte bReserved;

            /// <summary>
            /// Ignored. Earlier implementations of OpenGL used this member, but it is no longer used.
            /// </summary>
            /// <remarks>
            ///		Specifies the layer mask. The layer mask is used in conjunction with the visible mask to determine
            ///		if one layer overlays another.
            /// </remarks>
            public Int32 dwLayerMask;

            /// <summary>
            /// Specifies the transparent color or index of an underlay plane. When the pixel type is RGBA, <b>dwVisibleMask</b>
            /// is a transparent RGB color value. When the pixel type is color index, it is a transparent index value.
            /// </summary>
            public Int32 dwVisibleMask;

            /// <summary>
            /// Ignored. Earlier implementations of OpenGL used this member, but it is no longer used.
            /// </summary>
            /// <remarks>
            ///		Specifies whether more than one pixel format shares the same frame buffer. If the result of the bitwise
            ///		AND of the damage masks between two pixel formats is nonzero, then they share the same buffers.
            /// </remarks>
            public Int32 dwDamageMask;
        };
        [StructLayout(LayoutKind.Sequential)]
        public struct POINTFLOAT
        {
            /// <summary>
            /// Specifies the horizontal (x) coordinate of a point.
            /// </summary>
            public float X;

            /// <summary>
            /// Specifies the vertical (y) coordinate of a point.
            /// </summary>
            public float Y;
        };
        [StructLayout(LayoutKind.Sequential)]
        public struct GLYPHMETRICSFLOAT
        {
            /// <summary>
            /// Specifies the width of the smallest rectangle (the glyph's black box) that completely encloses the glyph.
            /// </summary>
            public float gmfBlackBoxX;

            /// <summary>
            /// Specifies the height of the smallest rectangle (the glyph's black box) that completely encloses the glyph.
            /// </summary>
            public float gmfBlackBoxY;

            /// <summary>
            /// Specifies the x and y coordinates of the upper-left corner of the smallest rectangle that completely encloses the glyph.
            /// </summary>
            public POINTFLOAT gmfptGlyphOrigin;

            /// <summary>
            /// Specifies the horizontal distance from the origin of the current character cell to the origin of the next character cell.
            /// </summary>
            public float gmfCellIncX;

            /// <summary>
            /// Specifies the vertical distance from the origin of the current character cell to the origin of the next character cell.
            /// </summary>
            public float gmfCellIncY;
        };
        [DllImport(GDI_NATIVE_LIBRARY, EntryPoint = "SetPixelFormat", SetLastError = true), CLSCompliant(false), SuppressUnmanagedCodeSecurity]
        public static extern bool _SetPixelFormat(IntPtr deviceContext, int pixelFormat, ref PIXELFORMATDESCRIPTOR pixelFormatDescriptor);
        public static bool SetPixelFormat(IntPtr deviceContext, int pixelFormat, ref PIXELFORMATDESCRIPTOR pixelFormatDescriptor)
        {
            Kernel.LoadLibrary("opengl32.dll");
            return _SetPixelFormat(deviceContext, pixelFormat, ref pixelFormatDescriptor);
        }
        [DllImport(GDI_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern int ChoosePixelFormat(IntPtr deviceContext, ref PIXELFORMATDESCRIPTOR pixelFormatDescriptor);
        [DllImport(GDI_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool DeleteObject(IntPtr objectHandle);
        [DllImport(GDI_NATIVE_LIBRARY), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr objectHandle);
        [DllImport(GDI_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr CreateFont(int height, int width, int escapement, int orientation, int weight, bool italic, bool underline, bool strikeOut, int charSet, int outputPrecision, int clipPrecision, int quality, int pitchAndFamily, string typeFace);
        [DllImport(GDI_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, EntryPoint = "SwapBuffers"), SuppressUnmanagedCodeSecurity]
        public static extern int SwapBuffersFast([In] IntPtr deviceContext);
        [DllImport(GDI_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern int GetPixelFormat(IntPtr deviceContext);
        [DllImport(GDI_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [CLSCompliant(false)]
        public struct ABC
        {
            public int abcA;
            public uint abcB;
            public int abcC;
        }
        [DllImport(GDI_NATIVE_LIBRARY, SetLastError = true, EntryPoint = "GetCharABCWidthsW"), SuppressUnmanagedCodeSecurity, CLSCompliant(false)]
        public static extern bool GetCharABCWidths(IntPtr hdc, uint uFirstChar, uint uLastChar, [Out] ABC[] lpabc);
        [DllImport(GDI_NATIVE_LIBRARY, SetLastError = true, EntryPoint = "GetCharWidth32W"), SuppressUnmanagedCodeSecurity, CLSCompliant(false)]
        public static extern bool GetCharWidth32(IntPtr hdc, uint uFirstChar, uint uLastChar, [Out] int[] lpwidth);
        [DllImport(GDI_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool DeleteDC(IntPtr hdc);
        public struct KERNINGPAIR
        {
            public short wFirst;
            public short wSecond;
            public int iKernAmount;
        }
        [DllImport("gdi32", EntryPoint = "GetKerningPairsW")]
        public static extern int GetKerningPairs(IntPtr hDC, int cPairs, [Out] KERNINGPAIR[] lpkrnpair);
    }
}
