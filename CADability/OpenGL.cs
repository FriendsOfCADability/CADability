using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace CADability
{

    /* Gebrauchsanweisung
     * Die folgenden Klassen sind fast nur weiterleitungen an Windows API
     * bzw. an OpenGL. Sie sind entnommen aus verschiedenen Dateien unter
     * C:\Programme\Tao\source\src\Tao.OpenGl bzw. C:\Programme\Tao\source\src\Tao.Platform.Windows
     * Wenn etwas fehlt, einfach von dort kopieren
     */

    public class Kernel
    {
        // --- Fields ---
        #region Private Constants
        #region string KERNEL_NATIVE_LIBRARY
        /// <summary>
        ///     Specifies Kernel32's native library archive.
        /// </summary>
        /// <remarks>
        ///     Specifies kernel32.dll for Windows.
        /// </remarks>
        private const string KERNEL_NATIVE_LIBRARY = "kernel32.dll";
        #endregion string KERNEL_NATIVE_LIBRARY

        #region CallingConvention CALLING_CONVENTION
        /// <summary>
        ///     Specifies the calling convention.
        /// </summary>
        /// <remarks>
        ///     Specifies <see cref="CallingConvention.StdCall" />.
        /// </remarks>
        private const CallingConvention CALLING_CONVENTION = CallingConvention.StdCall;
        #endregion CallingConvention CALLING_CONVENTION
        #endregion Private Constants

        #region Public Structs
        #region MEMORYSTATUS
        /// <summary>
        ///     <para>
        ///         The <b>MEMORYSTATUS</b> structure contains information about the current state
        ///         of both physical and virtual memory.
        ///     </para>
        ///     <para>
        ///         The <see cref="GlobalMemoryStatus" /> function stores information in a
        ///         <b>MEMORYSTATUS</b> structure.
        ///     </para>
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <b>MEMORYSTATUS</b> reflects the state of memory at the time of the call.  It
        ///         reflects the size of the paging file at that time.  The operating system can
        ///         enlarge the paging file up to the maximum size set by the administrator.
        ///     </para>
        ///     <para>
        ///         On computers with more than 4 GB of memory, the <b>MEMORYSTATUS</b> structure
        ///         can return incorrect information.  Windows reports a value of -1 to indicate
        ///         an overflow, while Windows NT reports a value that is the real amount of
        ///         memory, modulo 4 GB.  If your application is at risk for this behavior, use
        ///         the <b>GlobalMemoryStatusEx</b> function instead of the
        ///         <see cref="GlobalMemoryStatus" /> function.
        ///     </para>
        /// </remarks>
        /// <seealso cref="GlobalMemoryStatus" />
        // <seealso cref="GlobalMemoryStatusEx" />
        // typedef struct _MEMORYSTATUS {
        //     DWORD dwLength;
        //     DWORD dwMemoryLoad;
        //     SIZE_T dwTotalPhys;
        //     SIZE_T dwAvailPhys;
        //     SIZE_T dwTotalPageFile;
        //     SIZE_T dwAvailPageFile;
        //     SIZE_T dwTotalVirtual;
        //     SIZE_T dwAvailVirtual;
        // } MEMORYSTATUS, *LPMEMORYSTATUS;
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUS
        {
            #region int Length
            /// <summary>
            ///     Size of the <b>MEMORYSTATUS</b> data structure, in bytes.  You do not need to
            ///     set this member before calling the <see cref="GlobalMemoryStatus" /> function;
            ///     the function sets it.
            /// </summary>
            public int Length;
            #endregion int Length

            #region int MemoryLoad
            /// <summary>
            ///     <para>
            ///         Approximate percentage of total physical memory that is in use.
            ///     </para>
            ///     <para>
            ///         <b>Windows NT:</b>  Percentage of approximately the last 1000 pages of
            ///         physical memory that is in use.
            ///     </para>
            /// </summary>
            public int MemoryLoad;
            #endregion int MemoryLoad

            #region int TotalPhys
            /// <summary>
            ///     Total size of physical memory, in bytes.
            /// </summary>
            public int TotalPhys;
            #endregion int TotalPhys

            #region int AvailPhys
            /// <summary>
            ///     Size of physical memory available, in bytes.
            /// </summary>
            public int AvailPhys;
            #endregion int AvailPhys

            #region int TotalPageFile
            /// <summary>
            ///     Size of the committed memory limit, in bytes.
            /// </summary>
            public int TotalPageFile;
            #endregion int TotalPageFile

            #region int AvailPageFile
            /// <summary>
            ///     Size of available memory to commit, in bytes.
            /// </summary>
            public int AvailPageFile;
            #endregion int AvailPageFile

            #region int TotalVirtual
            /// <summary>
            ///     Total size of the user mode portion of the virtual address space of the
            ///     calling process, in bytes.
            /// </summary>
            public int TotalVirtual;
            #endregion int TotalVirtual

            #region int AvailVirtual
            /// <summary>
            ///     Size of unreserved and uncommitted memory in the user mode portion of the
            ///     virtual address space of the calling process, in bytes.
            /// </summary>
            public int AvailVirtual;
            #endregion int AvailVirtual
        }
        #endregion MEMORYSTATUS

        #region SYSTEM_INFO
        /// <summary>
        ///     The <b>SYSTEM_INFO</b> structure contains information about the current computer
        ///     system.  This includes the architecture and type of the processor, the number of
        ///     processors in the system, the page size, and other such information.
        /// </summary>
        /// <seealso cref="GetSystemInfo" />
        /// <seealso cref="SYSTEM_INFO_UNION" />
        // <seealso cref="MapViewOfFile" />
        // <seealso cref="MapViewOfFileEx" />
        // typedef struct _SYSTEM_INFO {
        //     union {
        //         DWORD dwOemId;          // Obsolete field...do not use
        //         struct {
        //             WORD wProcessorArchitecture;
        //             WORD wReserved;
        //         };
        //     };
        //     DWORD dwPageSize;
        //     LPVOID lpMinimumApplicationAddress;
        //     LPVOID lpMaximumApplicationAddress;
        //     DWORD_PTR dwActiveProcessorMask;
        //     DWORD dwNumberOfProcessors;
        //     DWORD dwProcessorType;
        //     DWORD dwAllocationGranularity;
        //     WORD wProcessorLevel;
        //     WORD wProcessorRevision;
        // } SYSTEM_INFO, *LPSYSTEM_INFO;
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_INFO
        {
            #region SYSTEM_INFO_UNION SystemInfoUnion
            /// <summary>
            ///     Union for the OemId, ProcessorArchitecture, and Reserved fields of the
            ///     SYSTEM_INFO structure.  See <see cref="SYSTEM_INFO_UNION" />.
            /// </summary>
            public SYSTEM_INFO_UNION SystemInfoUnion;
            #endregion SYSTEM_INFO_UNION SystemInfoUnion

            #region int PageSize
            /// <summary>
            ///     Page size and the granularity of page protection and commitment.  This is the
            ///     page size used by the <b>VirtualAlloc</b> function.
            /// </summary>
            public int PageSize;
            #endregion int PageSize

            #region IntPtr MinimumApplicationAddress
            /// <summary>
            ///     Pointer to the lowest memory address accessible to applications and
            ///     dynamic-link libraries (DLLs).
            /// </summary>
            public IntPtr MinimumApplicationAddress;
            #endregion IntPtr MinimumApplicationAddress

            #region IntPtr MaximumApplicationAddress
            /// <summary>
            ///     Pointer to the highest memory address accessible to applications and DLLs.
            /// </summary>
            public IntPtr MaximumApplicationAddress;
            #endregion IntPtr MaximumApplicationAddress

            #region int ActiveProcessorMask
            /// <summary>
            ///     Mask representing the set of processors configured into the system.  Bit 0 is
            ///     processor 0; bit 31 is processor 31.
            /// </summary>
            public int ActiveProcessorMask;
            #endregion int ActiveProcessorMask

            #region int NumberOfProcessors
            /// <summary>
            ///     Number of processors in the system.
            /// </summary>
            public int NumberOfProcessors;
            #endregion int NumberOfProcessors

            #region int ProcessorType
            /// <summary>
            ///     <para>
            ///         An obsolete member that is retained for compatibility with Windows NT 3.5
            ///         and earlier.  Use the <i>SystemInfoUnion.ProcessorArchitecture</i>,
            ///         <i>ProcessorLevel</i>, and <i>ProcessorRevision</i> members to determine
            ///         the type of processor.
            ///     </para>
            ///     <para>
            ///         <b>Windows Me/98/95:</b>  Specifies the type of processor in the system.
            ///         This member is one of the following values:
            ///     </para>
            ///     <para>
            ///         <see cref="WinNt.PROCESSOR_INTEL_386" />
            ///     </para>
            ///     <para>
            ///         <see cref="WinNt.PROCESSOR_INTEL_486" />
            ///     </para>
            ///     <para>
            ///         <see cref="WinNt.PROCESSOR_INTEL_PENTIUM" />
            ///     </para>
            /// </summary>
            public int ProcessorType;
            #endregion int ProcessorType

            #region int AllocationGranularity
            /// <summary>
            ///     Granularity with which virtual memory is allocated.  For example, a
            ///     <b>VirtualAlloc</b> request to allocate 1 byte will reserve an address space
            ///     of <i>AllocationGranularity</i> bytes.  This value was hard coded as 64K in
            ///     the past, but other hardware architectures may require different values.
            /// </summary>
            public int AllocationGranularity;
            #endregion int AllocationGranularity

            #region int ProcessorLevel
            /// <summary>
            ///     <para>
            ///         System's architecture-dependent processor level.  It should be used only
            ///         for display purposes.  To determine the feature set of a processor, use
            ///         the <see cref="IsProcessorFeaturePresent" /> function.
            ///     </para>
            ///     <para>
            ///         If <i>SystemInfoUnion.ProcessorArchitecture</i> is
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_INTEL" />, <i>ProcessorLevel</i>
            ///         is defined by the CPU vendor.
            ///     </para>
            ///     <para>
            ///         If <i>SystemInfoUnion.ProcessorArchitecture</i> is
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_IA64" />, <i>ProcessorLevel</i> is
            ///         set to 1.
            ///     </para>
            ///     <para>
            ///         If <i>SystemInfoUnion.ProcessorArchitecture</i> is
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_MIPS" />, <i>ProcessorLevel</i> is
            ///         of the form 00xx, where xx is an 8-bit implementation number (bits 8-15 of
            ///         the PRId register).  The member can be the following value:
            ///     </para>
            ///     <para>
            ///         <list type="table">
            ///             <listheader>
            ///                 <term>Value</term>
            ///                 <description>Description</description>
            ///             </listheader>
            ///             <item>
            ///                 <term>0004</term>
            ///                 <description>MIPS R4000</description>
            ///             </item>
            ///         </list>
            ///     </para>
            ///     <para>
            ///         If <i>SystemInfoUnion.ProcessorArchitecture</i> is
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_ALPHA" />, <i>ProcessorLevel</i>
            ///         is of the form xxxx, where xxxx is a 16-bit processor version number (the
            ///         low-order 16 bits of a version number from the firmware).  The member can
            ///         be one of the following values:
            ///     </para>
            ///     <para>
            ///         <list type="table">
            ///             <listheader>
            ///                 <term>Value</term>
            ///                 <description>Description</description>
            ///             </listheader>
            ///             <item>
            ///                 <term>21064</term>
            ///                 <description>Alpha 21064</description>
            ///             </item>
            ///             <item>
            ///                 <term>21066</term>
            ///                 <description>Alpha 21066</description>
            ///             </item>
            ///             <item>
            ///                 <term>21164</term>
            ///                 <description>Alpha 21164</description>
            ///             </item>
            ///         </list>
            ///     </para>
            ///     <para>
            ///         If <i>SystemInfoUnion.ProcessorArchitecture</i> is
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_PPC" />, <i>ProcessorLevel</i> is
            ///         of the form xxxx, where xxxx is a 16-bit processor version number (the
            ///         high-order 16 bits of the Processor Version Register).  The member can be
            ///         one of the following values:
            ///     </para>
            ///     <para>
            ///         <list type="table">
            ///             <listheader>
            ///                 <term>Value</term>
            ///                 <description>Description</description>
            ///             </listheader>
            ///             <item>
            ///                 <term>1</term>
            ///                 <description>PPC 601</description>
            ///             </item>
            ///             <item>
            ///                 <term>3</term>
            ///                 <description>PPC 603</description>
            ///             </item>
            ///             <item>
            ///                 <term>4</term>
            ///                 <description>PPC 604</description>
            ///             </item>
            ///             <item>
            ///                 <term>6</term>
            ///                 <description>PPC 603+</description>
            ///             </item>
            ///             <item>
            ///                 <term>9</term>
            ///                 <description>PPC 604+</description>
            ///             </item>
            ///             <item>
            ///                 <term>20</term>
            ///                 <description>PPC 620</description>
            ///             </item>
            ///         </list>
            ///     </para>
            /// </summary>
            public int ProcessorLevel;
            #endregion int ProcessorLevel

            #region int ProcessorRevision
            /// <summary>
            ///     <para>
            ///         Architecture-dependent processor revision.  The following table shows how
            ///         the revision value is assembled for each type of processor architecture:
            ///     </para>
            ///     <para>
            ///         <list type="table">
            ///             <listheader>
            ///                 <term>Processor</term>
            ///                 <description>Description</description>
            ///             </listheader>
            ///             <item>
            ///                 <term>Intel 80386 or 80486</term>
            ///                 <description>
            ///                     <para>
            ///                         A value of the form xxyz.
            ///                     </para>
            ///                     <para>
            ///                         If xx is equal to 0xFF, y - 0xA is the model number, and
            ///                         z is the stepping identifier.  For example, an Intel
            ///                         80486-D0 system returns 0xFFD0.
            ///                     </para>
            ///                     <para>
            ///                         If xx is not equal to 0xFF, xx + 'A' is the stepping
            ///                         letter and yz is the minor stepping.
            ///                     </para>
            ///                 </description>
            ///             </item>
            ///             <item>
            ///                 <term>Intel Pentium, Cyrix, or NextGen 586</term>
            ///                 <description>
            ///                     <para>
            ///                         A value of the form xxyy, where xx is the model number and
            ///                         yy is the stepping.  Display this value of 0x0201 as
            ///                         follows:
            ///                     </para>
            ///                     <para>
            ///                         Model xx, Stepping yy.
            ///                     </para>
            ///                 </description>
            ///             </item>
            ///             <item>
            ///                 <term>MIPS</term>
            ///                 <description>
            ///                     A value of the form 00xx, where xx is the 8-bit revision
            ///                     number of the processor (the low-order 8 bits of the
            ///                     PRId register).
            ///                 </description>
            ///             </item>
            ///             <item>
            ///                 <term>ALPHA</term>
            ///                 <description>
            ///                     <para>
            ///                         A value of the form xxyy, where xxyy is the low-order 16
            ///                         bits of the processor revision number from the firmware.
            ///                         Display this value as follows:
            ///                     </para>
            ///                     <para>
            ///                         Model A+xx, Pass yy.
            ///                     </para>
            ///                 </description>
            ///             </item>
            ///             <item>
            ///                 <term>PPC</term>
            ///                 <description>
            ///                     <para>
            ///                         A value of the form xxyy, where xxyy is the low-order 16
            ///                         bits of the processor version register.  Display this
            ///                         value as follows:
            ///                     </para>
            ///                     <para>
            ///                         xx.yy.
            ///                     </para>
            ///                 </description>
            ///             </item>
            ///         </list>
            ///     </para>
            /// </summary>
            public int ProcessorRevision;
            #endregion int ProcessorRevision
        }
        #endregion SYSTEM_INFO

        #region SYSTEM_INFO_UNION
        /// <summary>
        ///     Union for the OemId, ProcessorArchitecture, and Reserved fields of the
        ///     <see cref="SYSTEM_INFO" /> structure.
        /// </summary>
        /// <seealso cref="SYSTEM_INFO" />
        //     union {
        //         DWORD dwOemId;          // Obsolete field...do not use
        //         struct {
        //             WORD wProcessorArchitecture;
        //             WORD wReserved;
        //         };
        //     };
        [StructLayout(LayoutKind.Explicit)]
        public struct SYSTEM_INFO_UNION
        {
            #region int OemId
            /// <summary>
            ///     <para>
            ///         An obsolete member that is retained for compatibility with Windows NT 3.5
            ///         and earlier.  New applications should use the <i>ProcessorArchitecture</i>
            ///         branch of the union.
            ///     </para>
            ///     <para>
            ///         <b>Windows Me/98/95:</b>  The system always sets this member to zero, the
            ///         value defined for <see cref="WinNt.PROCESSOR_ARCHITECTURE_INTEL" />.
            ///     </para>
            /// </summary>
            [FieldOffset(0)]
            public int OemId;
            #endregion int OemId

            #region short ProcessorArchitecture
            /// <summary>
            ///     <para>
            ///         System's processor architecture.  This value can be one of the following
            ///         values:
            ///     </para>
            ///     <para>
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_UNKNOWN" />
            ///     </para>
            ///     <para>
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_INTEL" />
            ///     </para>
            ///     <para>
            ///         <b>Windows NT 3.51:</b>  <see cref="WinNt.PROCESSOR_ARCHITECTURE_MIPS" />
            ///     </para>
            ///     <para>
            ///         <b>Windows NT 4.0 and earlier:</b>
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_ALPHA" />
            ///     </para>
            ///     <para>
            ///         <b>Windows NT 4.0 and earlier:</b>
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_PPC" />
            ///     </para>
            ///     <para>
            ///         <b>64-bit Windows:</b>  <see cref="WinNt.PROCESSOR_ARCHITECTURE_IA64" />,
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_IA32_ON_WIN64" />,
            ///         <see cref="WinNt.PROCESSOR_ARCHITECTURE_AMD64" />
            ///     </para>
            /// </summary>
            [FieldOffset(0)]
            public short ProcessorArchitecture;
            #endregion short ProcessorArchitecture

            #region short Reserved
            /// <summary>
            ///     Reserved for future use.
            /// </summary>
            [FieldOffset(2)]
            public short Reserved;
            #endregion short Reserved
        }
        #endregion SYSTEM_INFO_UNION
        #endregion Public Structs

        // --- Constructors & Destructors ---
        #region Kernel()
        /// <summary>
        ///     Prevents instantiation.
        /// </summary>
        private Kernel()
        {
        }
        #endregion Kernel()

        // --- Public Externs ---
        #region bool Beep(int frequency, int duration)
        /// <summary>
        ///     The <b>Beep</b> function generates simple tones on the speaker.  The function is
        ///     synchronous; it does not return control to its caller until the sound finishes.
        /// </summary>
        /// <param name="frequency">
        ///     <para>
        ///         Frequency of the sound, in hertz.  This parameter must be in the range
        ///         37 through 32,767 (0x25 through 0x7FFF).
        ///     </para>
        ///     <para>
        ///         <b>Windows 95/98/Me:</b>  The <b>Beep</b> function ignores this parameter.
        ///     </para>
        /// </param>
        /// <param name="duration">
        ///     <para>
        ///         Duration of the sound, in milliseconds.
        ///     </para>
        ///     <para>
        ///         <b>Windows 95/98/Me:</b>  The <b>Beep</b> function ignores this parameter.
        ///     </para>
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is true.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is false.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         <b>Terminal Services:</b>  The beep is redirected to the client.
        ///     </para>
        ///     <para>
        ///         <b>Windows 95/98/Me:</b>  On computers with a sound card, the function
        ///         plays the default sound event.  On computers without a sound card, the
        ///         function plays the standard system beep.
        ///     </para>
        /// </remarks>
        // <seealso cref="User.MessageBeep" />
        // WINBASEAPI BOOL WINAPI Beep(IN DWORD dwFreq, IN DWORD dwDuration);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool Beep(int frequency, int duration);
        #endregion bool Beep(int frequency, int duration)

        #region bool FreeLibrary(IntPtr moduleHandle)
        /// <summary>
        ///     The <b>FreeLibrary</b> function decrements the reference count of the loaded
        ///     dynamic-link library (DLL).  When the reference count reaches zero, the module
        ///     is unmapped from the address space of the calling process and the handle is no
        ///     longer valid.
        /// </summary>
        /// <param name="moduleHandle">
        ///     Handle to the loaded DLL module.  The <see cref="LoadLibrary" /> or
        ///     <see cref="GetModuleHandle" /> function returns this handle.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is true.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is false.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         Each process maintains a reference count for each loaded library module.  This
        ///         reference count is incremented each time <see cref="LoadLibrary" /> is called
        ///         and is decremented each time <b>FreeLibrary</b> is called.  A DLL module
        ///         loaded at process initialization due to load-time dynamic linking has a
        ///         reference count of one.  This count is incremented if the same module is
        ///         loaded by a call to <see cref="LoadLibrary" />.
        ///     </para>
        ///     <para>
        ///         Before unmapping a library module, the system enables the DLL to detach from
        ///         the process by calling the DLL's <b>DllMain</b> function, if it has one, with
        ///         the DLL_PROCESS_DETACH value.  Doing so gives the DLL an opportunity to clean
        ///         up resources allocated on behalf of the current process.  After the
        ///         entry-point function returns, the library module is removed from the address
        ///         space of the current process.
        ///     </para>
        ///     <para>
        ///         It is not safe to call <b>FreeLibrary</b> from <b>DllMain</b>.  For more
        ///         information, see the Remarks section in <b>DllMain</b>.
        ///     </para>
        ///     <para>
        ///         Calling <b>FreeLibrary</b> does not affect other processes using the same
        ///         library module.
        ///     </para>
        /// </remarks>
        /// <seealso cref="GetModuleHandle" />
        /// <seealso cref="LoadLibrary" />
        // <seealso cref="DllMain" />
        // <seealso cref="FreeLibraryAndExitThread" />
        // WINBASEAPI BOOL WINAPI FreeLibrary(IN OUT HMODULE hLibModule);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool FreeLibrary(IntPtr moduleHandle);
        #endregion bool FreeLibrary(IntPtr moduleHandle)

        #region int GetDllDirectory(int bufferLength, [Out] StringBuilder buffer)
        /// <summary>
        ///     The <b>GetDllDirectory</b> function retrieves the application-specific portion of
        ///     the search path used to locate DLLs for the application.
        /// </summary>
        /// <param name="bufferLength">
        ///     Size of the output buffer, in characters.
        /// </param>
        /// <param name="buffer">
        ///     Pointer to a buffer that receives the application-specific portion of the search path.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is the length of the string copied
        ///         to <i>buffer</i>, in characters, not including the terminating null character.
        ///         If the return value is greater than <i>bufferLength</i>, it specifies the size
        ///         of the buffer required for the path.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is zero.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <seealso cref="SetDllDirectory" />
        // WINBASEAPI DWORD WINAPI GetDllDirectoryA(IN DWORD nBufferLength, OUT LPSTR lpBuffer);
        // WINBASEAPI DWORD WINAPI GetDllDirectoryW(IN DWORD nBufferLength, OUT LPWSTR lpBuffer);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern int GetDllDirectory(int bufferLength, [Out] StringBuilder buffer);
        #endregion int GetDllDirectory(int bufferLength, [Out] StringBuilder buffer)

        #region int GetModuleFileName(IntPtr module, [Out] StringBuilder fileName, int size)
        /// <summary>
        ///     <para>
        ///         The <b>GetModuleFileName</b> function retrieves the fully qualified path for
        ///         the specified module.
        ///     </para>
        ///     <para>
        ///         To specify the process that contains the module, use the
        ///         <b>GetModuleFileNameEx</b> function.
        ///     </para>
        /// </summary>
        /// <param name="module">
        ///     Handle to the module whose path is being requested.  If this parameter is NULL,
        ///     <b>GetModuleFileName</b> retrieves the path for the current module.
        /// </param>
        /// <param name="fileName">
        ///     <para>
        ///         Pointer to a buffer that receives a null-terminated string that specifies the
        ///         fully-qualified path of the module.  If the length of the path exceeds the
        ///         size specified by the <i>size</i> parameter, the function succeeds and the
        ///         string is truncated to <i>size</i> characters and null terminated.
        ///     </para>
        ///     <para>
        ///         The path can have the prefix "\\?\", depending on how the module was loaded.
        ///     </para>
        /// </param>
        /// <param name="size">
        ///     Size of the <i>filename</i> buffer, in TCHARs.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is the length of the string copied
        ///         to the buffer, in TCHARs.  If the buffer is too small to hold the module name,
        ///         the string is truncated to <i>size</i>, and the function returns <i>size</i>.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is zero.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         If a DLL is loaded in two processes, its file name in one process may differ
        ///         in case from its file name in the other process.
        ///     </para>
        ///     <para>
        ///         For the ANSI version of the function, the number of TCHARs is the number of
        ///         bytes; for the Unicode version, it is the number of characters.
        ///     </para>
        ///     <para>
        ///         <b>Windows Me/98/95:</b>  This function retrieves long file names when an
        ///         application's version number is greater than or equal to 4.00 and the long
        ///         file name is available.  Otherwise, it returns only 8.3 format file names.
        ///     </para>
        /// </remarks>
        /// <seealso cref="GetModuleHandle" />
        /// <seealso cref="LoadLibrary" />
        // <seealso cref="GetModuleFileNameEx" />
        // WINBASEAPI DWORD WINAPI GetModuleFileNameA(IN HMODULE hModule, OUT LPSTR lpFilename, IN DWORD nSize);
        // WINBASEAPI DWORD WINAPI GetModuleFileNameW(IN HMODULE hModule, OUT LPWSTR lpFilename, IN DWORD nSize);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern int GetModuleFileName(IntPtr module, [Out] StringBuilder fileName, int size);
        #endregion int GetModuleFileName(IntPtr module, [Out] StringBuilder fileName, int size)

        #region IntPtr GetModuleHandle(string moduleName)
        /// <summary>
        ///     <para>
        ///         The <b>GetModuleHandle</b> function retrieves a module handle for the
        ///         specified module if the file has been mapped into the address space of the
        ///         calling process.
        ///     </para>
        ///     <para>
        ///         To avoid the race conditions described in the Remarks section, use the
        ///         <b>GetModuleHandleEx</b> function.
        ///     </para>
        /// </summary>
        /// <param name="moduleName">
        ///     <para>
        ///         Pointer to a null-terminated string that contains the name of the module
        ///         (either a .dll or .exe file).  If the file name extension is omitted, the
        ///         default library extension .dll is appended.  The file name string can include
        ///         a trailing point character (.) to indicate that the module name has no
        ///         extension.  The string does not have to specify a path.  When specifying a
        ///         path, be sure to use backslashes (\), not forward slashes (/).  The name is
        ///         compared (case independently) to the names of modules currently mapped into
        ///         the address space of the calling process.
        ///     </para>
        ///     <para>
        ///         If this parameter is NULL, <b>GetModuleHandle</b> returns a handle to the
        ///         file used to create the calling process.
        ///     </para>
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is a handle to the specified module
        ///         (IntPtr).
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is NULL (IntPtr.Zero).  To get
        ///         extended error information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The returned handle is not global or inheritable.  It cannot be duplicated
        ///         or used by another process.
        ///     </para>
        ///     <para>
        ///         The <b>GetModuleHandle</b> function returns a handle to a mapped module
        ///         without incrementing its reference count.  Therefore, use care when passing
        ///         the handle to the <see cref="FreeLibrary" /> function, because doing so can
        ///         cause a DLL module to be unmapped prematurely.
        ///     </para>
        ///     <para>
        ///         This function must be used carefully in a multithreaded application.  There
        ///         is no guarantee that the module handle remains valid between the time this
        ///         function returns the handle and the time it is used.  For example, a thread
        ///         retrieves a module handle, but before it uses the handle, a second thread
        ///         frees the module.  If the system loads another module, it could reuse the
        ///         module handle that was recently freed.  Therefore, first thread would have
        ///         a handle to a module different than the one intended.
        ///     </para>
        /// </remarks>
        /// <seealso cref="FreeLibrary" />
        /// <seealso cref="GetModuleFileName" />
        // <seealso cref="GetModuleHandleEx" />
        // WINBASEAPI HMODULE WINAPI GetModuleHandleA(IN LPCSTR lpModuleName);
        // WINBASEAPI HMODULE WINAPI GetModuleHandleW(IN LPCWSTR lpModuleName);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr GetModuleHandle(string moduleName);
        #endregion IntPtr GetModuleHandle(string moduleName)

        #region IntPtr GetProcAddress(IntPtr module, string processName)
        /// <summary>
        ///     The <b>GetProcAddress</b> function retrieves the address of an exported function
        ///     or variable from the specified dynamic-link library (DLL).
        /// </summary>
        /// <param name="module">
        ///     Handle to the DLL module that contains the function or variable.  The
        ///     <see cref="LoadLibrary" /> or <see cref="GetModuleHandle" /> function returns
        ///     this handle.
        /// </param>
        /// <param name="processName">
        ///     Pointer to a null-terminated string that specifies the function or variable name,
        ///     or the function's ordinal value.  If this parameter is an ordinal value, it must
        ///     be in the low-order word; the high-order word must be zero.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is the address of the exported
        ///         function or variable.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is NULL (IntPtr.Zero).  To get
        ///         extended error information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The spelling and case of a function name pointed to by <i>processName</i> must
        ///         be identical to that in the EXPORTS statement of the source DLL's
        ///         module-definition (.def) file.  The exported names of functions may differ
        ///         from the names you use when calling these functions in your code.  This
        ///         difference is hidden by macros used in the SDK header files.
        ///     </para>
        ///     <para>
        ///         The <i>processName</i> parameter can identify the DLL function by specifying
        ///         an ordinal value associated with the function in the EXPORTS statement.
        ///         <b>GetProcAddress</b> verifies that the specified ordinal is in the range 1
        ///         through the highest ordinal value exported in the .def file.  The function
        ///         then uses the ordinal as an index to read the function's address from a
        ///         function table.  If the .def file does not number the functions consecutively
        ///         from 1 to N (where N is the number of exported functions), an error can occur
        ///         where <b>GetProcAddress</b> returns an invalid, non-NULL address, even though
        ///         there is no function with the specified ordinal.
        ///     </para>
        ///     <para>
        ///         In cases where the function may not exist, the function should be specified by
        ///         name rather than by ordinal value.
        ///     </para>
        /// </remarks>
        /// <seealso cref="FreeLibrary" />
        /// <seealso cref="GetModuleHandle" />
        /// <seealso cref="LoadLibrary" />
        // WINBASEAPI FARPROC WINAPI GetProcAddress(IN HMODULE hModule, IN LPCSTR lpProcName);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Ansi, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr GetProcAddress(IntPtr module, string processName);
        #endregion IntPtr GetProcAddress(IntPtr module, string processName)

        #region bool GetProcessWorkingSetSize(IntPtr process, out int minimumWorkingSetSize, out int maximumWorkingSetSize)
        /// <summary>
        ///     The <b>GetProcessWorkingSetSize</b> function retrieves the minimum and maximum
        ///     working set sizes of the specified process.
        /// </summary>
        /// <param name="process">
        ///     Handle to the process whose working set sizes will be obtained.  The handle must
        ///     have the PROCESS_QUERY_INFORMATION access right.
        /// </param>
        /// <param name="minimumWorkingSetSize">
        ///     Pointer to a variable that receives the minimum working set size of the specified
        ///     process, in bytes.  The virtual memory manager attempts to keep at least this much
        ///     memory resident in the process whenever the process is active.
        /// </param>
        /// <param name="maximumWorkingSetSize">
        ///     Pointer to a variable that receives the maximum working set size of the specified
        ///     process, in bytes.  The virtual memory manager attempts to keep no more than this
        ///     much memory resident in the process whenever the process is active when memory is
        ///     in short supply.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is true.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is false.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     The "working set" of a process is the set of memory pages currently visible to
        ///     the process in physical RAM memory.  These pages are resident and available for
        ///     an application to use without triggering a page fault.  The minimum and maximum
        ///     working set sizes affect the virtual memory paging behavior of a process.
        /// </remarks>
        /// <seealso cref="SetProcessWorkingSetSize" />
        // WINBASEAPI BOOL WINAPI GetProcessWorkingSetSize(IN HANDLE hProcess, OUT PSIZE_T lpMinimumWorkingSetSize, OUT PSIZE_T lpMaximumWorkingSetSize);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool GetProcessWorkingSetSize(IntPtr process, out int minimumWorkingSetSize, out int maximumWorkingSetSize);
        #endregion bool GetProcessWorkingSetSize(IntPtr process, out int minimumWorkingSetSize, out int maximumWorkingSetSize)

        #region int GetSystemDirectory([Out] StringBuilder buffer, int size)
        /// <summary>
        ///     <para>
        ///         The <b>GetSystemDirectory</b> function retrieves the path of the system
        ///         directory.  The system directory contains system such files such as
        ///         dynamic-link libraries, drivers, and font files.
        ///     </para>
        ///     <para>
        ///         This function is provided primarily for compatibility.  Applications should
        ///         store code in the Program Files folder and persistent data in the Application
        ///         Data folder in the user's profile.
        ///     </para>
        /// </summary>
        /// <param name="buffer">
        ///     Pointer to the buffer to receive the null-terminated string containing the path.
        ///     This path does not end with a backslash unless the system directory is the root
        ///     directory.  For example, if the system directory is named Windows\System on drive
        ///     C, the path of the system directory retrieved by this function is
        ///     C:\Windows\System.
        /// </param>
        /// <param name="size">
        ///     Maximum size of the buffer, in TCHARs.  This value should be set to at least
        ///     MAX_PATH+1 to allow sufficient space for the path and the null terminator.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is the length, in TCHARs, of the
        ///         string copied to the buffer, not including the terminating null character.  If
        ///         the length is greater than the size of the buffer, the return value is the
        ///         size of the buffer required to hold the path.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is zero.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     Applications should not create files in the system directory.  If the user is
        ///     running a shared version of the operating system, the application does not have
        ///     write access to the system directory.
        /// </remarks>
        /// <seealso cref="GetWindowsDirectory" />
        // <seealso cref="GetCurrentDirectory" />
        // <seealso cref="SetCurrentDirectory" />
        // WINBASEAPI UINT WINAPI GetSystemDirectoryA(OUT LPSTR lpBuffer, IN UINT uSize);
        // WINBASEAPI UINT WINAPI GetSystemDirectoryW(OUT LPWSTR lpBuffer, IN UINT uSize);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern int GetSystemDirectory([Out] StringBuilder buffer, int size);
        #endregion int GetSystemDirectory([Out] StringBuilder buffer, int size)

        #region GetSystemInfo(out SYSTEM_INFO systemInfo)
        /// <summary>
        ///     <para>
        ///         The <b>GetSystemInfo</b> function returns information about the current
        ///         system.
        ///     </para>
        ///     <para>
        ///         To retrieve accurate information for a Win32-based application running on
        ///         WOW64, call the <b>GetNativeSystemInfo</b> function.
        ///     </para>
        /// </summary>
        /// <param name="systemInfo">
        ///     Pointer to a <see cref="SYSTEM_INFO" /> structure that receives the information.
        /// </param>
        /// <seealso cref="SYSTEM_INFO" />
        // <seealso cref="GetNativeSystemInfo" />
        // WINBASEAPI VOID WINAPI GetSystemInfo(OUT LPSYSTEM_INFO lpSystemInfo);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern void GetSystemInfo(out SYSTEM_INFO systemInfo);
        #endregion GetSystemInfo(out SYSTEM_INFO systemInfo)

        #region int GetSystemWindowsDirectory([Out] StringBuilder buffer, int size)
        /// <summary>
        ///     The <b>GetSystemWindowsDirectory</b> function retrieves the path of the shared
        ///     Windows directory on a multi-user system.
        /// </summary>
        /// <param name="buffer">
        ///     Pointer to the buffer to receive a null-terminated string containing the path.
        ///     This path does not end with a backslash unless the Windows directory is the root
        ///     directory.  For example, if the Windows directory is named Windows on drive C,
        ///     the path of the Windows directory retrieved by this function is C:\Windows.  If
        ///     the system was installed in the root directory of drive C, the path retrieved
        ///     is C:\.
        /// </param>
        /// <param name="size">
        ///     Maximum size of the buffer specified by the <i>buffer</i> parameter, in TCHARs.
        ///     This value should be set to at least MAX_PATH+1 to allow sufficient space for the
        ///     path and the null-terminating character.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is the length of the string copied
        ///         to the buffer, in TCHARs, not including the terminating null character.
        ///     </para>
        ///     <para>
        ///         If the length is greater than the size of the buffer, the return value is the
        ///         size of the buffer required to hold the path.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is zero.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         On a system that is running Terminal Server, each user has a unique Windows
        ///         directory.  The system Windows directory is shared by all users, so it is the
        ///         directory where an application should store initialization and help files that
        ///         apply to all users.
        ///     </para>
        ///     <para>
        ///         With Terminal Services, the <b>GetSystemWindowsDirectory</b> function
        ///         retrieves the path of the system Windows directory, while the
        ///         <see cref="GetWindowsDirectory" /> function retrieves the path of a Windows
        ///         directory that is private for each user.  On a single-user system,
        ///         <b>GetSystemWindowsDirectory</b> is the same as
        ///         <see cref="GetWindowsDirectory" />.
        ///     </para>
        ///     <para>
        ///         <b>Windows NT 4.0 Terminal Server Edition:</b>  To retrieve the shared
        ///         Windows directory, call <see cref="GetSystemDirectory" /> and trim the
        ///         "System32" element from the end of the returned path.
        ///     </para>
        /// </remarks>
        /// <seealso cref="GetWindowsDirectory" />
        // WINBASEAPI UINT WINAPI GetSystemWindowsDirectoryA(OUT LPSTR lpBuffer, IN UINT uSize);
        // WINBASEAPI UINT WINAPI GetSystemWindowsDirectoryW(OUT LPWSTR lpBuffer, IN UINT uSize);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern int GetSystemWindowsDirectory([Out] StringBuilder buffer, int size);
        #endregion int GetSystemWindowsDirectory([Out] StringBuilder buffer, int size)

        #region int GetTickCount()
        /// <summary>
        ///     The <b>GetTickCount</b> function retrieves the number of milliseconds that have
        ///     elapsed since the system was started.  It is limited to the resolution of the
        ///     system timer.  To obtain the system timer resolution, use the
        ///     <b>GetSystemTimeAdjustment</b> function.
        /// </summary>
        /// <returns>
        ///     The return value is the number of milliseconds that have elapsed since the system
        ///     was started.
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The elapsed time is stored as a DWORD value.  Therefore, the time will wrap
        ///         around to zero if the system is run continuously for 49.7 days.
        ///     </para>
        ///     <para>
        ///         If you need a higher resolution timer, use a multimedia timer or a
        ///         high-resolution timer.
        ///     </para>
        ///     <para>
        ///         To obtain the time elapsed since the computer was started, retrieve the System
        ///         Up Time counter in the performance data in the registry key
        ///         HKEY_PERFORMANCE_DATA.  The value returned is an 8-byte value.
        ///     </para>
        /// </remarks>
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern int GetTickCount();
        #endregion int GetTickCount()

        #region int GetWindowsDirectory([Out] StringBuilder buffer, int size)
        /// <summary>
        ///     <para>
        ///         The <b>GetWindowsDirectory</b> function retrieves the path of the Windows
        ///         directory.  The Windows directory contains such files as applications,
        ///         initialization files, and help files.
        ///     </para>
        ///     <para>
        ///         This function is provided primarily for compatibility.  Applications should
        ///         store code in the Program Files folder and persistent data in the Application
        ///         Data folder in the user's profile.
        ///     </para>
        /// </summary>
        /// <param name="buffer">
        ///     Pointer to the buffer to receive the null-terminated string containing the path.
        ///     This path does not end with a backslash unless the Windows directory is the root
        ///     directory.  For example, if the Windows directory is named Windows on drive C, the
        ///     path of the Windows directory retrieved by this function is C:\Windows.  If the
        ///     system was installed in the root directory of drive C, the path retrieved is C:\.
        /// </param>
        /// <param name="size">
        ///     Maximum size of the buffer specified by the <i>buffer</i> parameter, in TCHARs.
        ///     This value should be set to MAX_PATH.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is the length of the string copied
        ///         to the buffer, in TCHARs, not including the terminating null character.
        ///     </para>
        ///     <para>
        ///         If the length is greater than the size of the buffer, the return value is the
        ///         size of the buffer required to hold the path.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is zero.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The Windows directory is the directory where an application should store
        ///         initialization and help files. If the user is running a shared version of the
        ///         system, the Windows directory is guaranteed to be private for each user.
        ///     </para>
        ///     <para>
        ///         If an application creates other files that it wants to store on a per-user
        ///         basis, it should place them in the directory specified by the HOMEPATH
        ///         environment variable.  This directory will be different for each user, if so
        ///         specified by an administrator, through the User Manager administrative tool.
        ///         HOMEPATH always specifies either the user's home directory, which is
        ///         guaranteed to be private for each user, or a default directory (for example,
        ///         C:\USERS\DEFAULT) where the user will have all access.
        ///     </para>
        ///     <para>
        ///         <b>Terminal Services:</b>  If the application is running in a Terminal
        ///         Services environment, each user has a unique Windows directory.  If an
        ///         application that is not Terminal-Services-aware calls this function, it
        ///         retrieves the path of the Windows directory on the client, not the Windows
        ///         directory on the server.
        ///     </para>
        /// </remarks>
        /// <seealso cref="GetSystemDirectory" />
        /// <seealso cref="GetSystemWindowsDirectory" />
        // <seealso cref="GetCurrentDirectory" />
        // WINBASEAPI UINT WINAPI GetWindowsDirectoryA(OUT LPSTR lpBuffer, IN UINT uSize);
        // WINBASEAPI UINT WINAPI GetWindowsDirectoryW(OUT LPWSTR lpBuffer, IN UINT uSize);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern int GetWindowsDirectory([Out] StringBuilder buffer, int size);
        #endregion int GetWindowsDirectory([Out] StringBuilder buffer, int size)

        #region GlobalMemoryStatus(out MEMORYSTATUS buffer)
        /// <summary>
        ///     <para>
        ///         The <b>GlobalMemoryStatus</b> function obtains information about the system's
        ///         current usage of both physical and virtual memory.
        ///     </para>
        ///     <para>
        ///         To obtain information about the extended portion of the virtual address space,
        ///         or if your application may run on computers with more than 4 GB of main
        ///         memory, use the <b>GlobalMemoryStatusEx</b> function.
        ///     </para>
        /// </summary>
        /// <param name="buffer">
        ///     Pointer to a <see cref="MEMORYSTATUS" /> structure.  The <b>GlobalMemoryStatus</b>
        ///     function stores information about current memory availability into this structure.
        /// </param>
        /// <remarks>
        ///     <para>
        ///         You can use the <b>GlobalMemoryStatus</b> function to determine how much
        ///         memory your application can allocate without severely impacting other
        ///         applications.
        ///     </para>
        ///     <para>
        ///         The information returned by the <b>GlobalMemoryStatus</b> function is
        ///         volatile.  There is no guarantee that two sequential calls to this function
        ///         will return the same information.
        ///     </para>
        ///     <para>
        ///         On computers with more than 4 GB of memory, the <b>GlobalMemoryStatus</b>
        ///         function can return incorrect information.  Windows 2000 and later report a
        ///         value of -1 to indicate an overflow.  Earlier versions of Windows NT report a
        ///         value that is the real amount of memory, modulo 4 GB.  For this reason, use
        ///         the <b>GlobalMemoryStatusEx</b> function instead.
        ///     </para>
        ///     <para>
        ///         On Intel x86 computers with more than 2 GB and less than 4 GB of memory, the
        ///         <b>GlobalMemoryStatus</b> function will always return 2 GB in the
        ///         <see cref="MEMORYSTATUS.TotalPhys" /> member of the
        ///         <see cref="MEMORYSTATUS" /> structure.  Similarly, if the total available
        ///         memory is between 2 and 4 GB, the <see cref="MEMORYSTATUS.AvailPhys" /> member
        ///         of the <see cref="MEMORYSTATUS" /> structure will be rounded down to 2 GB.  If
        ///         the executable is linked using the /LARGEADDRESSWARE linker option, then the
        ///         <b>GlobalMemoryStatus</b> function will return the correct amount of physical
        ///         memory in both members.
        ///     </para>
        /// </remarks>
        /// <seealso cref="MEMORYSTATUS" />
        // <seealso cref="GlobalMemoryStatusEx" />
        // WINBASEAPI VOID WINAPI GlobalMemoryStatus(IN OUT LPMEMORYSTATUS lpBuffer);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern void GlobalMemoryStatus(out MEMORYSTATUS buffer);
        #endregion GlobalMemoryStatus(out MEMORYSTATUS buffer)

        #region bool IsProcessorFeaturePresent(int processorFeature)
        /// <summary>
        ///     The <b>IsProcessorFeaturePresent</b> function determines whether the specified
        ///     processor feature is supported by the current computer.
        /// </summary>
        /// <param name="processorFeature">
        ///     <para>
        ///         Processor feature to be tested.  This parameter can be one of the following
        ///         values:
        ///     </para>
        ///     <para>
        ///         <list type="table">
        ///             <listheader>
        ///                 <term>Value</term>
        ///                 <description>Description</description>
        ///             </listheader>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_3DNOW_INSTRUCTIONS_AVAILABLE" /></term>
        ///                 <description>
        ///                     The 3D-Now instruction set is available.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_COMPARE_EXCHANGE_DOUBLE" /></term>
        ///                 <description>
        ///                     The compare and exchange double operation is available (Pentium,
        ///                     MIPS, and Alpha).
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_FLOATING_POINT_EMULATED" /></term>
        ///                 <description>
        ///                     <para>
        ///                         Floating-point operations are emulated using a software
        ///                         emulator.
        ///                     </para>
        ///                     <para>
        ///                         This function returns true if floating-point operations are
        ///                         emulated; otherwise, it returns false.
        ///                     </para>
        ///                     <para>
        ///                         <b>Windows NT 4.0:</b>  This function returns false if
        ///                         floating-point operations are emulated; otherwise, it returns
        ///                         true.  This behavior is a bug that is fixed in later versions.
        ///                     </para>
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_FLOATING_POINT_PRECISION_ERRATA" /></term>
        ///                 <description>
        ///                     <b>Pentium:</b>  In rare circumstances, a floating-point precision
        ///                     error can occur.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_MMX_INSTRUCTIONS_AVAILABLE" /></term>
        ///                 <description>
        ///                     The MMX instruction set is available.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_PAE_ENABLED" /></term>
        ///                 <description>
        ///                     The processor is PAE-enabled.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_RDTSC_INSTRUCTION_AVAILABLE" /></term>
        ///                 <description>
        ///                     The RDTSC instruction is available.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_XMMI_INSTRUCTIONS_AVAILABLE" /></term>
        ///                 <description>
        ///                     The SSE instruction set is available.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><see cref="WinNt.PF_XMMI64_INSTRUCTIONS_AVAILABLE" /></term>
        ///                 <description>
        ///                     The SSE2 instruction set is available.
        ///                 </description>
        ///             </item>
        ///         </list>
        ///     </para>
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the feature is supported, the return value is true.
        ///     </para>
        ///     <para>
        ///         If the feature is not supported, the return value is false.
        ///     </para>
        /// </returns>
        // WINBASEAPI BOOL WINAPI IsProcessorFeaturePresent(IN DWORD ProcessorFeature);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern bool IsProcessorFeaturePresent(int processorFeature);
        #endregion bool IsProcessorFeaturePresent(int processorFeature)

        #region IntPtr LoadLibrary(string fileName)
        /// <summary>
        ///     The <b>LoadLibrary</b> function maps the specified executable module into the
        ///     address space of the calling process.
        /// </summary>
        /// <param name="fileName">
        ///     <para>
        ///         Pointer to a null-terminated string that names the executable module (either
        ///         a .dll or .exe file).  The name specified is the file name of the module and
        ///         is not related to the name stored in the library module itself, as specified
        ///         by the LIBRARY keyword in the module-definition (.def) file.
        ///     </para>
        ///     <para>
        ///         If the string specifies a path but the file does not exist in the specified
        ///         directory, the function fails.  When specifying a path, be sure to use
        ///         backslashes (\), not forward slashes (/).
        ///     </para>
        ///     <para>
        ///         If the string does not specify a path, the function uses a standard search
        ///         strategy to find the file.  See the Remarks for more information.
        ///     </para>
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is a handle to the module (IntPtr).
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is NULL (IntPtr.Zero).  To get
        ///         extended error information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        ///     <para>
        ///         <b>Windows Me/98/95:</b>  If you are using <b>LoadLibrary</b> to load a module
        ///         that contains a resource whose numeric identifier is greater than 0x7FFF,
        ///         <b>LoadLibrary</b> fails.  If you are attempting to load a 16-bit DLL directly
        ///         from 32-bit code, <b>LoadLibrary</b> fails.  If you are attempting to load a
        ///         DLL whose subsystem version is greater than 4.0, <b>LoadLibrary</b> fails.  If
        ///         your <b>DllMain</b> function tries to call the Unicode version of a function,
        ///         <b>LoadLibrary</b> fails.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         <b>LoadLibrary</b> can be used to map a DLL module and return a handle that
        ///         can be used in <see cref="GetProcAddress" /> to get the address of a DLL
        ///         function.  <b>LoadLibrary</b> can also be used to map other executable
        ///         modules.  For example, the function can specify an .exe file to get a
        ///         handle that can be used in <b>FindResource</b> or <b>LoadResource</b>.
        ///         However, do not use <b>LoadLibrary</b> to run an .exe file, use the
        ///         <b>CreateProcess</b> function.
        ///     </para>
        ///     <para>
        ///         If the module is a DLL not already mapped for the calling process, the system
        ///         calls the DLL's <b>DllMain</b> function with the DLL_PROCESS_ATTACH value.  If
        ///         the DLL's entry-point function does not return TRUE, <b>LoadLibrary</b> fails
        ///         and returns NULL.  (The system immediately calls your entry-point function
        ///         with DLL_PROCESS_DETACH and unloads the DLL.)
        ///     </para>
        ///     <para>
        ///         It is not safe to call <b>LoadLibrary</b> from <b>DllMain</b>.  For more
        ///         information, see the Remarks section in <b>DllMain</b>.
        ///     </para>
        ///     <para>
        ///         Module handles are not global or inheritable.  A call to <b>LoadLibrary</b> by
        ///         one process does not produce a handle that another process can use — for
        ///         example, in calling <see cref="GetProcAddress" />.  The other process must
        ///         make its own call to <b>LoadLibrary</b> for the module before calling
        ///         <see cref="GetProcAddress" />.
        ///     </para>
        ///     <para>
        ///         If no file name extension is specified in the <i>fileName</i> parameter, the
        ///         default library extension .dll is appended.  However, the file name string
        ///         can include a trailing point character (.) to indicate that the module name
        ///         has no extension.  When no path is specified, the function searches for loaded
        ///         modules whose base name matches the base name of the module to be loaded.  If
        ///         the name matches, the load succeeds.  Otherwise, the function searches for the
        ///         file in the following sequence:
        ///     </para>
        ///     <para>
        ///         <list type="number">
        ///             <item>
        ///                 <description>
        ///                     The directory from which the application loaded.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The current directory.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The system directory.  Use the <see cref="GetSystemDirectory" />
        ///                     function to get the path of this directory.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     <para>
        ///                         The 16-bit system directory.  There is no function that
        ///                         obtains the path of this directory, but it is searched.
        ///                     </para>
        ///                     <para>
        ///                         <b>Windows Me/98/95:</b>  This directory does not exist.
        ///                     </para>
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The Windows directory.  Use the <see cref="GetWindowsDirectory" />
        ///                     function to get the path of this directory.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The directories that are listed in the PATH environment variable.
        ///                 </description>
        ///             </item>
        ///         </list>
        ///     </para>
        ///     <para>
        ///         <b>Windows Server 2003, Windows XP SP1:</b>  The default value of
        ///         HKLM\System\CurrentControlSet\Control\Session Manager\SafeDllSearchMode is 1
        ///         (current directory is searched after the system and Windows directories).
        ///     </para>
        ///     <para>
        ///         <b>Windows XP:</b>  If
        ///         HKLM\System\CurrentControlSet\Control\Session Manager\SafeDllSearchMode is 1,
        ///         the current directory is searched after the system and Windows directories,
        ///         but before the directories in the PATH environment variable.  The default
        ///         value is 0 (current directory is searched before the system and Windows
        ///         directories).
        ///     </para>
        ///     <para>
        ///         The first directory searched is the one directory containing the image file
        ///         used to create the calling process (for more information, see the
        ///         <b>CreateProcess</b> function).  Doing this allows private dynamic-link
        ///         library (DLL) files associated with a process to be found without adding the
        ///         process's installed directory to the PATH environment variable.
        ///     </para>
        ///     <para>
        ///         The search path can be altered using the <see cref="SetDllDirectory" />
        ///         function.  This solution is recommended instead of using
        ///         <b>SetCurrentDirectory</b> or hard-coding the full path to the DLL.
        ///     </para>
        ///     <para>
        ///         If a path is specified and there is a redirection file for the application,
        ///         the function searches for the module in the application's directory.  If the
        ///         module exists in the application's directory, the <b>LoadLibrary</b> function
        ///         ignores the specified path and loads the module from the application's
        ///         directory.  If the module does not exist in the application's directory,
        ///         <b>LoadLibrary</b> loads the module from the specified directory.
        ///     </para>
        /// </remarks>
        /// <seealso cref="FreeLibrary" />
        /// <seealso cref="GetProcAddress" />
        /// <seealso cref="GetSystemDirectory" />
        /// <seealso cref="GetWindowsDirectory" />
        /// <seealso cref="SetDllDirectory" />
        // <seealso cref="DllMain" />
        // <seealso cref="FindResource" />
        // <seealso cref="LoadLibraryEx" />
        // <seealso cref="LoadResource" />
        // WINBASEAPI HMODULE WINAPI LoadLibraryA(IN LPCSTR lpLibFileName);
        // WINBASEAPI HMODULE WINAPI LoadLibraryW(IN LPCWSTR lpLibFileName);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr LoadLibrary(string fileName);
        #endregion IntPtr LoadLibrary(string fileName)

        #region bool QueryPerformanceCounter(out long performanceCount)
        /// <summary>
        ///     The <b>QueryPerformanceCounter</b> function retrieves the current value of the
        ///     high-resolution performance counter.
        /// </summary>
        /// <param name="performanceCount">
        ///     Pointer to a variable that receives the current performance-counter value, in
        ///     counts.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is true.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is false.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     On a multiprocessor machine, it should not matter which processor is called.
        ///     However, you can get different results on different processors due to bugs in the
        ///     BIOS or the HAL.  To specify processor affinity for a thread, use the
        ///     <b>SetThreadAffinityMask</b> function.
        /// </remarks>
        /// <seealso cref="QueryPerformanceCounterFast" />
        /// <seealso cref="QueryPerformanceFrequency" />
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool QueryPerformanceCounter(out long performanceCount);
        #endregion bool QueryPerformanceCounter(out long performanceCount)

        #region int QueryPerformanceCounterFast(out long performanceCount)
        /// <summary>
        ///     The <b>QueryPerformanceCounterFast</b> function retrieves the current value of the
        ///     high-resolution performance counter.
        /// </summary>
        /// <param name="performanceCount">
        ///     Pointer to a variable that receives the current performance-counter value, in
        ///     counts.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is true.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is false.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         This version of <see cref="QueryPerformanceCounter" /> is slightly faster.  It
        ///         does not set the last Windows error.  Use with care.
        ///     </para>
        ///     <para>
        ///         On a multiprocessor machine, it should not matter which processor is called.
        ///         However, you can get different results on different processors due to bugs in
        ///         the BIOS or the HAL.  To specify processor affinity for a thread, use the
        ///         <b>SetThreadAffinityMask</b> function.
        ///     </para>
        /// </remarks>
        /// <seealso cref="QueryPerformanceCounter" />
        /// <seealso cref="QueryPerformanceFrequency" />
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, EntryPoint = "QueryPerformanceCounter"), SuppressUnmanagedCodeSecurity]
        public static extern int QueryPerformanceCounterFast(out long performanceCount);
        #endregion int QueryPerformanceCounterFast(out long performanceCount)

        #region bool QueryPerformanceFrequency(out long frequency)
        /// <summary>
        ///     The <b>QueryPerformanceFrequency</b> function retrieves the frequency of the
        ///     high-resolution performance counter, if one exists.  The frequency cannot change
        ///     while the system is running.
        /// </summary>
        /// <param name="frequency">
        ///     Pointer to a variable that receives the current performance-counter frequency, in
        ///     counts per second.  If the installed hardware does not support a high-resolution
        ///     performance counter, this parameter can be zero.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the installed hardware supports a high-resolution performance counter, the
        ///         return value is true.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is false.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.  For example, if
        ///         the installed hardware does not support a high-resolution performance counter,
        ///         the function fails.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <b>Note</b>  The frequency of the high-resolution performance counter is not the
        ///     processor speed.
        /// </remarks>
        /// <seealso cref="QueryPerformanceCounter" />
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool QueryPerformanceFrequency(out long frequency);
        #endregion #region bool QueryPerformanceFrequency(out long frequency)

        #region bool SetDllDirectory(string pathName)
        /// <summary>
        ///     The <b>SetDllDirectory</b> function modifies the search path used to locate DLLs
        ///     for the application.
        /// </summary>
        /// <param name="pathName">
        ///     Pointer to a null-terminated string that specifies the directories to be added to
        ///     the search path, separated by semicolons.  If this parameter is NULL, the default
        ///     search path is used.
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is true.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is false.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The <b>SetDllDirectory</b> function affects all subsequent calls to the
        ///         <see cref="LoadLibrary" /> and <b>LoadLibraryEx</b> functions.  After calling
        ///         <b>SetDllDirectory</b>, the DLL search path is:
        ///     </para>
        ///     <para>
        ///         <list type="number">
        ///             <item>
        ///                 <description>
        ///                     The directory from which the application loaded.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The directory specified by the <i>pathName</i> parameter.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The system directory.  Use the <see cref="GetSystemDirectory" />
        ///                     function to get the path of this directory.  The name of this
        ///                     directory is System32.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The 16-bit system directory.  There is no function that obtains
        ///                     the path of this directory, but it is searched.  The name of this
        ///                     directory is System.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The Windows directory.  Use the <see cref="GetWindowsDirectory" />
        ///                     function to get the path of this directory.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <description>
        ///                     The directories that are listed in the PATH environment variable.
        ///                 </description>
        ///             </item>
        ///         </list>
        ///     </para>
        ///     <para>
        ///         To revert to the default search path used by <see cref="LoadLibrary" /> and
        ///         <b>LoadLibraryEx</b>, call <b>SetDllDirectory</b> with NULL.
        ///     </para>
        /// </remarks>
        /// <seealso cref="GetDllDirectory" />
        /// <seealso cref="GetSystemDirectory" />
        /// <seealso cref="GetWindowsDirectory" />
        /// <seealso cref="LoadLibrary" />
        // <seealso cref="LoadLibraryEx" />
        // WINBASEAPI BOOL WINAPI SetDllDirectoryA(IN LPCSTR lpPathName);
        // WINBASEAPI BOOL WINAPI SetDllDirectoryW(IN LPCWSTR lpPathName);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool SetDllDirectory(string pathName);
        #endregion bool SetDllDirectory(string pathName)

        #region bool SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int maximumWorkingSetSize)
        /// <summary>
        ///     The <b>SetProcessWorkingSetSize</b> function sets the minimum and maximum working
        ///     set sizes for the specified process.
        /// </summary>
        /// <param name="process">
        ///     <para>
        ///         Handle to the process whose working set sizes is to be set.
        ///     </para>
        ///     <para>
        ///         The handle must have the PROCESS_SET_QUOTA access right.
        ///     </para>
        /// </param>
        /// <param name="minimumWorkingSetSize">
        ///     <para>
        ///         Minimum working set size for the process, in bytes.  The virtual memory
        ///         manager attempts to keep at least this much memory resident in the
        ///         process whenever the process is active.
        ///     </para>
        ///     <para>
        ///         If both <i>minimumWorkingSetSize</i> and <i>maximumWorkingSetSize</i> have the
        ///         value -1, the function temporarily trims the working set of the specified
        ///         process to zero.  This essentially swaps the process out of physical RAM
        ///         memory.
        ///     </para>
        /// </param>
        /// <param name="maximumWorkingSetSize">
        ///     <para>
        ///         Maximum working set size for the process, in bytes.  The virtual memory
        ///         manager attempts to keep no more than this much memory resident in the
        ///         process whenever the process is active and memory is in short supply.
        ///     </para>
        ///     <para>
        ///         If both <i>minimumWorkingSetSize</i> and <i>maximumWorkingSetSize</i> have the
        ///         value -1, the function temporarily trims the working set of the specified
        ///         process to zero.  This essentially swaps the process out of physical RAM
        ///         memory.
        ///     </para>
        /// </param>
        /// <returns>
        ///     <para>
        ///         If the function succeeds, the return value is true.
        ///     </para>
        ///     <para>
        ///         If the function fails, the return value is false.  To get extended error
        ///         information, call <see cref="Marshal.GetLastWin32Error" />.
        ///     </para>
        /// </returns>
        /// <remarks>
        ///     <para>
        ///         The working set of a process is the set of memory pages currently visible to
        ///         the process in physical RAM memory.  These pages are resident and available
        ///         for an application to use without triggering a page fault.  The minimum and
        ///         maximum working set sizes affect the virtual memory paging behavior of a
        ///         process.
        ///     </para>
        ///     <para>
        ///         The working set of the specified process can be emptied by specifying the
        ///         value -1 for both the minimum and maximum working set sizes.
        ///     </para>
        ///     <para>
        ///         If the values of either <i>minimumWorkingSetSize</i> or
        ///         <i>maximumWorkingSetSize</i> are greater than the process' current working
        ///         set sizes, the specified process must have the SE_INC_BASE_PRIORITY_NAME
        ///         privilege.  Users in the Administrators and Power Users groups generally
        ///         have this privilege.
        ///     </para>
        ///     <para>
        ///         The operating system allocates working set sizes on a first-come,
        ///         first-served basis.  For example, if an application successfully sets 40
        ///         megabytes as its minimum working set size on a 64-megabyte system, and a
        ///         second application requests a 40-megabyte working set size, the operating
        ///         system denies the second application's request.
        ///     </para>
        ///     <para>
        ///         Using the <b>SetProcessWorkingSetSize</b> function to set an application's
        ///         minimum and maximum working set sizes does not guarantee that the requested
        ///         memory will be reserved, or that it will remain resident at all times.  When
        ///         the application is idle, or a low-memory situation causes a demand for memory,
        ///         the operating system can reduce the application's working set.  An application
        ///         can use the <b>VirtualLock</b> function to lock ranges of the application's
        ///         virtual address space in memory; however, that can potentially degrade the
        ///         performance of the system.
        ///     </para>
        ///     <para>
        ///         When you increase the working set size of an application, you are taking away
        ///         physical memory from the rest of the system.  This can degrade the performance
        ///         of other applications and the system as a whole.  It can also lead to failures
        ///         of operations that require physical memory to be present; for example,
        ///         creating processes, threads, and kernel pool.  Thus, you must use the
        ///         <b>SetProcessWorkingSetSize</b> function carefully.  You must always consider
        ///         the performance of the whole system when you are designing an application.
        ///     </para>
        /// </remarks>
        /// <seealso cref="GetProcessWorkingSetSize" />
        // <seealso cref="VirtualLock" />
        // WINBASEAPI BOOL WINAPI SetProcessWorkingSetSize(IN HANDLE hProcess, IN SIZE_T dwMinimumWorkingSetSize, IN SIZE_T dwMaximumWorkingSetSize);
        [DllImport(KERNEL_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int maximumWorkingSetSize);
        #endregion bool SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int maximumWorkingSetSize)
    }

    /// <summary>
    /// OpenGL wrapper methods and constants. See OpenGL documentation for more information.
    /// </summary>

    [CLSCompliant(false)]
    public static class Gl
    {
        public const int GL_MATRIX4_ARB = 0x000088c4;
        public const int GL_COMPRESSED_RGBA_S3TC_DXT3_EXT = 0x000083f2;
        public const int GL_ONE_MINUS_SRC_ALPHA = 0x00000303;
        public const int GL_STENCIL_BACK_FUNC = 0x00008800;
        public const int GL_AND_REVERSE = 0x00001502;
        public const int GL_UNSIGNED_SHORT_8_8_APPLE = 0x000085ba;
        public const int GL_POST_COLOR_MATRIX_BLUE_BIAS_SGI = 0x000080ba;
        public const int GL_MAT_AMBIENT_AND_DIFFUSE_BIT_PGI = 0x00200000;
        public const int GL_DRAW_BUFFER3_ARB = 0x00008828;
        public const int GL_REG_9_ATI = 0x0000892a;
        public const int GL_SGIX_sprite = 0x00000001;
        public const int GL_DEPTH_BOUNDS_EXT = 0x00008891;
        public const int GL_POINT_SIZE_GRANULARITY = 0x00000b13;
        public const int GL_CONVOLUTION_WIDTH = 0x00008018;
        public const int GL_RGB_FLOAT32_ATI = 0x00008815;
        public const int GL_COLOR_INDEXES = 0x00001603;
        public const int GL_CON_26_ATI = 0x0000895b;
        public const int GL_TRIANGLE_LIST_SUN = 0x000081d7;
        public const int GL_COLOR_ARRAY_STRIDE = 0x00008083;
        public const int GL_TEXTURE_CLIPMAP_CENTER_SGIX = 0x00008171;
        public const int GL_OUTPUT_TEXTURE_COORD21_EXT = 0x000087b2;
        public const int GL_MAX_VIEWPORT_DIMS = 0x00000d3a;
        public const int GL_GENERATE_MIPMAP_SGIS = 0x00008191;
        public const int GL_PIXEL_MAP_I_TO_G_SIZE = 0x00000cb3;
        public const int GL_LUMINANCE8_ALPHA8_EXT = 0x00008045;
        public const int GL_BINORMAL_ARRAY_STRIDE_EXT = 0x00008441;
        public const int GL_OP_RECIP_SQRT_EXT = 0x00008795;
        public const int GL_SAMPLER_2D_RECT_ARB = 0x00008b63;
        public const int GL_MATRIX29_ARB = 0x000088dd;
        public const int GL_CULL_VERTEX_IBM = 0x0001928a;
        public const int GL_MAX = 0x00008008;
        public const int GL_OBJECT_LINE_SGIS = 0x000081f7;
        public const int GL_SIGNED_LUMINANCE8_ALPHA8_NV = 0x00008704;
        public const int GL_CLAMP_READ_COLOR_ARB = 0x0000891c;
        public const int GL_PIXEL_SUBSAMPLE_4444_SGIX = 0x000085a2;
        public const int GL_COMPRESSED_LUMINANCE_ARB = 0x000084ea;
        public const int GL_TEXTURE_BINDING_CUBE_MAP_EXT = 0x00008514;
        public const int GL_STRICT_LIGHTING_HINT_PGI = 0x0001a217;
        public const int GL_FOG_COORDINATE_ARRAY_STRIDE_EXT = 0x00008455;
        public const int GL_INDEX_TEST_REF_EXT = 0x000081b7;
        public const int GL_COMBINER2_NV = 0x00008552;
        public const int GL_SMOOTH_LINE_WIDTH_RANGE = 0x00000b22;
        public const int GL_MAX_TEXTURE_COORDS_ARB = 0x00008871;
        public const int GL_DECAL = 0x00002101;
        public const int GL_UNPACK_ALIGNMENT = 0x00000cf5;
        public const int GL_MAX_PROGRAM_MATRICES_ARB = 0x0000862f;
        public const int GL_NUM_COMPRESSED_TEXTURE_FORMATS_ARB = 0x000086a2;
        public const int GL_FLOAT_RGBA32_NV = 0x0000888b;
        public const int GL_FOG_COORDINATE_ARRAY_LIST_IBM = 0x000192a4;
        public const int GL_CON_4_ATI = 0x00008945;
        public const int GL_VERTEX_ARRAY_PARALLEL_POINTERS_INTEL = 0x000083f5;
        public const int GL_COMBINE_ARB = 0x00008570;
        public const int GL_BOOL_VEC2_ARB = 0x00008b57;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_Z_EXT = 0x0000851a;
        public const int GL_BUMP_TEX_UNITS_ATI = 0x00008778;
        public const int GL_BINORMAL_ARRAY_POINTER_EXT = 0x00008443;
        public const int GL_TEXTURE21_ARB = 0x000084d5;
        public const int GL_OPERAND2_ALPHA = 0x0000859a;
        public const int GL_COLOR_ATTACHMENT7_EXT = 0x00008ce7;
        public const int GL_OPERAND0_ALPHA_EXT = 0x00008598;
        public const int GL_OUTPUT_TEXTURE_COORD1_EXT = 0x0000879e;
        public const int GL_MODELVIEW29_ARB = 0x0000873d;
        public const int GL_VERTEX_ATTRIB_ARRAY5_NV = 0x00008655;
        public const int GL_FUNC_ADD_EXT = 0x00008006;
        public const int GL_SCALE_BY_FOUR_NV = 0x0000853f;
        public const int GL_FOG_SCALE_SGIX = 0x000081fc;
        public const int GL_CONSTANT_BORDER_HP = 0x00008151;
        public const int GL_PROGRAM_NATIVE_INSTRUCTIONS_ARB = 0x000088a2;
        public const int GL_COLOR_WRITEMASK = 0x00000c23;
        public const int GL_VERTEX_STREAM3_ATI = 0x0000876f;
        public const int GL_SPRITE_AXIAL_SGIX = 0x0000814c;
        public const int GL_TEXTURE_LIGHT_EXT = 0x00008350;
        public const int GL_NORMAL_ARRAY_TYPE = 0x0000807e;
        public const int GL_PROXY_TEXTURE_4D_SGIS = 0x00008135;
        public const int GL_SPRITE_OBJECT_ALIGNED_SGIX = 0x0000814d;
        public const int GL_NOR = 0x00001508;
        public const int GL_TEXTURE17 = 0x000084d1;
        public const int GL_TEXTURE16 = 0x000084d0;
        public const int GL_TEXTURE15 = 0x000084cf;
        public const int GL_TEXTURE14 = 0x000084ce;
        public const int GL_TEXTURE13 = 0x000084cd;
        public const int GL_TEXTURE12 = 0x000084cc;
        public const int GL_SMOOTH_LINE_WIDTH_GRANULARITY = 0x00000b23;
        public const int GL_TEXTURE10 = 0x000084ca;
        public const int GL_PACK_SKIP_PIXELS = 0x00000d04;
        public const int GL_TEXTURE19 = 0x000084d3;
        public const int GL_TEXTURE18 = 0x000084d2;
        public const int GL_PROGRAM_STRING_NV = 0x00008628;
        public const int GL_IMAGE_TRANSLATE_Y_HP = 0x00008158;
        public const int GL_POINT_SIZE = 0x00000b11;
        public const int GL_TEXTURE2_ARB = 0x000084c2;
        public const int GL_SGIS_texture4D = 0x00000001;
        public const int GL_TEXTURE_COLOR_WRITEMASK_SGIS = 0x000081ef;
        public const int GL_EVAL_TRIANGULAR_2D_NV = 0x000086c1;
        public const int GL_TEXTURE_WRAP_S = 0x00002802;
        public const int GL_MAX_OPTIMIZED_VERTEX_SHADER_LOCAL_CONSTANTS_EXT = 0x000087cc;
        public const int GL_UNPACK_SWAP_BYTES = 0x00000cf0;
        public const int GL_TEXTURE_COMPARE_FAIL_VALUE_ARB = 0x000080bf;
        public const int GL_DRAW_BUFFER13 = 0x00008832;
        public const int GL_MAX_PROGRAM_LOOP_DEPTH_NV = 0x000088f7;
        public const int GL_VERTEX_ARRAY_SIZE = 0x0000807a;
        public const int GL_REG_6_ATI = 0x00008927;
        public const int GL_ACTIVE_TEXTURE_ARB = 0x000084e0;
        public const int GL_DRAW_BUFFER14_ATI = 0x00008833;
        public const int GL_SPARE0_PLUS_SECONDARY_COLOR_NV = 0x00008532;
        public const int GL_CURRENT_QUERY = 0x00008865;
        public const int GL_TEXTURE_BLUE_SIZE = 0x0000805e;
        public const int GL_RESAMPLE_REPLICATE_OML = 0x00008986;
        public const int GL_SATURATE_BIT_ATI = 0x00000040;
        public const int GL_COMPRESSED_INTENSITY_ARB = 0x000084ec;
        public const int GL_DRAW_BUFFER = 0x00000c01;
        public const int GL_1PASS_SGIS = 0x000080a1;
        public const int GL_REPLACEMENT_CODE_SUN = 0x000081d8;
        public const int GL_POST_TEXTURE_FILTER_SCALE_SGIX = 0x0000817a;
        public const int GL_QUAD_LUMINANCE4_SGIS = 0x00008120;
        public const int GL_INTERPOLATE_ARB = 0x00008575;
        public const int GL_NORMAL_ARRAY = 0x00008075;
        public const int GL_COMBINER4_NV = 0x00008554;
        public const int GL_POST_COLOR_MATRIX_GREEN_BIAS = 0x000080b9;
        public const int GL_COMBINER_SCALE_NV = 0x00008548;
        public const int GL_EVAL_VERTEX_ATTRIB6_NV = 0x000086cc;
        public const int GL_LUMINANCE4 = 0x0000803f;
        public const int GL_UNSIGNED_INT_2_10_10_10_REV = 0x00008368;
        public const int GL_VARIABLE_F_NV = 0x00008528;
        public const int GL_COLOR_SUM = 0x00008458;
        public const int GL_DRAW_BUFFER10 = 0x0000882f;
        public const int GL_ACCUM_RED_BITS = 0x00000d58;
        public const int GL_DRAW_BUFFER12 = 0x00008831;
        public const int GL_LUMINANCE8 = 0x00008040;
        public const int GL_DRAW_BUFFER14 = 0x00008833;
        public const int GL_TEXTURE22_ARB = 0x000084d6;
        public const int GL_LINE_WIDTH_GRANULARITY = 0x00000b23;
        public const int GL_HISTOGRAM_ALPHA_SIZE_EXT = 0x0000802b;
        public const int GL_FRAGMENT_LIGHT7_SGIX = 0x00008413;
        public const int GL_MAX_SHININESS_NV = 0x00008504;
        public const int GL_ACCUM = 0x00000100;
        public const int GL_CONSTANT_COLOR1_NV = 0x0000852b;
        public const int GL_FOG_COORDINATE_ARRAY_EXT = 0x00008457;
        public const int GL_ALPHA_BITS = 0x00000d55;
        public const int GL_TEXTURE_COMPRESSION_HINT = 0x000084ef;
        public const int GL_REG_21_ATI = 0x00008936;
        public const int GL_BUFFER_MAP_POINTER = 0x000088bd;
        public const int GL_INVALID_VALUE = 0x00000501;
        public const int GL_INTENSITY_FLOAT32_ATI = 0x00008817;
        public const int GL_BLUE_MIN_CLAMP_INGR = 0x00008562;
        public const int GL_ONE = 0x00000001;
        public const int GL_TRANSPOSE_TEXTURE_MATRIX_ARB = 0x000084e5;
        public const int GL_DISTANCE_ATTENUATION_EXT = 0x00008129;
        public const int GL_UNSIGNED_INT_24_8_NV = 0x000084fa;
        public const int GL_VERSION_1_4 = 0x00000001;
        public const int GL_VERSION_1_5 = 0x00000001;
        public const int GL_VERSION_1_2 = 0x00000001;
        public const int GL_VERSION_1_3 = 0x00000001;
        public const int GL_VERSION_1_1 = 0x00000001;
        public const int GL_FRAGMENT_COLOR_MATERIAL_FACE_SGIX = 0x00008402;
        public const int GL_COMBINE_RGB_ARB = 0x00008571;
        public const int GL_EXT_shared_texture_palette = 0x00000001;
        public const int GL_POINT_SIZE_MAX = 0x00008127;
        public const int GL_MAP1_GRID_DOMAIN = 0x00000dd0;
        public const int GL_OUTPUT_TEXTURE_COORD0_EXT = 0x0000879d;
        public const int GL_PERSPECTIVE_CORRECTION_HINT = 0x00000c50;
        public const int GL_TEXTURE_COMPARE_FUNC = 0x0000884d;
        public const int GL_SECONDARY_COLOR_ARRAY_POINTER = 0x0000845d;
        public const int GL_VERTEX_STREAM1_ATI = 0x0000876d;
        public const int GL_MAX_DRAW_BUFFERS_ATI = 0x00008824;
        public const int GL_QUERY_RESULT_AVAILABLE = 0x00008867;
        public const int GL_TEXTURE_DEFORMATION_BIT_SGIX = 0x00000001;
        public const int GL_MATRIX17_ARB = 0x000088d1;
        public const int GL_SCISSOR_TEST = 0x00000c11;
        public const int GL_MATRIX25_ARB = 0x000088d9;
        public const int GL_PROGRAM_ERROR_POSITION_NV = 0x0000864b;
        public const int GL_DEPENDENT_RGB_TEXTURE_3D_NV = 0x00008859;
        public const int GL_PACK_IMAGE_HEIGHT = 0x0000806c;
        public const int GL_PROXY_TEXTURE_1D = 0x00008063;
        public const int GL_SGIS_texture_border_clamp = 0x00000001;
        public const int GL_BIAS_BY_NEGATIVE_ONE_HALF_NV = 0x00008541;
        public const int GL_FOG_SCALE_VALUE_SGIX = 0x000081fd;
        public const int GL_SAMPLE_MASK_SGIS = 0x000080a0;
        public const int GL_VERTEX_ATTRIB_ARRAY4_NV = 0x00008654;
        public const int GL_CONVOLUTION_HEIGHT_EXT = 0x00008019;
        public const int GL_IUI_V2F_EXT = 0x000081ad;
        public const int GL_POLYGON_TOKEN = 0x00000703;
        public const int GL_CONSTANT_BORDER = 0x00008151;
        public const int GL_TRUE = 0x00000001;
        public const int GL_VERTEX_ARRAY_RANGE_APPLE = 0x0000851d;
        public const int GL_COLOR_ARRAY_PARALLEL_POINTERS_INTEL = 0x000083f7;
        public const int GL_CURRENT_RASTER_POSITION_VALID = 0x00000b08;
        public const int GL_COORD_REPLACE_NV = 0x00008862;
        public const int GL_2PASS_1_SGIS = 0x000080a3;
        public const int GL_TEXTURE_PRE_SPECULAR_HP = 0x00008169;
        public const int GL_RGB16_EXT = 0x00008054;
        public const int GL_DRAW_BUFFER7_ATI = 0x0000882c;
        public const int GL_SPRITE_EYE_ALIGNED_SGIX = 0x0000814e;
        public const int GL_PROGRAM_NATIVE_TEX_INSTRUCTIONS_ARB = 0x00008809;
        public const int GL_4PASS_2_EXT = 0x000080a6;
        public const int GL_RENDERER = 0x00001f01;
        public const int GL_FORCE_BLUE_TO_ONE_NV = 0x00008860;
        public const int GL_VIEWPORT = 0x00000ba2;
        public const int GL_VARIANT_DATATYPE_EXT = 0x000087e5;
        public const int GL_COLOR4_BIT_PGI = 0x00020000;
        public const int GL_FOG_COLOR = 0x00000b66;
        public const int GL_POINT_SIZE_MIN_ARB = 0x00008126;
        public const int GL_RGB8_EXT = 0x00008051;
        public const int GL_POLYGON_OFFSET_FACTOR = 0x00008038;
        public const int GL_LUMINANCE6_ALPHA2_EXT = 0x00008044;
        public const int GL_POST_CONVOLUTION_BLUE_BIAS_EXT = 0x00008022;
        public const int GL_EVAL_VERTEX_ATTRIB12_NV = 0x000086d2;
        public const int GL_MAP2_VERTEX_ATTRIB8_4_NV = 0x00008678;
        public const int GL_UNSIGNED_INT_S8_S8_8_8_NV = 0x000086da;
        public const int GL_OPERAND0_ALPHA = 0x00008598;
        public const int GL_REDUCE = 0x00008016;
        public const int GL_ONE_MINUS_CONSTANT_ALPHA_EXT = 0x00008004;
        public const int GL_DONT_CARE = 0x00001100;
        public const int GL_BLEND_SRC = 0x00000be1;
        public const int GL_TEXTURE_FILTER_CONTROL = 0x00008500;
        public const int GL_VERTEX_STATE_PROGRAM_NV = 0x00008621;
        public const int GL_INTERLACE_OML = 0x00008980;
        public const int GL_TEXTURE4_ARB = 0x000084c4;
        public const int GL_LOCAL_CONSTANT_EXT = 0x000087c3;
        public const int GL_NORMAL_ARRAY_PARALLEL_POINTERS_INTEL = 0x000083f6;
        public const int GL_MAX_RENDERBUFFER_SIZE_EXT = 0x000084e8;
        public const int GL_COLOR_BUFFER_BIT = 0x00004000;
        public const int GL_TEXTURE_HI_SIZE_NV = 0x0000871b;
        public const int GL_2_BYTES = 0x00001407;
        public const int GL_UNPACK_RESAMPLE_SGIX = 0x0000842d;
        public const int GL_R1UI_T2F_C4F_N3F_V3F_SUN = 0x000085cb;
        public const int GL_VERTEX_PROGRAM_ARB = 0x00008620;
        public const int GL_VERTEX_ARRAY_POINTER_EXT = 0x0000808e;
        public const int GL_MAP1_VERTEX_ATTRIB5_4_NV = 0x00008665;
        public const int GL_VARIABLE_B_NV = 0x00008524;
        public const int GL_UNPACK_IMAGE_DEPTH_SGIS = 0x00008133;
        public const int GL_SGIX_impact_pixel_texture = 0x00000001;
        public const int GL_IDENTITY_NV = 0x0000862a;
        public const int GL_PIXEL_SUBSAMPLE_2424_SGIX = 0x000085a3;
        public const int GL_EXPAND_NORMAL_NV = 0x00008538;
        public const int GL_EXT_texture = 0x00000001;
        public const int GL_MATRIX31_ARB = 0x000088df;
        public const int GL_VERTEX_ATTRIB_ARRAY6_NV = 0x00008656;
        public const int GL_LIGHT_ENV_MODE_SGIX = 0x00008407;
        public const int GL_MULTISAMPLE_BIT = 0x20000000;
        public const int GL_NORMAL_ARRAY_POINTER_EXT = 0x0000808f;
        public const int GL_MAX_VARYING_FLOATS = 0x00008b4b;
        public const int GL_DUAL_LUMINANCE4_SGIS = 0x00008114;
        public const int GL_STENCIL_WRITEMASK = 0x00000b98;
        public const int GL_PIXEL_MAP_I_TO_I_SIZE = 0x00000cb0;
        public const int GL_4PASS_0_SGIS = 0x000080a4;
        public const int GL_VERTEX_ARRAY_BUFFER_BINDING_ARB = 0x00008896;
        public const int GL_STENCIL_REF = 0x00000b97;
        public const int GL_INTENSITY8 = 0x0000804b;
        public const int GL_PACK_ALIGNMENT = 0x00000d05;
        public const int GL_INTENSITY4 = 0x0000804a;
        public const int GL_SHADER_CONSISTENT_NV = 0x000086dd;
        public const int GL_VERTEX_BLEND_ARB = 0x000086a7;
        public const int GL_TEXTURE_WIDTH = 0x00001000;
        public const int GL_VERTEX_ATTRIB_ARRAY_POINTER_ARB = 0x00008645;
        public const int GL_OP_ADD_EXT = 0x00008787;
        public const int GL_MODELVIEW0_EXT = 0x00001700;
        public const int GL_4PASS_3_SGIS = 0x000080a7;
        public const int GL_NATIVE_GRAPHICS_HANDLE_PGI = 0x0001a202;
        public const int GL_FENCE_APPLE = 0x00008a0b;
        public const int GL_TRANSPOSE_COLOR_MATRIX_ARB = 0x000084e6;
        public const int GL_INSTRUMENT_BUFFER_POINTER_SGIX = 0x00008180;
        public const int GL_FLOAT_RGBA_NV = 0x00008883;
        public const int GL_FLOAT_RGBA16_NV = 0x0000888a;
        public const int GL_COLOR_INDEX1_EXT = 0x000080e2;
        public const int GL_PROGRAM_NATIVE_ALU_INSTRUCTIONS_ARB = 0x00008808;
        public const int GL_STENCIL_FAIL = 0x00000b94;
        public const int GL_REPLACEMENT_CODE_ARRAY_POINTER_SUN = 0x000085c3;
        public const int GL_CURRENT_INDEX = 0x00000b01;
        public const int GL_PRESERVE_ATI = 0x00008762;
        public const int GL_REFLECTION_MAP_NV = 0x00008512;
        public const int GL_SOURCE1_RGB_EXT = 0x00008581;
        public const int GL_EXP2 = 0x00000801;
        public const int GL_TRIANGLE_MESH_SUN = 0x00008615;
        public const int GL_TEXTURE_ENV_MODE = 0x00002200;
        public const int GL_FRAGMENT_LIGHTING_SGIX = 0x00008400;
        public const int GL_UNSIGNED_SHORT_5_5_5_1_EXT = 0x00008034;
        public const int GL_SOURCE0_ALPHA_EXT = 0x00008588;
        public const int GL_SAMPLE_ALPHA_TO_ONE_ARB = 0x0000809f;
        public const int GL_BLEND = 0x00000be2;
        public const int GL_PREVIOUS = 0x00008578;
        public const int GL_MAP2_COLOR_4 = 0x00000db0;
        public const int GL_FALSE = 0x00000000;
        public const int GL_TEXTURE20_ARB = 0x000084d4;
        public const int GL_ARRAY_OBJECT_BUFFER_ATI = 0x00008766;
        public const int GL_CURRENT_RASTER_INDEX = 0x00000b05;
        public const int GL_TRANSPOSE_PROJECTION_MATRIX_ARB = 0x000084e4;
        public const int GL_EVAL_VERTEX_ATTRIB10_NV = 0x000086d0;
        public const int GL_FENCE_STATUS_NV = 0x000084f3;
        public const int GL_EXPAND_NEGATE_NV = 0x00008539;
        public const int GL_REG_14_ATI = 0x0000892f;
        public const int GL_RESCALE_NORMAL = 0x0000803a;
        public const int GL_INDEX_MATERIAL_FACE_EXT = 0x000081ba;
        public const int GL_WRITE_PIXEL_DATA_RANGE_LENGTH_NV = 0x0000887a;
        public const int GL_4PASS_3_EXT = 0x000080a7;
        public const int GL_FRAMEBUFFER_COMPLETE_EXT = 0x00008cd5;
        public const int GL_VERTEX_ATTRIB_ARRAY_SIZE_ARB = 0x00008623;
        public const int GL_MODELVIEW0_STACK_DEPTH_EXT = 0x00000ba3;
        public const int GL_SAMPLE_COVERAGE_ARB = 0x000080a0;
        public const int GL_INTENSITY12_EXT = 0x0000804c;
        public const int GL_EYE_PLANE_ABSOLUTE_NV = 0x0000855c;
        public const int GL_DELETE_STATUS = 0x00008b80;
        public const int GL_ALPHA_FLOAT32_ATI = 0x00008816;
        public const int GL_TEXTURE7_ARB = 0x000084c7;
        public const int GL_NEVER = 0x00000200;
        public const int GL_INCR_WRAP = 0x00008507;
        public const int GL_STATIC_ATI = 0x00008760;
        public const int GL_VERTEX_ARRAY_COUNT_EXT = 0x0000807d;
        public const int GL_AND_INVERTED = 0x00001504;
        public const int GL_TEXTURE_RECTANGLE_ARB = 0x000084f5;
        public const int GL_RGB10_EXT = 0x00008052;
        public const int GL_COLOR_ARRAY_BUFFER_BINDING = 0x00008898;
        public const int GL_SOURCE0_ALPHA_ARB = 0x00008588;
        public const int GL_OP_FRAC_EXT = 0x00008789;
        public const int GL_FOG_HINT = 0x00000c54;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_X_ARB = 0x00008515;
        public const int GL_TEXTURE_GREEN_SIZE = 0x0000805d;
        public const int GL_FLOAT_VEC3_ARB = 0x00008b51;
        public const int GL_PROGRAM_ATTRIBS_ARB = 0x000088ac;
        public const int GL_TEXTURE_BINDING_1D = 0x00008068;
        public const int GL_EMBOSS_MAP_NV = 0x0000855f;
        public const int GL_FRAMEBUFFER_INCOMPLETE_ATTACHMENTS_EXT = 0x00008cd6;
        public const int GL_STACK_UNDERFLOW = 0x00000504;
        public const int GL_FOG_START = 0x00000b63;
        public const int GL_OFFSET_PROJECTIVE_TEXTURE_RECTANGLE_NV = 0x00008852;
        public const int GL_TEXTURE_DS_SIZE_NV = 0x0000871d;
        public const int GL_FOG_COORD_SRC = 0x00008450;
        public const int GL_POINT_SIZE_MAX_SGIS = 0x00008127;
        public const int GL_RENDERBUFFER_HEIGHT_EXT = 0x00008d43;
        public const int GL_OBJECT_ACTIVE_ATTRIBUTES_ARB = 0x00008b89;
        public const int GL_MAX_TEXTURE_UNITS = 0x000084e2;
        public const int GL_DOT3_RGB_EXT = 0x00008740;
        public const int GL_PROXY_TEXTURE_1D_EXT = 0x00008063;
        public const int GL_POST_COLOR_MATRIX_RED_BIAS_SGI = 0x000080b8;
        public const int GL_LINE_WIDTH = 0x00000b21;
        public const int GL_BGR_EXT = 0x000080e0;
        public const int GL_RGBA12_EXT = 0x0000805a;
        public const int GL_IMAGE_ROTATE_ANGLE_HP = 0x00008159;
        public const int GL_EDGEFLAG_BIT_PGI = 0x00040000;
        public const int GL_CON_1_ATI = 0x00008942;
        public const int GL_PROXY_TEXTURE_RECTANGLE_ARB = 0x000084f7;
        public const int GL_OBJECT_PLANE = 0x00002501;
        public const int GL_IUI_N3F_V2F_EXT = 0x000081af;
        public const int GL_ACCUM_BLUE_BITS = 0x00000d5a;
        public const int GL_FOG_DENSITY = 0x00000b62;
        public const int GL_PIXEL_COUNT_AVAILABLE_NV = 0x00008867;
        public const int GL_MIRROR_CLAMP_TO_EDGE_ATI = 0x00008743;
        public const int GL_OPERAND0_RGB_ARB = 0x00008590;
        public const int GL_OUTPUT_TEXTURE_COORD22_EXT = 0x000087b3;
        public const int GL_MAX_CONVOLUTION_HEIGHT_EXT = 0x0000801b;
        public const int GL_VARIANT_ARRAY_EXT = 0x000087e8;
        public const int GL_INDEX_ARRAY_POINTER = 0x00008091;
        public const int GL_INT_VEC4_ARB = 0x00008b55;
        public const int GL_SIGNED_HILO16_NV = 0x000086fa;
        public const int GL_LUMINANCE_ALPHA32F_ARB = 0x00008819;
        public const int GL_RED_MAX_CLAMP_INGR = 0x00008564;
        public const int GL_T2F_C3F_V3F = 0x00002a2a;
        public const int GL_VERTEX_ATTRIB_ARRAY0_NV = 0x00008650;
        public const int GL_CCW = 0x00000901;
        public const int GL_VERTEX_ARRAY_LIST_STRIDE_IBM = 0x000192a8;
        public const int GL_MATRIX26_ARB = 0x000088da;
        public const int GL_COLOR_MATERIAL_PARAMETER = 0x00000b56;
        public const int GL_DYNAMIC_ATI = 0x00008761;
        public const int GL_TEXTURE31_ARB = 0x000084df;
        public const int GL_TEXTURE_STACK_DEPTH = 0x00000ba5;
        public const int GL_INDEX_MATERIAL_PARAMETER_EXT = 0x000081b9;
        public const int GL_REGISTER_COMBINERS_NV = 0x00008522;
        public const int GL_CON_31_ATI = 0x00008960;
        public const int GL_PACK_CMYK_HINT_EXT = 0x0000800e;
        public const int GL_SIGNED_ALPHA8_NV = 0x00008706;
        public const int GL_MODELVIEW14_ARB = 0x0000872e;
        public const int GL_NEGATIVE_X_EXT = 0x000087d9;
        public const int GL_DOT_PRODUCT_PASS_THROUGH_NV = 0x0000885b;
        public const int GL_LO_SCALE_NV = 0x0000870f;
        public const int GL_RGB5_EXT = 0x00008050;
        public const int GL_MAX_4D_TEXTURE_SIZE_SGIS = 0x00008138;
        public const int GL_FLOAT_RGB16_NV = 0x00008888;
        public const int GL_COLOR_SUM_CLAMP_NV = 0x0000854f;
        public const int GL_OUTPUT_TEXTURE_COORD8_EXT = 0x000087a5;
        public const int GL_TEXTURE_COORD_ARRAY_STRIDE_EXT = 0x0000808a;
        public const int GL_MAX_CLIPMAP_DEPTH_SGIX = 0x00008177;
        public const int GL_MULTISAMPLE_BIT_EXT = 0x20000000;
        public const int GL_EXT_blend_logic_op = 0x00000001;
        public const int GL_TYPE_RGBA_FLOAT_ATI = 0x00008820;
        public const int GL_SOURCE2_ALPHA = 0x0000858a;
        public const int GL_VERTEX_ARRAY_RANGE_POINTER_APPLE = 0x00008521;
        public const int GL_DEPTH_COMPONENT16 = 0x000081a5;
        public const int GL_POLYGON_SMOOTH_HINT = 0x00000c53;
        public const int GL_MATRIX11_ARB = 0x000088cb;
        public const int GL_TEXTURE27_ARB = 0x000084db;
        public const int GL_3_BYTES = 0x00001408;
        public const int GL_FOG_COORD_ARRAY_BUFFER_BINDING = 0x0000889d;
        public const int GL_MATRIX1_ARB = 0x000088c1;
        public const int GL_BGRA_EXT = 0x000080e1;
        public const int GL_LIGHT_MODEL_AMBIENT = 0x00000b53;
        public const int GL_INVARIANT_VALUE_EXT = 0x000087ea;
        public const int GL_HISTOGRAM_FORMAT_EXT = 0x00008027;
        public const int GL_VERTEX_ARRAY_BUFFER_BINDING = 0x00008896;
        public const int GL_ORDER = 0x00000a01;
        public const int GL_BOOL_ARB = 0x00008b56;
        public const int GL_SAMPLER_2D = 0x00008b5e;
        public const int GL_MATRIX8_ARB = 0x000088c8;
        public const int GL_SGIX_calligraphic_fragment = 0x00000001;
        public const int GL_REPLACE_OLDEST_SUN = 0x00000003;
        public const int GL_MAP_COLOR = 0x00000d10;
        public const int GL_STENCIL_INDEX16_EXT = 0x00008d49;
        public const int GL_COLOR_ARRAY_TYPE_EXT = 0x00008082;
        public const int GL_HISTOGRAM_BLUE_SIZE_EXT = 0x0000802a;
        public const int GL_CULL_VERTEX_EXT = 0x000081aa;
        public const int GL_CURRENT_SECONDARY_COLOR = 0x00008459;
        public const int GL_SGI_color_table = 0x00000001;
        public const int GL_TEXCOORD4_BIT_PGI = unchecked((int)0x80000000);
        public const int GL_FOG_COORDINATE_SOURCE_EXT = 0x00008450;
        public const int GL_DRAW_BUFFER1_ARB = 0x00008826;
        public const int GL_VERTEX_SHADER_VARIANTS_EXT = 0x000087d0;
        public const int GL_PROXY_TEXTURE_CUBE_MAP = 0x0000851b;
        public const int GL_VERTEX_ATTRIB_ARRAY_TYPE = 0x00008625;
        public const int GL_TEXTURE_PRIORITY = 0x00008066;
        public const int GL_REFERENCE_PLANE_SGIX = 0x0000817d;
        public const int GL_NORMAL_ARRAY_COUNT_EXT = 0x00008080;
        public const int GL_COMPRESSED_LUMINANCE_ALPHA_ARB = 0x000084eb;
        public const int GL_4PASS_1_SGIS = 0x000080a5;
        public const int GL_SAMPLE_ALPHA_TO_ONE_SGIS = 0x0000809f;
        public const int GL_NORMAL_MAP_ARB = 0x00008511;
        public const int GL_MODELVIEW8_ARB = 0x00008728;
        public const int GL_EXT_copy_texture = 0x00000001;
        public const int GL_MINMAX = 0x0000802e;
        public const int GL_NORMAL_ARRAY_TYPE_EXT = 0x0000807e;
        public const int GL_NUM_INSTRUCTIONS_PER_PASS_ATI = 0x00008971;
        public const int GL_BITMAP_TOKEN = 0x00000704;
        public const int GL_PIXEL_FRAGMENT_RGB_SOURCE_SGIS = 0x00008354;
        public const int GL_CURRENT_RASTER_NORMAL_SGIX = 0x00008406;
        public const int GL_ASYNC_DRAW_PIXELS_SGIX = 0x0000835d;
        public const int GL_POLYGON_OFFSET_FILL = 0x00008037;
        public const int GL_PRIMARY_COLOR_NV = 0x0000852c;
        public const int GL_POST_CONVOLUTION_GREEN_SCALE = 0x0000801d;
        public const int GL_SUBTRACT_ARB = 0x000084e7;
        public const int GL_STENCIL_BACK_FAIL_ATI = 0x00008801;
        public const int GL_PREVIOUS_EXT = 0x00008578;
        public const int GL_COMPRESSED_RGB_FXT1_3DFX = 0x000086b0;
        public const int GL_REG_3_ATI = 0x00008924;
        public const int GL_PRIMITIVE_RESTART_NV = 0x00008558;
        public const int GL_BLEND_SRC_RGB = 0x000080c9;
        public const int GL_TEXTURE_FILTER_CONTROL_EXT = 0x00008500;
        public const int GL_MAX_TEXTURE_IMAGE_UNITS_ARB = 0x00008872;
        public const int GL_MIRRORED_REPEAT_IBM = 0x00008370;
        public const int GL_LINEAR_CLIPMAP_NEAREST_SGIX = 0x0000844f;
        public const int GL_OFFSET_TEXTURE_MATRIX_NV = 0x000086e1;
        public const int GL_DEPTH_COMPONENT24_SGIX = 0x000081a6;
        public const int GL_INSTRUMENT_MEASUREMENTS_SGIX = 0x00008181;
        public const int GL_VERTEX_WEIGHT_ARRAY_POINTER_EXT = 0x00008510;
        public const int GL_ZERO = 0x00000000;
        public const int GL_C4UB_V3F = 0x00002a23;
        public const int GL_FRAMEBUFFER_INCOMPLETE_MISSING_ATTACHMENT_EXT = 0x00008cd7;
        public const int GL_UNPACK_SKIP_PIXELS = 0x00000cf4;
        public const int GL_PIXEL_PACK_BUFFER_BINDING_ARB = 0x000088ed;
        public const int GL_BYTE = 0x00001400;
        public const int GL_CW = 0x00000900;
        public const int GL_OFFSET_TEXTURE_2D_NV = 0x000086e8;
        public const int GL_MODELVIEW = 0x00001700;
        public const int GL_MODELVIEW1_STACK_DEPTH_EXT = 0x00008502;
        public const int GL_MAX_PROGRAM_TEX_INSTRUCTIONS_ARB = 0x0000880c;
        public const int GL_POLYGON_MODE = 0x00000b40;
        public const int GL_REG_23_ATI = 0x00008938;
        public const int GL_LUMINANCE12_ALPHA4_EXT = 0x00008046;
        public const int GL_OPERAND0_ALPHA_ARB = 0x00008598;
        public const int GL_SOURCE1_RGB_ARB = 0x00008581;
        public const int GL_PACK_LSB_FIRST = 0x00000d01;
        public const int GL_MOV_ATI = 0x00008961;
        public const int GL_DS_BIAS_NV = 0x00008716;
        public const int GL_INTERLACE_READ_OML = 0x00008981;
        public const int GL_UNSIGNED_INT_8_8_8_8_EXT = 0x00008035;
        public const int GL_OUTPUT_TEXTURE_COORD14_EXT = 0x000087ab;
        public const int GL_HISTOGRAM_SINK_EXT = 0x0000802d;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_Z_ARB = 0x00008519;
        public const int GL_VERTEX_ARRAY_STRIDE_EXT = 0x0000807c;
        public const int GL_DEPTH_COMPONENT32_SGIX = 0x000081a7;
        public const int GL_MATRIX6_ARB = 0x000088c6;
        public const int GL_COLOR_MATRIX_STACK_DEPTH_SGI = 0x000080b2;
        public const int GL_VERTEX_ARRAY_EXT = 0x00008074;
        public const int GL_CON_30_ATI = 0x0000895f;
        public const int GL_REG_31_ATI = 0x00008940;
        public const int GL_TEXTURE28_ARB = 0x000084dc;
        public const int GL_INDEX_ARRAY_LIST_STRIDE_IBM = 0x000192ab;
        public const int GL_FRAMEBUFFER_INCOMPLETE_DRAW_BUFFER_EXT = 0x00008cdb;
        public const int GL_MAT_AMBIENT_BIT_PGI = 0x00100000;
        public const int GL_REDUCE_EXT = 0x00008016;
        public const int GL_GLOBAL_ALPHA_FACTOR_SUN = 0x000081da;
        public const int GL_VIBRANCE_BIAS_NV = 0x00008719;
        public const int GL_SAMPLE_ALPHA_TO_ONE = 0x0000809f;
        public const int GL_DRAW_BUFFER11_ATI = 0x00008830;
        public const int GL_VERTEX_ARRAY_BINDING_APPLE = 0x000085b5;
        public const int GL_REPLACE_MIDDLE_SUN = 0x00000002;
        public const int GL_OFFSET_HILO_PROJECTIVE_TEXTURE_RECTANGLE_NV = 0x00008857;
        public const int GL_INVARIANT_EXT = 0x000087c2;
        public const int GL_SGIX_polynomial_ffd = 0x00000001;
        public const int GL_VARIANT_EXT = 0x000087c1;
        public const int GL_STENCIL_INDEX1_EXT = 0x00008d46;
        public const int GL_TEXTURE_WRAP_Q_SGIS = 0x00008137;
        public const int GL_FOG_COORDINATE_ARRAY = 0x00008457;
        public const int GL_PN_TRIANGLES_NORMAL_MODE_LINEAR_ATI = 0x000087f7;
        public const int GL_FRAMEBUFFER_ATTACHMENT_OBJECT_TYPE_EXT = 0x00008cd0;
        public const int GL_INDEX_ARRAY_STRIDE_EXT = 0x00008086;
        public const int GL_SGIS_multisample = 0x00000001;
        public const int GL_EQUIV = 0x00001509;
        public const int GL_CON_28_ATI = 0x0000895d;
        public const int GL_OPERAND2_RGB_EXT = 0x00008592;
        public const int GL_NORMAL_MAP = 0x00008511;
        public const int GL_TEXTURE0_ARB = 0x000084c0;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_Z_EXT = 0x00008519;
        public const int GL_REG_26_ATI = 0x0000893b;
        public const int GL_SGIX_vertex_preclip = 0x00000001;
        public const int GL_OP_MIN_EXT = 0x0000878b;
        public const int GL_PROGRAM_FORMAT_ARB = 0x00008876;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_X = 0x00008515;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_Y = 0x00008517;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_Z = 0x00008519;
        public const int GL_MAP2_VERTEX_ATTRIB9_4_NV = 0x00008679;
        public const int GL_EIGHTH_BIT_ATI = 0x00000020;
        public const int GL_FLOAT_MAT3_ARB = 0x00008b5b;
        public const int GL_REG_4_ATI = 0x00008925;
        public const int GL_SPOT_DIRECTION = 0x00001204;
        public const int GL_VECTOR_EXT = 0x000087bf;
        public const int GL_TEXTURE11 = 0x000084cb;
        public const int GL_ALLOW_DRAW_FRG_HINT_PGI = 0x0001a210;
        public const int GL_PIXEL_TEX_GEN_Q_ROUND_SGIX = 0x00008185;
        public const int GL_ALPHA_MIN_CLAMP_INGR = 0x00008563;
        public const int GL_KEEP = 0x00001e00;
        public const int GL_TRANSFORM_BIT = 0x00001000;
        public const int GL_OUTPUT_TEXTURE_COORD30_EXT = 0x000087bb;
        public const int GL_BLEND_EQUATION = 0x00008009;
        public const int GL_PACK_SKIP_VOLUMES_SGIS = 0x00008130;
        public const int GL_OFFSET_PROJECTIVE_TEXTURE_2D_NV = 0x00008850;
        public const int GL_LUMINANCE16 = 0x00008042;
        public const int GL_LUMINANCE12 = 0x00008041;
        public const int GL_RGBA_FLOAT_MODE_ARB = 0x00008820;
        public const int GL_COLOR_TABLE_GREEN_SIZE_SGI = 0x000080db;
        public const int GL_CON_20_ATI = 0x00008955;
        public const int GL_FOG_COORDINATE_SOURCE = 0x00008450;
        public const int GL_MATRIX15_ARB = 0x000088cf;
        public const int GL_MAX_MAP_TESSELLATION_NV = 0x000086d6;
        public const int GL_LUMINANCE = 0x00001909;
        public const int GL_OCCLUSION_TEST_HP = 0x00008165;
        public const int GL_TEXTURE_INTERNAL_FORMAT = 0x00001003;
        public const int GL_EDGE_FLAG_ARRAY_LIST_IBM = 0x000192a3;
        public const int GL_EYE_LINE_SGIS = 0x000081f6;
        public const int GL_HISTOGRAM_RED_SIZE_EXT = 0x00008028;
        public const int GL_INVALID_FRAMEBUFFER_OPERATION_EXT = 0x00000506;
        public const int GL_STENCIL_TEST_TWO_SIDE_EXT = 0x00008910;
        public const int GL_DRAW_BUFFER13_ARB = 0x00008832;
        public const int GL_MAP_ATTRIB_V_ORDER_NV = 0x000086c4;
        public const int GL_DUAL_INTENSITY4_SGIS = 0x00008118;
        public const int GL_DOMAIN = 0x00000a02;
        public const int GL_EMBOSS_CONSTANT_NV = 0x0000855e;
        public const int GL_UNPACK_SKIP_ROWS = 0x00000cf3;
        public const int GL_MODULATE_SIGNED_ADD_ATI = 0x00008745;
        public const int GL_STATIC_DRAW_ARB = 0x000088e4;
        public const int GL_CONVOLUTION_FILTER_BIAS_EXT = 0x00008015;
        public const int GL_ENABLE_BIT = 0x00002000;
        public const int GL_VERTEX_ATTRIB_ARRAY_BUFFER_BINDING_ARB = 0x0000889f;
        public const int GL_HISTOGRAM_WIDTH_EXT = 0x00008026;
        public const int GL_INTERLACE_READ_INGR = 0x00008568;
        public const int GL_BLEND_DST_ALPHA = 0x000080ca;
        public const int GL_SIGNED_RGB_UNSIGNED_ALPHA_NV = 0x0000870c;
        public const int GL_LINE_TOKEN = 0x00000702;
        public const int GL_FLOAT_RG32_NV = 0x00008887;
        public const int GL_NEAREST_MIPMAP_LINEAR = 0x00002702;
        public const int GL_AUX2 = 0x0000040b;
        public const int GL_AUX3 = 0x0000040c;
        public const int GL_AUX0 = 0x00000409;
        public const int GL_AUX1 = 0x0000040a;
        public const int GL_DRAW_BUFFER7_ARB = 0x0000882c;
        public const int GL_COLOR_TABLE = 0x000080d0;
        public const int GL_OUTPUT_TEXTURE_COORD25_EXT = 0x000087b6;
        public const int GL_NEGATIVE_W_EXT = 0x000087dc;
        public const int GL_TEXTURE_4D_SGIS = 0x00008134;
        public const int GL_REPEAT = 0x00002901;
        public const int GL_FRAGMENT_NORMAL_EXT = 0x0000834a;
        public const int GL_ALPHA4 = 0x0000803b;
        public const int GL_DSDT_MAG_VIB_NV = 0x000086f7;
        public const int GL_FOG_COORDINATE_ARRAY_BUFFER_BINDING = 0x0000889d;
        public const int GL_ALPHA8 = 0x0000803c;
        public const int GL_SGIS_texture_select = 0x00000001;
        public const int GL_VERTEX_ATTRIB_ARRAY_NORMALIZED = 0x0000886a;
        public const int GL_PROJECTION_MATRIX = 0x00000ba7;
        public const int GL_UNSIGNED_SHORT_4_4_4_4_REV_EXT = 0x00008365;
        public const int GL_INTENSITY8_EXT = 0x0000804b;
        public const int GL_UNSIGNED_SHORT_5_5_5_1 = 0x00008034;
        public const int GL_FLOAT_VEC4 = 0x00008b52;
        public const int GL_TEXTURE25_ARB = 0x000084d9;
        public const int GL_FRAMEBUFFER_INCOMPLETE_READ_BUFFER_EXT = 0x00008cdc;
        public const int GL_CONVOLUTION_BORDER_COLOR = 0x00008154;
        public const int GL_CND_ATI = 0x0000896a;
        public const int GL_DEPENDENT_RGB_TEXTURE_CUBE_MAP_NV = 0x0000885a;
        public const int GL_EDGE_FLAG_ARRAY_STRIDE = 0x0000808c;
        public const int GL_LUMINANCE6_ALPHA2 = 0x00008044;
        public const int GL_TEXTURE_DEPTH_EXT = 0x00008071;
        public const int GL_REG_0_ATI = 0x00008921;
        public const int GL_DEPTH_STENCIL_TO_BGRA_NV = 0x0000886f;
        public const int GL_DUAL_ALPHA12_SGIS = 0x00008112;
        public const int GL_HI_SCALE_NV = 0x0000870e;
        public const int GL_MAX_MODELVIEW_STACK_DEPTH = 0x00000d36;
        public const int GL_IGNORE_BORDER_HP = 0x00008150;
        public const int GL_VERTEX_ATTRIB_ARRAY7_NV = 0x00008657;
        public const int GL_NUM_INPUT_INTERPOLATOR_COMPONENTS_ATI = 0x00008973;
        public const int GL_C4UB_V2F = 0x00002a22;
        public const int GL_INTERPOLATE = 0x00008575;
        public const int GL_BUFFER_SIZE = 0x00008764;
        public const int GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT = 0x000084ff;
        public const int GL_MAX_OPTIMIZED_VERTEX_SHADER_INSTRUCTIONS_EXT = 0x000087ca;
        public const int GL_COMPRESSED_RGBA_S3TC_DXT1_EXT = 0x000083f1;
        public const int GL_SGIX_blend_alpha_minmax = 0x00000001;
        public const int GL_DEFORMATIONS_MASK_SGIX = 0x00008196;
        public const int GL_AUX_BUFFERS = 0x00000c00;
        public const int GL_MAP1_VERTEX_ATTRIB3_4_NV = 0x00008663;
        public const int GL_FRAMEZOOM_SGIX = 0x0000818b;
        public const int GL_PROXY_COLOR_TABLE = 0x000080d3;
        public const int GL_TEXTURE_MATERIAL_PARAMETER_EXT = 0x00008352;
        public const int GL_LUMINANCE32F_ARB = 0x00008818;
        public const int GL_MATRIX_MODE = 0x00000ba0;
        public const int GL_FRONT_LEFT = 0x00000400;
        public const int GL_SGIX_scalebias_hint = 0x00000001;
        public const int GL_RECLAIM_MEMORY_HINT_PGI = 0x0001a1fe;
        public const int GL_PASS_THROUGH_TOKEN = 0x00000700;
        public const int GL_RGB_SCALE_ARB = 0x00008573;
        public const int GL_SGIX_clipmap = 0x00000001;
        public const int GL_CURRENT_TANGENT_EXT = 0x0000843b;
        public const int GL_MAX_CLIP_PLANES = 0x00000d32;
        public const int GL_ADD = 0x00000104;
        public const int GL_TEXTURE_MAX_LOD = 0x0000813b;
        public const int GL_VARIANT_ARRAY_STRIDE_EXT = 0x000087e6;
        public const int GL_REG_18_ATI = 0x00008933;
        public const int GL_RGBA32F_ARB = 0x00008814;
        public const int GL_GREATER = 0x00000204;
        public const int GL_EDGE_FLAG_ARRAY_POINTER_EXT = 0x00008093;
        public const int GL_VARIABLE_C_NV = 0x00008525;
        public const int GL_OPERAND2_ALPHA_ARB = 0x0000859a;
        public const int GL_PROJECTION_STACK_DEPTH = 0x00000ba4;
        public const int GL_OUTPUT_COLOR0_EXT = 0x0000879b;
        public const int GL_SGIX_instruments = 0x00000001;
        public const int GL_SAMPLER_CUBE_ARB = 0x00008b60;
        public const int GL_SECONDARY_COLOR_ARRAY_SIZE = 0x0000845a;
        public const int GL_PREFER_DOUBLEBUFFER_HINT_PGI = 0x0001a1f8;
        public const int GL_MATRIX_INDEX_ARRAY_TYPE_ARB = 0x00008847;
        public const int GL_EXT_blend_minmax = 0x00000001;
        public const int GL_COORD_REPLACE_ARB = 0x00008862;
        public const int GL_FLOAT_R32_NV = 0x00008885;
        public const int GL_MATRIX27_ARB = 0x000088db;
        public const int GL_INDEX_ARRAY_LIST_IBM = 0x000192a1;
        public const int GL_SAMPLER_1D_SHADOW_ARB = 0x00008b61;
        public const int GL_MAX_3D_TEXTURE_SIZE_EXT = 0x00008073;
        public const int GL_PROGRAM_NATIVE_ADDRESS_REGISTERS_ARB = 0x000088b2;
        public const int GL_TRACK_MATRIX_NV = 0x00008648;
        public const int GL_MIRROR_CLAMP_ATI = 0x00008742;
        public const int GL_MAX_VERTEX_ATTRIBS_ARB = 0x00008869;
        public const int GL_TEXTURE_MAX_LEVEL = 0x0000813d;
        public const int GL_VERTEX_PROGRAM_NV = 0x00008620;
        public const int GL_NEGATIVE_Y_EXT = 0x000087da;
        public const int GL_SPRITE_SGIX = 0x00008148;
        public const int GL_BLEND_EQUATION_ALPHA = 0x0000883d;
        public const int GL_RGB4_S3TC = 0x000083a1;
        public const int GL_DOT_PRODUCT_TEXTURE_1D_NV = 0x0000885c;
        public const int GL_TEXTURE_LEQUAL_R_SGIX = 0x0000819c;
        public const int GL_SAMPLER_3D_ARB = 0x00008b5f;
        public const int GL_RGBA16F_ARB = 0x0000881a;
        public const int GL_VERTEX_STREAM7_ATI = 0x00008773;
        public const int GL_TRIANGLE_STRIP = 0x00000005;
        public const int GL_PIXEL_TEX_GEN_MODE_SGIX = 0x0000832b;
        public const int GL_PIXEL_TRANSFORM_2D_MATRIX_EXT = 0x00008338;
        public const int GL_DU8DV8_ATI = 0x0000877a;
        public const int GL_EXT_polygon_offset = 0x00000001;
        public const int GL_TEXCOORD2_BIT_PGI = 0x20000000;
        public const int GL_COLOR_ATTACHMENT1_EXT = 0x00008ce1;
        public const int GL_MAX_PROGRAM_INSTRUCTIONS_ARB = 0x000088a1;
        public const int GL_EDGE_FLAG_ARRAY_POINTER = 0x00008093;
        public const int GL_C4F_N3F_V3F = 0x00002a26;
        public const int GL_TEXTURE14_ARB = 0x000084ce;
        public const int GL_SGI_texture_color_table = 0x00000001;
        public const int GL_MAP2_VERTEX_3 = 0x00000db7;
        public const int GL_CON_10_ATI = 0x0000894b;
        public const int GL_MAX_PROGRAM_PARAMETERS_ARB = 0x000088a9;
        public const int GL_MAP2_VERTEX_4 = 0x00000db8;
        public const int GL_DEPENDENT_AR_TEXTURE_2D_NV = 0x000086e9;
        public const int GL_DST_COLOR = 0x00000306;
        public const int GL_TEXTURE24_ARB = 0x000084d8;
        public const int GL_MINMAX_SINK = 0x00008030;
        public const int GL_NATIVE_GRAPHICS_END_HINT_PGI = 0x0001a204;
        public const int GL_MAP2_VERTEX_ATTRIB14_4_NV = 0x0000867e;
        public const int GL_MAX_LIGHTS = 0x00000d31;
        public const int GL_UNPACK_CLIENT_STORAGE_APPLE = 0x000085b2;
        public const int GL_PROXY_TEXTURE_CUBE_MAP_EXT = 0x0000851b;
        public const int GL_OP_LOG_BASE_2_EXT = 0x00008792;
        public const int GL_DEPTH_COMPONENT16_ARB = 0x000081a5;
        public const int GL_STENCIL_BACK_WRITEMASK = 0x00008ca5;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_X_EXT = 0x00008516;
        public const int GL_FLOAT_R16_NV = 0x00008884;
        public const int GL_ALLOW_DRAW_WIN_HINT_PGI = 0x0001a20f;
        public const int GL_CON_21_ATI = 0x00008956;
        public const int GL_OPERAND1_RGB_EXT = 0x00008591;
        public const int GL_ONE_MINUS_DST_COLOR = 0x00000307;
        public const int GL_POST_COLOR_MATRIX_ALPHA_SCALE = 0x000080b7;
        public const int GL_PROXY_TEXTURE_COLOR_TABLE_SGI = 0x000080bd;
        public const int GL_COMBINER_MUX_SUM_NV = 0x00008547;
        public const int GL_PROGRAM_NATIVE_ATTRIBS_ARB = 0x000088ae;
        public const int GL_TEXTURE_DEPTH = 0x00008071;
        public const int GL_OPERAND2_ALPHA_EXT = 0x0000859a;
        public const int GL_CON_14_ATI = 0x0000894f;
        public const int GL_VERTEX_ATTRIB_ARRAY14_NV = 0x0000865e;
        public const int GL_ACCUM_GREEN_BITS = 0x00000d59;
        public const int GL_DEPTH_COMPONENT24_ARB = 0x000081a6;
        public const int GL_LEQUAL = 0x00000203;
        public const int GL_VERSION = 0x00001f02;
        public const int GL_REG_24_ATI = 0x00008939;
        public const int GL_CLIENT_ALL_ATTRIB_BITS = unchecked((int)0xffffffff);
        public const int GL_MIN_EXT = 0x00008007;
        public const int GL_LIST_BIT = 0x00020000;
        public const int GL_COMBINER_AB_DOT_PRODUCT_NV = 0x00008545;
        public const int GL_PACK_SWAP_BYTES = 0x00000d00;
        public const int GL_PIXEL_TEX_GEN_Q_CEILING_SGIX = 0x00008184;
        public const int GL_INDEX_TEST_FUNC_EXT = 0x000081b6;
        public const int GL_MAX_RATIONAL_EVAL_ORDER_NV = 0x000086d7;
        public const int GL_STACK_OVERFLOW = 0x00000503;
        public const int GL_DOT_PRODUCT_REFLECT_CUBE_MAP_NV = 0x000086f2;
        public const int GL_TEXTURE18_ARB = 0x000084d2;
        public const int GL_TANGENT_ARRAY_EXT = 0x00008439;
        public const int GL_MAX_ASYNC_READ_PIXELS_SGIX = 0x00008361;
        public const int GL_VERTEX_ARRAY_STRIDE = 0x0000807c;
        public const int GL_SECONDARY_COLOR_ARRAY_LIST_IBM = 0x000192a5;
        public const int GL_TEXTURE_INDEX_SIZE_EXT = 0x000080ed;
        public const int GL_LUMINANCE_ALPHA = 0x0000190a;
        public const int GL_MAP1_TANGENT_EXT = 0x00008444;
        public const int GL_ACCUM_CLEAR_VALUE = 0x00000b80;
        public const int GL_DRAW_PIXEL_TOKEN = 0x00000705;
        public const int GL_AMBIENT_AND_DIFFUSE = 0x00001602;
        public const int GL_MAX_PROGRAM_MATRIX_STACK_DEPTH_ARB = 0x0000862e;
        public const int GL_MAX_CUBE_MAP_TEXTURE_SIZE_EXT = 0x0000851c;
        public const int GL_PROGRAM_TEX_INSTRUCTIONS_ARB = 0x00008806;
        public const int GL_STENCIL_BACK_PASS_DEPTH_FAIL_ATI = 0x00008802;
        public const int GL_POST_TEXTURE_FILTER_BIAS_RANGE_SGIX = 0x0000817b;
        public const int GL_FULL_RANGE_EXT = 0x000087e1;
        public const int GL_1PASS_EXT = 0x000080a1;
        public const int GL_BUFFER_MAPPED = 0x000088bc;
        public const int GL_COMPRESSED_TEXTURE_FORMATS = 0x000086a3;
        public const int GL_MAP2_VERTEX_ATTRIB7_4_NV = 0x00008677;
        public const int GL_DEPTH_PASS_INSTRUMENT_COUNTERS_SGIX = 0x00008311;
        public const int GL_NUM_COMPRESSED_TEXTURE_FORMATS = 0x000086a2;
        public const int GL_BLEND_EQUATION_RGB = 0x00008009;
        public const int GL_CURRENT_ATTRIB_NV = 0x00008626;
        public const int GL_PIXEL_MAP_S_TO_S_SIZE = 0x00000cb1;
        public const int GL_FRAMEZOOM_FACTOR_SGIX = 0x0000818c;
        public const int GL_INVERSE_NV = 0x0000862b;
        public const int GL_NO_ERROR = 0x00000000;
        public const int GL_FLAT = 0x00001d00;
        public const int GL_AVERAGE_EXT = 0x00008335;
        public const int GL_COMPRESSED_RGB_S3TC_DXT1_EXT = 0x000083f0;
        public const int GL_OBJECT_INFO_LOG_LENGTH_ARB = 0x00008b84;
        public const int GL_COEFF = 0x00000a00;
        public const int GL_SGIX_texture_scale_bias = 0x00000001;
        public const int GL_POINT_SPRITE_COORD_ORIGIN = 0x00008ca0;
        public const int GL_ACTIVE_UNIFORMS = 0x00008b86;
        public const int GL_SAMPLES = 0x000080a9;
        public const int GL_STENCIL_BACK_VALUE_MASK = 0x00008ca4;
        public const int GL_MAX_CUBE_MAP_TEXTURE_SIZE = 0x0000851c;
        public const int GL_QUAD_MESH_SUN = 0x00008614;
        public const int GL_CURRENT_MATRIX_INDEX_ARB = 0x00008845;
        public const int GL_DRAW_BUFFER8_ARB = 0x0000882d;
        public const int GL_VERTEX_ATTRIB_ARRAY10_NV = 0x0000865a;
        public const int GL_ACTIVE_TEXTURE = 0x000084e0;
        public const int GL_INT = 0x00001404;
        public const int GL_FLOAT_VEC3 = 0x00008b51;
        public const int GL_EXT_blend_subtract = 0x00000001;
        public const int GL_DEPTH_COMPONENT = 0x00001902;
        public const int GL_ZOOM_X = 0x00000d16;
        public const int GL_POINT_TOKEN = 0x00000701;
        public const int GL_IMAGE_MAG_FILTER_HP = 0x0000815c;
        public const int GL_PRIMITIVE_RESTART_INDEX_NV = 0x00008559;
        public const int GL_PACK_SKIP_IMAGES = 0x0000806b;
        public const int GL_BUMP_NUM_TEX_UNITS_ATI = 0x00008777;
        public const int GL_EXT_packed_pixels = 0x00000001;
        public const int GL_DEPTH_COMPONENT32 = 0x000081a7;
        public const int GL_ABGR_EXT = 0x00008000;
        public const int GL_MAX_COLOR_MATRIX_STACK_DEPTH = 0x000080b3;
        public const int GL_NEAREST_MIPMAP_NEAREST = 0x00002700;
        public const int GL_SOURCE0_ALPHA = 0x00008588;
        public const int GL_YCBCR_MESA = 0x00008757;
        public const int GL_EXT_blend_color = 0x00000001;
        public const int GL_T2F_IUI_N3F_V2F_EXT = 0x000081b3;
        public const int GL_SAMPLE_BUFFERS_ARB = 0x000080a8;
        public const int GL_INDEX_LOGIC_OP = 0x00000bf1;
        public const int GL_IUI_V3F_EXT = 0x000081ae;
        public const int GL_DRAW_BUFFER11 = 0x00008830;
        public const int GL_SAMPLES_SGIS = 0x000080a9;
        public const int GL_LIGHT_MODEL_LOCAL_VIEWER = 0x00000b51;
        public const int GL_INTENSITY12 = 0x0000804c;
        public const int GL_PIXEL_TEX_GEN_Q_FLOOR_SGIX = 0x00008186;
        public const int GL_TEXTURE_LUMINANCE_TYPE_ARB = 0x00008c14;
        public const int GL_INTENSITY16 = 0x0000804d;
        public const int GL_BLEND_DST_RGB_EXT = 0x000080c8;
        public const int GL_DRAW_BUFFER15 = 0x00008834;
        public const int GL_DUAL_LUMINANCE_ALPHA4_SGIS = 0x0000811c;
        public const int GL_BUFFER_MAP_POINTER_ARB = 0x000088bd;
        public const int GL_LUMINANCE12_ALPHA4 = 0x00008046;
        public const int GL_MULTISAMPLE_SGIS = 0x0000809d;
        public const int GL_GEQUAL = 0x00000206;
        public const int GL_CONVOLUTION_BORDER_MODE = 0x00008013;
        public const int GL_OBJECT_POINT_SGIS = 0x000081f5;
        public const int GL_TEXTURE_LOD_BIAS = 0x00008501;
        public const int GL_COLOR_ATTACHMENT2_EXT = 0x00008ce2;
        public const int GL_VERTEX_ARRAY_RANGE_WITHOUT_FLUSH_NV = 0x00008533;
        public const int GL_VERTEX_ATTRIB_ARRAY_ENABLED_ARB = 0x00008622;
        public const int GL_MAX_NAME_STACK_DEPTH = 0x00000d37;
        public const int GL_UNSIGNED_SHORT_4_4_4_4_EXT = 0x00008033;
        public const int GL_DRAW_BUFFER1_ATI = 0x00008826;
        public const int GL_COLOR_ARRAY_EXT = 0x00008076;
        public const int GL_MODELVIEW7_ARB = 0x00008727;
        public const int GL_HISTOGRAM_SINK = 0x0000802d;
        public const int GL_PROXY_POST_COLOR_MATRIX_COLOR_TABLE_SGI = 0x000080d5;
        public const int GL_REG_12_ATI = 0x0000892d;
        public const int GL_POST_COLOR_MATRIX_RED_SCALE_SGI = 0x000080b4;
        public const int GL_CURRENT_TEXTURE_COORDS = 0x00000b03;
        public const int GL_VERTEX_SHADER_ARB = 0x00008b31;
        public const int GL_LUMINANCE8_ALPHA8 = 0x00008045;
        public const int GL_STENCIL_ATTACHMENT_EXT = 0x00008d20;
        public const int GL_INTENSITY_EXT = 0x00008049;
        public const int GL_FOG_FUNC_SGIS = 0x0000812a;
        public const int GL_PROGRAM_LENGTH_ARB = 0x00008627;
        public const int GL_TRANSPOSE_MODELVIEW_MATRIX_ARB = 0x000084e3;
        public const int GL_TEXTURE = 0x00001702;
        public const int GL_LUMINANCE12_ALPHA12_EXT = 0x00008047;
        public const int GL_ALPHA16F_ARB = 0x0000881c;
        public const int GL_WEIGHT_ARRAY_TYPE_ARB = 0x000086a9;
        public const int GL_PROJECTION = 0x00001701;
        public const int GL_SAMPLE_BUFFERS = 0x000080a8;
        public const int GL_MODELVIEW1_MATRIX_EXT = 0x00008506;
        public const int GL_TEXTURE_LO_SIZE_NV = 0x0000871c;
        public const int GL_TEXTURE_BORDER_COLOR = 0x00001004;
        public const int GL_NORMAL_ARRAY_LIST_IBM = 0x0001929f;
        public const int GL_MAX_VERTEX_SHADER_INVARIANTS_EXT = 0x000087c7;
        public const int GL_MAD_ATI = 0x00008968;
        public const int GL_OR_REVERSE = 0x0000150b;
        public const int GL_NORMAL_ARRAY_BUFFER_BINDING_ARB = 0x00008897;
        public const int GL_FRAGMENT_LIGHT2_SGIX = 0x0000840e;
        public const int GL_PROGRAM_STRING_ARB = 0x00008628;
        public const int GL_CURRENT_SECONDARY_COLOR_EXT = 0x00008459;
        public const int GL_LINEAR = 0x00002601;
        public const int GL_HALF_BIT_ATI = 0x00000008;
        public const int GL_QUAD_INTENSITY8_SGIS = 0x00008123;
        public const int GL_PROXY_TEXTURE_3D_EXT = 0x00008070;
        public const int GL_FRONT_RIGHT = 0x00000401;
        public const int GL_OFFSET_HILO_TEXTURE_RECTANGLE_NV = 0x00008855;
        public const int GL_MAX_EVAL_ORDER = 0x00000d30;
        public const int GL_STENCIL_PASS_DEPTH_PASS = 0x00000b96;
        public const int GL_MAX_TRACK_MATRIX_STACK_DEPTH_NV = 0x0000862e;
        public const int GL_INVERT = 0x0000150a;
        public const int GL_SHARPEN_TEXTURE_FUNC_POINTS_SGIS = 0x000080b0;
        public const int GL_INTERPOLATE_EXT = 0x00008575;
        public const int GL_EVAL_VERTEX_ATTRIB11_NV = 0x000086d1;
        public const int GL_INTENSITY16_EXT = 0x0000804d;
        public const int GL_MULTISAMPLE = 0x0000809d;
        public const int GL_MAX_PROGRAM_IF_DEPTH_NV = 0x000088f6;
        public const int GL_CURRENT_VERTEX_ATTRIB = 0x00008626;
        public const int GL_TEXTURE_LUMINANCE_SIZE_EXT = 0x00008060;
        public const int GL_TEXTURE_COORD_ARRAY_LIST_IBM = 0x000192a2;
        public const int GL_BLEND_SRC_ALPHA = 0x000080cb;
        public const int GL_FIXED_ONLY_ARB = 0x0000891d;
        public const int GL_LESS = 0x00000201;
        public const int GL_MIRROR_CLAMP_EXT = 0x00008742;
        public const int GL_MULTISAMPLE_BIT_3DFX = 0x20000000;
        public const int GL_LINE_STIPPLE = 0x00000b24;
        public const int GL_VERTEX_PROGRAM_POINT_SIZE = 0x00008642;
        public const int GL_FOG_COORD_ARRAY_STRIDE = 0x00008455;
        public const int GL_CONVOLUTION_BORDER_COLOR_HP = 0x00008154;
        public const int GL_VERTEX_ARRAY_RANGE_NV = 0x0000851d;
        public const int GL_IMAGE_CUBIC_WEIGHT_HP = 0x0000815e;
        public const int GL_TEXTURE_POST_SPECULAR_HP = 0x00008168;
        public const int GL_MAP2_INDEX = 0x00000db1;
        public const int GL_TEXTURE_3D_EXT = 0x0000806f;
        public const int GL_EYE_DISTANCE_TO_LINE_SGIS = 0x000081f2;
        public const int GL_GENERATE_MIPMAP_HINT_SGIS = 0x00008192;
        public const int GL_PROXY_TEXTURE_3D = 0x00008070;
        public const int GL_ACTIVE_STENCIL_FACE_EXT = 0x00008911;
        public const int GL_SRC2_ALPHA = 0x0000858a;
        public const int GL_CURRENT_RASTER_COLOR = 0x00000b04;
        public const int GL_VERTEX_STREAM2_ATI = 0x0000876e;
        public const int GL_TEXTURE26_ARB = 0x000084da;
        public const int GL_LINE_STIPPLE_PATTERN = 0x00000b25;
        public const int GL_MAT_COLOR_INDEXES_BIT_PGI = 0x01000000;
        public const int GL_MATRIX_INDEX_ARRAY_POINTER_ARB = 0x00008849;
        public const int GL_MODELVIEW1_EXT = 0x0000850a;
        public const int GL_EVAL_VERTEX_ATTRIB13_NV = 0x000086d3;
        public const int GL_TEXTURE_COMPRESSED_IMAGE_SIZE_ARB = 0x000086a0;
        public const int GL_MAX_FOG_FUNC_POINTS_SGIS = 0x0000812c;
        public const int GL_CON_25_ATI = 0x0000895a;
        public const int GL_INDEX_CLEAR_VALUE = 0x00000c20;
        public const int GL_MAX_TEXTURE_IMAGE_UNITS = 0x00008872;
        public const int GL_NORMAL_MAP_EXT = 0x00008511;
        public const int GL_CALLIGRAPHIC_FRAGMENT_SGIX = 0x00008183;
        public const int GL_REPLICATE_BORDER = 0x00008153;
        public const int GL_INDEX_WRITEMASK = 0x00000c21;
        public const int GL_TEXTURE29_ARB = 0x000084dd;
        public const int GL_TEXTURE_LUMINANCE_SIZE = 0x00008060;
        public const int GL_POINT_FADE_THRESHOLD_SIZE_ARB = 0x00008128;
        public const int GL_RENDERBUFFER_INTERNAL_FORMAT_EXT = 0x00008d44;
        public const int GL_ARRAY_BUFFER_BINDING_ARB = 0x00008894;
        public const int GL_COLOR_ARRAY_LIST_IBM = 0x000192a0;
        public const int GL_SAMPLE_MASK_INVERT_EXT = 0x000080ab;
        public const int GL_SWIZZLE_STR_ATI = 0x00008976;
        public const int GL_SCISSOR_BIT = 0x00080000;
        public const int GL_INDEX_ARRAY_POINTER_EXT = 0x00008091;
        public const int GL_BLEND_EQUATION_EXT = 0x00008009;
        public const int GL_MAP2_VERTEX_ATTRIB15_4_NV = 0x0000867f;
        public const int GL_MAX_PROGRAM_NATIVE_TEX_INSTRUCTIONS_ARB = 0x0000880f;
        public const int GL_REG_16_ATI = 0x00008931;
        public const int GL_FRAMEBUFFER_INCOMPLETE_DIMENSIONS_EXT = 0x00008cd9;
        public const int GL_RED_MIN_CLAMP_INGR = 0x00008560;
        public const int GL_EVAL_VERTEX_ATTRIB9_NV = 0x000086cf;
        public const int GL_CON_19_ATI = 0x00008954;
        public const int GL_COLOR_ARRAY_BUFFER_BINDING_ARB = 0x00008898;
        public const int GL_UNSIGNED_BYTE_3_3_2_EXT = 0x00008032;
        public const int GL_REG_15_ATI = 0x00008930;
        public const int GL_OPERAND2_RGB_ARB = 0x00008592;
        public const int GL_IMPLEMENTATION_COLOR_READ_TYPE_OES = 0x00008b9a;
        public const int GL_READ_PIXEL_DATA_RANGE_POINTER_NV = 0x0000887d;
        public const int GL_EMBOSS_LIGHT_NV = 0x0000855d;
        public const int GL_MODELVIEW15_ARB = 0x0000872f;
        public const int GL_COLOR_TABLE_WIDTH = 0x000080d9;
        public const int GL_LINEAR_MIPMAP_LINEAR = 0x00002703;
        public const int GL_VARIANT_VALUE_EXT = 0x000087e4;
        public const int GL_SGIS_point_parameters = 0x00000001;
        public const int GL_POLYGON_STIPPLE_BIT = 0x00000010;
        public const int GL_MODELVIEW2_ARB = 0x00008722;
        public const int GL_DRAW_BUFFER0_ARB = 0x00008825;
        public const int GL_MAX_VERTEX_ARRAY_RANGE_ELEMENT_NV = 0x00008520;
        public const int GL_OFFSET_PROJECTIVE_TEXTURE_2D_SCALE_NV = 0x00008851;
        public const int GL_RGBA4_S3TC = 0x000083a3;
        public const int GL_OBJECT_SUBTYPE_ARB = 0x00008b4f;
        public const int GL_DSDT_NV = 0x000086f5;
        public const int GL_DOT_PRODUCT_TEXTURE_CUBE_MAP_NV = 0x000086f0;
        public const int GL_MAX_PROGRAM_NATIVE_TEMPORARIES_ARB = 0x000088a7;
        public const int GL_HI_BIAS_NV = 0x00008714;
        public const int GL_BLUE_BIT_ATI = 0x00000004;
        public const int GL_MAX_VERTEX_TEXTURE_IMAGE_UNITS_ARB = 0x00008b4c;
        public const int GL_MINMAX_FORMAT = 0x0000802f;
        public const int GL_CONVOLUTION_FILTER_BIAS = 0x00008015;
        public const int GL_MODELVIEW0_ARB = 0x00001700;
        public const int GL_LUMINANCE16_ALPHA16_EXT = 0x00008048;
        public const int GL_DT_SCALE_NV = 0x00008711;
        public const int GL_TANGENT_ARRAY_POINTER_EXT = 0x00008442;
        public const int GL_PIXEL_TRANSFORM_2D_STACK_DEPTH_EXT = 0x00008336;
        public const int GL_POINT_FADE_THRESHOLD_SIZE = 0x00008128;
        public const int GL_NORMALIZE = 0x00000ba1;
        public const int GL_CON_5_ATI = 0x00008946;
        public const int GL_VERTEX_PROGRAM_POINT_SIZE_NV = 0x00008642;
        public const int GL_VARIANT_ARRAY_TYPE_EXT = 0x000087e7;
        public const int GL_OFFSET_TEXTURE_2D_BIAS_NV = 0x000086e3;
        public const int GL_SWIZZLE_STRQ_DQ_ATI = 0x0000897b;
        public const int GL_LUMINANCE_ALPHA_FLOAT32_ATI = 0x00008819;
        public const int GL_NEGATIVE_Z_EXT = 0x000087db;
        public const int GL_TEXTURE_RESIDENT = 0x00008067;
        public const int GL_EXT_subtexture = 0x00000001;
        public const int GL_DUDV_ATI = 0x00008779;
        public const int GL_FRAGMENT_LIGHT_MODEL_LOCAL_VIEWER_SGIX = 0x00008408;
        public const int GL_SET = 0x0000150f;
        public const int GL_BUFFER_MAPPED_ARB = 0x000088bc;
        public const int GL_PIXEL_TILE_HEIGHT_SGIX = 0x00008141;
        public const int GL_EYE_RADIAL_NV = 0x0000855b;
        public const int GL_VERTEX_WEIGHT_ARRAY_TYPE_EXT = 0x0000850e;
        public const int GL_TEXTURE_ENV_COLOR = 0x00002201;
        public const int GL_DEPTH_RANGE = 0x00000b70;
        public const int GL_MAX_GENERAL_COMBINERS_NV = 0x0000854d;
        public const int GL_IUI_N3F_V3F_EXT = 0x000081b0;
        public const int GL_CURRENT_FOG_COORDINATE = 0x00008453;
        public const int GL_MAX_CONVOLUTION_HEIGHT = 0x0000801b;
        public const int GL_OP_RECIP_EXT = 0x00008794;
        public const int GL_DEPTH_COMPONENT24 = 0x000081a6;
        public const int GL_MATRIX18_ARB = 0x000088d2;
        public const int GL_MAX_COLOR_MATRIX_STACK_DEPTH_SGI = 0x000080b3;
        public const int GL_TEXTURE6_ARB = 0x000084c6;
        public const int GL_REFERENCE_PLANE_EQUATION_SGIX = 0x0000817e;
        public const int GL_OPERAND1_ALPHA_EXT = 0x00008599;
        public const int GL_COLOR_TABLE_BLUE_SIZE_SGI = 0x000080dc;
        public const int GL_MODELVIEW5_ARB = 0x00008725;
        public const int GL_CLAMP = 0x00002900;
        public const int GL_SGIS_texture_filter4 = 0x00000001;
        public const int GL_PARALLEL_ARRAYS_INTEL = 0x000083f4;
        public const int GL_YCRCB_SGIX = 0x00008318;
        public const int GL_SAMPLE_ALPHA_TO_MASK_EXT = 0x0000809e;
        public const int GL_PACK_ROW_LENGTH = 0x00000d02;
        public const int GL_DRAW_BUFFER3_ATI = 0x00008828;
        public const int GL_MAX_PROGRAM_NATIVE_ATTRIBS_ARB = 0x000088af;
        public const int GL_READ_PIXEL_DATA_RANGE_NV = 0x00008879;
        public const int GL_FOG_COORDINATE_ARRAY_STRIDE = 0x00008455;
        public const int GL_TRANSPOSE_NV = 0x0000862c;
        public const int GL_UNPACK_RESAMPLE_OML = 0x00008985;
        public const int GL_UNSIGNED_BYTE_3_3_2 = 0x00008032;
        public const int GL_VERTEX_PRECLIP_SGIX = 0x000083ee;
        public const int GL_ALL_ATTRIB_BITS = unchecked((int)0xffffffff);
        public const int GL_DEPTH = 0x00001801;
        public const int GL_CONST_EYE_NV = 0x000086e5;
        public const int GL_COMPRESSED_RGBA = 0x000084ee;
        public const int GL_MAX_DEFORMATION_ORDER_SGIX = 0x00008197;
        public const int GL_EXT_vertex_array = 0x00000001;
        public const int GL_MULT = 0x00000103;
        public const int GL_VERTEX_SHADER_BINDING_EXT = 0x00008781;
        public const int GL_NAND = 0x0000150e;
        public const int GL_ALPHA4_EXT = 0x0000803b;
        public const int GL_INT_VEC3_ARB = 0x00008b54;
        public const int GL_TEXTURE_MAX_CLAMP_S_SGIX = 0x00008369;
        public const int GL_RGBA = 0x00001908;
        public const int GL_MATRIX14_ARB = 0x000088ce;
        public const int GL_DRAW_BUFFER2_ATI = 0x00008827;
        public const int GL_CURRENT_VERTEX_ATTRIB_ARB = 0x00008626;
        public const int GL_DUAL_LUMINANCE16_SGIS = 0x00008117;
        public const int GL_TEXTURE_COLOR_TABLE_SGI = 0x000080bc;
        public const int GL_RGB5 = 0x00008050;
        public const int GL_RGB4 = 0x0000804f;
        public const int GL_T4F_C4F_N3F_V4F = 0x00002a2d;
        public const int GL_FRAGMENT_MATERIAL_EXT = 0x00008349;
        public const int GL_TEXTURE12_ARB = 0x000084cc;
        public const int GL_CULL_VERTEX_EYE_POSITION_EXT = 0x000081ab;
        public const int GL_TRANSFORM_HINT_APPLE = 0x000085b1;
        public const int GL_RGB8 = 0x00008051;
        public const int GL_FRAGMENT_LIGHT5_SGIX = 0x00008411;
        public const int GL_RGB5_A1_EXT = 0x00008057;
        public const int GL_POINT_SPRITE_ARB = 0x00008861;
        public const int GL_WEIGHT_ARRAY_SIZE_ARB = 0x000086ab;
        public const int GL_PIXEL_TILE_GRID_HEIGHT_SGIX = 0x00008143;
        public const int GL_COPY = 0x00001503;
        public const int GL_RGB2_EXT = 0x0000804e;
        public const int GL_COLOR_ARRAY_COUNT_EXT = 0x00008084;
        public const int GL_POINT_DISTANCE_ATTENUATION_ARB = 0x00008129;
        public const int GL_VERTEX_WEIGHTING_EXT = 0x00008509;
        public const int GL_LUMINANCE12_EXT = 0x00008041;
        public const int GL_REG_11_ATI = 0x0000892c;
        public const int GL_SHARED_TEXTURE_PALETTE_EXT = 0x000081fb;
        public const int GL_BLEND_SRC_ALPHA_EXT = 0x000080cb;
        public const int GL_ADD_SIGNED_ARB = 0x00008574;
        public const int GL_T2F_V3F = 0x00002a27;
        public const int GL_MAX_OPTIMIZED_VERTEX_SHADER_LOCALS_EXT = 0x000087ce;
        public const int GL_OBJECT_DISTANCE_TO_LINE_SGIS = 0x000081f3;
        public const int GL_MATRIX21_ARB = 0x000088d5;
        public const int GL_PROGRAM_OBJECT_ARB = 0x00008b40;
        public const int GL_FRAGMENT_SHADER_DERIVATIVE_HINT = 0x00008b8b;
        public const int GL_STENCIL_BACK_PASS_DEPTH_PASS = 0x00008803;
        public const int GL_VARIABLE_E_NV = 0x00008527;
        public const int GL_DUAL_LUMINANCE12_SGIS = 0x00008116;
        public const int GL_EMISSION = 0x00001600;
        public const int GL_ATTRIB_ARRAY_SIZE_NV = 0x00008623;
        public const int GL_ALPHA8_EXT = 0x0000803c;
        public const int GL_POST_CONVOLUTION_ALPHA_SCALE_EXT = 0x0000801f;
        public const int GL_FLOAT_CLEAR_COLOR_VALUE_NV = 0x0000888d;
        public const int GL_LIST_MODE = 0x00000b30;
        public const int GL_LINK_STATUS = 0x00008b82;
        public const int GL_FRAMEBUFFER_EXT = 0x00008d40;
        public const int GL_SGIS_pixel_texture = 0x00000001;
        public const int GL_STREAM_COPY_ARB = 0x000088e2;
        public const int GL_PIXEL_CUBIC_WEIGHT_EXT = 0x00008333;
        public const int GL_SAMPLE_COVERAGE_INVERT_ARB = 0x000080ab;
        public const int GL_ATTRIB_STACK_DEPTH = 0x00000bb0;
        public const int GL_TEXTURE_FLOAT_COMPONENTS_NV = 0x0000888c;
        public const int GL_DEPENDENT_HILO_TEXTURE_2D_NV = 0x00008858;
        public const int GL_SMOOTH_POINT_SIZE_RANGE = 0x00000b12;
        public const int GL_ATTRIB_ARRAY_TYPE_NV = 0x00008625;
        public const int GL_SINGLE_COLOR_EXT = 0x000081f9;
        public const int GL_R1UI_T2F_V3F_SUN = 0x000085c9;
        public const int GL_IMAGE_MIN_FILTER_HP = 0x0000815d;
        public const int GL_MAX_LIST_NESTING = 0x00000b31;
        public const int GL_RGB4_EXT = 0x0000804f;
        public const int GL_FLOAT_MAT2_ARB = 0x00008b5a;
        public const int GL_INDEX_ARRAY_COUNT_EXT = 0x00008087;
        public const int GL_SOURCE1_ALPHA_ARB = 0x00008589;
        public const int GL_ACTIVE_ATTRIBUTE_MAX_LENGTH = 0x00008b8a;
        public const int GL_4PASS_0_EXT = 0x000080a4;
        public const int GL_POST_CONVOLUTION_RED_BIAS = 0x00008020;
        public const int GL_PROGRAM_INSTRUCTIONS_ARB = 0x000088a0;
        public const int GL_DECR_WRAP = 0x00008508;
        public const int GL_IMAGE_SCALE_X_HP = 0x00008155;
        public const int GL_PIXEL_UNPACK_BUFFER_EXT = 0x000088ec;
        public const int GL_SIGNED_RGB_NV = 0x000086fe;
        public const int GL_TEXTURE_BASE_LEVEL = 0x0000813c;
        public const int GL_SLICE_ACCUM_SUN = 0x000085cc;
        public const int GL_STATIC_DRAW = 0x000088e4;
        public const int GL_PROGRAM_LENGTH_NV = 0x00008627;
        public const int GL_DITHER = 0x00000bd0;
        public const int GL_CLAMP_TO_BORDER_SGIS = 0x0000812d;
        public const int GL_PN_TRIANGLES_NORMAL_MODE_QUADRATIC_ATI = 0x000087f8;
        public const int GL_DT_BIAS_NV = 0x00008717;
        public const int GL_NORMAL_ARRAY_EXT = 0x00008075;
        public const int GL_TEXTURE_BORDER_VALUES_NV = 0x0000871a;
        public const int GL_FRAMEBUFFER_ATTACHMENT_TEXTURE_CUBE_MAP_FACE_EXT = 0x00008cd3;
        public const int GL_RGB = 0x00001907;
        public const int GL_PROXY_TEXTURE_2D = 0x00008064;
        public const int GL_ZERO_EXT = 0x000087dd;
        public const int GL_MATRIX13_ARB = 0x000088cd;
        public const int GL_EDGE_FLAG = 0x00000b43;
        public const int GL_QUAD_TEXTURE_SELECT_SGIS = 0x00008125;
        public const int GL_MAX_TRACK_MATRICES_NV = 0x0000862f;
        public const int GL_PIXEL_TEX_GEN_ALPHA_LS_SGIX = 0x00008189;
        public const int GL_TEXTURE_CLIPMAP_OFFSET_SGIX = 0x00008173;
        public const int GL_COMBINE_EXT = 0x00008570;
        public const int GL_POINT_DISTANCE_ATTENUATION = 0x00008129;
        public const int GL_FOG_MODE = 0x00000b65;
        public const int GL_PIXEL_TILE_WIDTH_SGIX = 0x00008140;
        public const int GL_MATRIX7_NV = 0x00008637;
        public const int GL_FLOAT_MAT3 = 0x00008b5b;
        public const int GL_STREAM_DRAW = 0x000088e0;
        public const int GL_CLIP_FAR_HINT_PGI = 0x0001a221;
        public const int GL_SAMPLE_COVERAGE_INVERT = 0x000080ab;
        public const int GL_FOG = 0x00000b60;
        public const int GL_CONSTANT_EXT = 0x00008576;
        public const int GL_MAX_CONVOLUTION_WIDTH_EXT = 0x0000801a;
        public const int GL_DOT3_RGBA_ARB = 0x000086af;
        public const int GL_MATRIX_EXT = 0x000087c0;
        public const int GL_CURRENT_OCCLUSION_QUERY_ID_NV = 0x00008865;
        public const int GL_GEOMETRY_DEFORMATION_SGIX = 0x00008194;
        public const int GL_POST_COLOR_MATRIX_GREEN_BIAS_SGI = 0x000080b9;
        public const int GL_PROXY_TEXTURE_CUBE_MAP_ARB = 0x0000851b;
        public const int GL_READ_ONLY_ARB = 0x000088b8;
        public const int GL_OUTPUT_TEXTURE_COORD15_EXT = 0x000087ac;
        public const int GL_STRICT_DEPTHFUNC_HINT_PGI = 0x0001a216;
        public const int GL_VARIABLE_G_NV = 0x00008529;
        public const int GL_PN_TRIANGLES_POINT_MODE_ATI = 0x000087f2;
        public const int GL_TEXTURE_BLUE_SIZE_EXT = 0x0000805e;
        public const int GL_QUADRATIC_ATTENUATION = 0x00001209;
        public const int GL_STENCIL_BUFFER_BIT = 0x00000400;
        public const int GL_MAX_PROGRAM_NATIVE_ADDRESS_REGISTERS_ARB = 0x000088b3;
        public const int GL_MODELVIEW16_ARB = 0x00008730;
        public const int GL_PROXY_HISTOGRAM_EXT = 0x00008025;
        public const int GL_MIRROR_CLAMP_TO_EDGE_EXT = 0x00008743;
        public const int GL_POST_CONVOLUTION_ALPHA_SCALE = 0x0000801f;
        public const int GL_TEXTURE_MIN_FILTER = 0x00002801;
        public const int GL_INT_VEC2 = 0x00008b53;
        public const int GL_INT_VEC3 = 0x00008b54;
        public const int GL_INT_VEC4 = 0x00008b55;
        public const int GL_SGIX_async = 0x00000001;
        public const int GL_EDGE_FLAG_ARRAY = 0x00008079;
        public const int GL_DRAW_BUFFER5_ARB = 0x0000882a;
        public const int GL_COLOR_TABLE_RED_SIZE_SGI = 0x000080da;
        public const int GL_BLEND_DST = 0x00000be0;
        public const int GL_POST_COLOR_MATRIX_GREEN_SCALE_SGI = 0x000080b5;
        public const int GL_OUTPUT_COLOR1_EXT = 0x0000879c;
        public const int GL_MODELVIEW19_ARB = 0x00008733;
        public const int GL_FRAGMENT_SHADER_ARB = 0x00008b30;
        public const int GL_OUTPUT_TEXTURE_COORD12_EXT = 0x000087a9;
        public const int GL_UNSIGNED_SHORT = 0x00001403;
        public const int GL_TEXTURE_MAX_CLAMP_T_SGIX = 0x0000836a;
        public const int GL_DOT_PRODUCT_CONST_EYE_REFLECT_CUBE_MAP_NV = 0x000086f3;
        public const int GL_CONSTANT_ARB = 0x00008576;
        public const int GL_SIGNED_HILO_NV = 0x000086f9;
        public const int GL_MAP2_VERTEX_ATTRIB1_4_NV = 0x00008671;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_X_ARB = 0x00008516;
        public const int GL_SRC1_RGB = 0x00008581;
        public const int GL_OFFSET_HILO_TEXTURE_2D_NV = 0x00008854;
        public const int GL_CLIP_PLANE0 = 0x00003000;
        public const int GL_CLIP_PLANE1 = 0x00003001;
        public const int GL_CLIP_PLANE2 = 0x00003002;
        public const int GL_CLIP_PLANE3 = 0x00003003;
        public const int GL_CLIP_PLANE4 = 0x00003004;
        public const int GL_CLIP_PLANE5 = 0x00003005;
        public const int GL_POINT_SIZE_RANGE = 0x00000b12;
        public const int GL_NEAREST = 0x00002600;
        public const int GL_MIRRORED_REPEAT_ARB = 0x00008370;
        public const int GL_POST_IMAGE_TRANSFORM_COLOR_TABLE_HP = 0x00008162;
        public const int GL_SGIS_fog_function = 0x00000001;
        public const int GL_FORMAT_SUBSAMPLE_244_244_OML = 0x00008983;
        public const int GL_OUTPUT_TEXTURE_COORD23_EXT = 0x000087b4;
        public const int GL_MAP2_GRID_SEGMENTS = 0x00000dd3;
        public const int GL_MAP2_VERTEX_ATTRIB0_4_NV = 0x00008670;
        public const int GL_DUAL_ALPHA8_SGIS = 0x00008111;
        public const int GL_TEXTURE_HEIGHT = 0x00001001;
        public const int GL_STENCIL_INDEX8_EXT = 0x00008d48;
        public const int GL_FOG_COORD_ARRAY_TYPE = 0x00008454;
        public const int GL_VERTEX_WEIGHT_ARRAY_EXT = 0x0000850c;
        public const int GL_MAGNITUDE_BIAS_NV = 0x00008718;
        public const int GL_UPPER_LEFT = 0x00008ca2;
        public const int GL_LINEAR_SHARPEN_SGIS = 0x000080ad;
        public const int GL_SGIX_convolution_accuracy = 0x00000001;
        public const int GL_ALPHA_FLOAT16_ATI = 0x0000881c;
        public const int GL_PREVIOUS_ARB = 0x00008578;
        public const int GL_FRAGMENT_LIGHT1_SGIX = 0x0000840d;
        public const int GL_POST_COLOR_MATRIX_ALPHA_BIAS_SGI = 0x000080bb;
        public const int GL_SOURCE0_RGB = 0x00008580;
        public const int GL_R1UI_C3F_V3F_SUN = 0x000085c6;
        public const int GL_POST_COLOR_MATRIX_COLOR_TABLE_SGI = 0x000080d2;
        public const int GL_FORMAT_SUBSAMPLE_24_24_OML = 0x00008982;
        public const int GL_MAX_PROGRAM_LOCAL_PARAMETERS_ARB = 0x000088b4;
        public const int GL_FOG_BIT = 0x00000080;
        public const int GL_REG_27_ATI = 0x0000893c;
        public const int GL_MATRIX23_ARB = 0x000088d7;
        public const int GL_DRAW_BUFFER6_ARB = 0x0000882b;
        public const int GL_TEXTURE_BASE_LEVEL_SGIS = 0x0000813c;
        public const int GL_TEXTURE_LOD_BIAS_T_SGIX = 0x0000818f;
        public const int GL_FLOAT_RG_NV = 0x00008881;
        public const int GL_LINE_SMOOTH = 0x00000b20;
        public const int GL_MATRIX_PALETTE_ARB = 0x00008840;
        public const int GL_SAMPLE_MASK_VALUE_SGIS = 0x000080aa;
        public const int GL_SAMPLE_COVERAGE_VALUE = 0x000080aa;
        public const int GL_OUTPUT_TEXTURE_COORD5_EXT = 0x000087a2;
        public const int GL_MAP2_VERTEX_ATTRIB4_4_NV = 0x00008674;
        public const int GL_OUTPUT_TEXTURE_COORD7_EXT = 0x000087a4;
        public const int GL_SGIX_ir_instrument1 = 0x00000001;
        public const int GL_STENCIL_VALUE_MASK = 0x00000b93;
        public const int GL_VERTEX_ATTRIB_ARRAY9_NV = 0x00008659;
        public const int GL_OUTPUT_TEXTURE_COORD13_EXT = 0x000087aa;
        public const int GL_MAP1_VERTEX_ATTRIB0_4_NV = 0x00008660;
        public const int GL_TEXTURE_BIT = 0x00040000;
        public const int GL_CON_27_ATI = 0x0000895c;
        public const int GL_EVAL_BIT = 0x00010000;
        public const int GL_TRIANGLES = 0x00000004;
        public const int GL_NOTEQUAL = 0x00000205;
        public const int GL_MODELVIEW31_ARB = 0x0000873f;
        public const int GL_LIGHT_MODEL_COLOR_CONTROL = 0x000081f8;
        public const int GL_HISTOGRAM_WIDTH = 0x00008026;
        public const int GL_HISTOGRAM_ALPHA_SIZE = 0x0000802b;
        public const int GL_MVP_MATRIX_EXT = 0x000087e3;
        public const int GL_ACTIVE_ATTRIBUTES = 0x00008b89;
        public const int GL_TEXTURE_MAX_LEVEL_SGIS = 0x0000813d;
        public const int GL_DRAW_BUFFER4_ARB = 0x00008829;
        public const int GL_DUAL_ALPHA16_SGIS = 0x00008113;
        public const int GL_4PASS_2_SGIS = 0x000080a6;
        public const int GL_COMBINE4_NV = 0x00008503;
        public const int GL_QUERY_RESULT_ARB = 0x00008866;
        public const int GL_LINE_SMOOTH_HINT = 0x00000c52;
        public const int GL_COLOR_ATTACHMENT6_EXT = 0x00008ce6;
        public const int GL_INDEX_OFFSET = 0x00000d13;
        public const int GL_EXT_point_parameters = 0x00000001;
        public const int GL_AVERAGE_HP = 0x00008160;
        public const int GL_PROGRAM_ADDRESS_REGISTERS_ARB = 0x000088b0;
        public const int GL_SGIS_texture_edge_clamp = 0x00000001;
        public const int GL_FLOAT_RG16_NV = 0x00008886;
        public const int GL_SAMPLE_PATTERN_EXT = 0x000080ac;
        public const int GL_CLAMP_TO_EDGE_SGIS = 0x0000812f;
        public const int GL_MATRIX_INDEX_ARRAY_ARB = 0x00008844;
        public const int GL_REPLACE_EXT = 0x00008062;
        public const int GL_EDGE_FLAG_ARRAY_LIST_STRIDE_IBM = 0x000192ad;
        public const int GL_SOURCE3_ALPHA_NV = 0x0000858b;
        public const int GL_MAP1_GRID_SEGMENTS = 0x00000dd1;
        public const int GL_OCCLUSION_TEST_RESULT_HP = 0x00008166;
        public const int GL_RGB10_A2_EXT = 0x00008059;
        public const int GL_COMPRESSED_RGBA_FXT1_3DFX = 0x000086b1;
        public const int GL_COLOR_ATTACHMENT15_EXT = 0x00008cef;
        public const int GL_COLOR_TABLE_SGI = 0x000080d0;
        public const int GL_MAX_PROGRAM_LOOP_COUNT_NV = 0x000088f8;
        public const int GL_FRAGMENT_SHADER = 0x00008b30;
        public const int GL_UNSIGNED_NORMALIZED_ARB = 0x00008c17;
        public const int GL_SGIX_pixel_texture = 0x00000001;
        public const int GL_MAP2_TEXTURE_COORD_4 = 0x00000db6;
        public const int GL_MAP2_TEXTURE_COORD_3 = 0x00000db5;
        public const int GL_MAP2_TEXTURE_COORD_2 = 0x00000db4;
        public const int GL_MAP2_TEXTURE_COORD_1 = 0x00000db3;
        public const int GL_NORMAL_MAP_NV = 0x00008511;
        public const int GL_LUMINANCE12_ALPHA12 = 0x00008047;
        public const int GL_POINT_SIZE_MIN_EXT = 0x00008126;
        public const int GL_OUT_OF_MEMORY = 0x00000505;
        public const int GL_FUNC_REVERSE_SUBTRACT_EXT = 0x0000800b;
        public const int GL_T2F_IUI_V3F_EXT = 0x000081b2;
        public const int GL_UNPACK_SKIP_IMAGES_EXT = 0x0000806d;
        public const int GL_CONSTANT_ATTENUATION = 0x00001207;
        public const int GL_TEXTURE_MAX_LOD_SGIS = 0x0000813b;
        public const int GL_SGIX_resample = 0x00000001;
        public const int GL_HALF_FLOAT_NV = 0x0000140b;
        public const int GL_INDEX_ARRAY = 0x00008077;
        public const int GL_ALPHA32F_ARB = 0x00008816;
        public const int GL_POINT_FADE_THRESHOLD_SIZE_EXT = 0x00008128;
        public const int GL_TEXTURE_APPLICATION_MODE_EXT = 0x0000834f;
        public const int GL_INDEX_ARRAY_BUFFER_BINDING_ARB = 0x00008899;
        public const int GL_MAX_SPOT_EXPONENT_NV = 0x00008505;
        public const int GL_FOG_FUNC_POINTS_SGIS = 0x0000812b;
        public const int GL_MAX_RECTANGLE_TEXTURE_SIZE_NV = 0x000084f8;
        public const int GL_COLOR = 0x00001800;
        public const int GL_OUTPUT_VERTEX_EXT = 0x0000879a;
        public const int GL_LIST_BASE = 0x00000b32;
        public const int GL_HALF_BIAS_NORMAL_NV = 0x0000853a;
        public const int GL_OP_INDEX_EXT = 0x00008782;
        public const int GL_NUM_FRAGMENT_REGISTERS_ATI = 0x0000896e;
        public const int GL_POST_TEXTURE_FILTER_BIAS_SGIX = 0x00008179;
        public const int GL_DRAW_BUFFER14_ARB = 0x00008833;
        public const int GL_MAP2_VERTEX_ATTRIB11_4_NV = 0x0000867b;
        public const int GL_STENCIL_INDEX = 0x00001901;
        public const int GL_MAX_VERTEX_SHADER_INSTRUCTIONS_EXT = 0x000087c5;
        public const int GL_BOOL_VEC3_ARB = 0x00008b58;
        public const int GL_UNSIGNED_BYTE_2_3_3_REV = 0x00008362;
        public const int GL_T2F_IUI_N3F_V3F_EXT = 0x000081b4;
        public const int GL_NEGATIVE_ONE_EXT = 0x000087df;
        public const int GL_SGIX_icc_texture = 0x00000001;
        public const int GL_COLOR_ATTACHMENT11_EXT = 0x00008ceb;
        public const int GL_UNSIGNED_IDENTITY_NV = 0x00008536;
        public const int GL_TEXTURE_UNSIGNED_REMAP_MODE_NV = 0x0000888f;
        public const int GL_TEXTURE_LOD_BIAS_S_SGIX = 0x0000818e;
        public const int GL_BOOL_VEC4 = 0x00008b59;
        public const int GL_SAMPLE_MASK_INVERT_SGIS = 0x000080ab;
        public const int GL_BOOL_VEC2 = 0x00008b57;
        public const int GL_BOOL_VEC3 = 0x00008b58;
        public const int GL_SGIX_framezoom = 0x00000001;
        public const int GL_SGIS_texture_lod = 0x00000001;
        public const int GL_PROGRAM_RESIDENT_NV = 0x00008647;
        public const int GL_RGBA_FLOAT32_ATI = 0x00008814;
        public const int GL_MAX_VERTEX_SHADER_VARIANTS_EXT = 0x000087c6;
        public const int GL_STENCIL_CLEAR_VALUE = 0x00000b91;
        public const int GL_AMBIENT = 0x00001200;
        public const int GL_VIEWPORT_BIT = 0x00000800;
        public const int GL_MAX_TEXTURE_COORDS_NV = 0x00008871;
        public const int GL_SOURCE2_ALPHA_ARB = 0x0000858a;
        public const int GL_BLEND_COLOR_EXT = 0x00008005;
        public const int GL_TEXTURE_INTENSITY_SIZE = 0x00008061;
        public const int GL_DEPTH_COMPONENT32_ARB = 0x000081a7;
        public const int GL_REFLECTION_MAP = 0x00008512;
        public const int GL_C3F_V3F = 0x00002a24;
        public const int GL_MAT_SPECULAR_BIT_PGI = 0x04000000;
        public const int GL_DETAIL_TEXTURE_FUNC_POINTS_SGIS = 0x0000809c;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_Z_ARB = 0x0000851a;
        public const int GL_MAX_DRAW_BUFFERS_ARB = 0x00008824;
        public const int GL_STORAGE_CACHED_APPLE = 0x000085be;
        public const int GL_SGIX_texture_lod_bias = 0x00000001;
        public const int GL_PIXEL_MAP_R_TO_R = 0x00000c76;
        public const int GL_SAMPLE_COVERAGE = 0x000080a0;
        public const int GL_BLUE_SCALE = 0x00000d1a;
        public const int GL_SIGNED_RGBA8_NV = 0x000086fc;
        public const int GL_R1UI_C4F_N3F_V3F_SUN = 0x000085c8;
        public const int GL_PIXEL_UNPACK_BUFFER_BINDING_ARB = 0x000088ef;
        public const int GL_VERTEX_ARRAY = 0x00008074;
        public const int GL_COMPRESSED_RGBA_S3TC_DXT5_EXT = 0x000083f3;
        public const int GL_OUTPUT_TEXTURE_COORD11_EXT = 0x000087a8;
        public const int GL_SCALAR_EXT = 0x000087be;
        public const int GL_IMAGE_TRANSFORM_2D_HP = 0x00008161;
        public const int GL_VERTEX_PRECLIP_HINT_SGIX = 0x000083ef;
        public const int GL_MAX_FRAGMENT_LIGHTS_SGIX = 0x00008404;
        public const int GL_OP_MAX_EXT = 0x0000878a;
        public const int GL_COMPILE_STATUS = 0x00008b81;
        public const int GL_POST_CONVOLUTION_GREEN_BIAS = 0x00008021;
        public const int GL_SHADER_OBJECT_ARB = 0x00008b48;
        public const int GL_SPOT_CUTOFF = 0x00001206;
        public const int GL_MAX_TEXTURE_COORDS = 0x00008871;
        public const int GL_CONVOLUTION_WIDTH_EXT = 0x00008018;
        public const int GL_SGIX_flush_raster = 0x00000001;
        public const int GL_OUTPUT_TEXTURE_COORD3_EXT = 0x000087a0;
        public const int GL_VARIABLE_A_NV = 0x00008523;
        public const int GL_DEPTH_STENCIL_NV = 0x000084f9;
        public const int GL_COMPILE_AND_EXECUTE = 0x00001301;
        public const int GL_INTENSITY = 0x00008049;
        public const int GL_TEXTURE_DEPTH_SIZE_ARB = 0x0000884a;
        public const int GL_NUM_LOOPBACK_COMPONENTS_ATI = 0x00008974;
        public const int GL_WRAP_BORDER_SUN = 0x000081d4;
        public const int GL_MATRIX_INDEX_ARRAY_SIZE_ARB = 0x00008846;
        public const int GL_MATRIX22_ARB = 0x000088d6;
        public const int GL_VERTEX_ARRAY_TYPE_EXT = 0x0000807b;
        public const int GL_COLOR_TABLE_WIDTH_SGI = 0x000080d9;
        public const int GL_LINE_LOOP = 0x00000002;
        public const int GL_STREAM_DRAW_ARB = 0x000088e0;
        public const int GL_VERTEX_ARRAY_SIZE_EXT = 0x0000807a;
        public const int GL_RGB10_A2 = 0x00008059;
        public const int GL_CURRENT_RASTER_TEXTURE_COORDS = 0x00000b06;
        public const int GL_MAX_VERTEX_UNITS_ARB = 0x000086a4;
        public const int GL_MODELVIEW21_ARB = 0x00008735;
        public const int GL_MATERIAL_SIDE_HINT_PGI = 0x0001a22c;
        public const int GL_FULL_STIPPLE_HINT_PGI = 0x0001a219;
        public const int GL_FENCE_CONDITION_NV = 0x000084f4;
        public const int GL_DOT_PRODUCT_TEXTURE_3D_NV = 0x000086ef;
        public const int GL_SHADER_SOURCE_LENGTH = 0x00008b88;
        public const int GL_MATRIX20_ARB = 0x000088d4;
        public const int GL_LINEAR_CLIPMAP_LINEAR_SGIX = 0x00008170;
        public const int GL_SAMPLE_COVERAGE_VALUE_ARB = 0x000080aa;
        public const int GL_MAP1_VERTEX_ATTRIB6_4_NV = 0x00008666;
        public const int GL_PROGRAM_UNDER_NATIVE_LIMITS_ARB = 0x000088b6;
        public const int GL_DOT3_RGBA_EXT = 0x00008741;
        public const int GL_UNSIGNED_SHORT_4_4_4_4_REV = 0x00008365;
        public const int GL_UNPACK_ROW_LENGTH = 0x00000cf2;
        public const int GL_EVAL_VERTEX_ATTRIB0_NV = 0x000086c6;
        public const int GL_RGBA_S3TC = 0x000083a2;
        public const int GL_SGIX_fragment_lighting = 0x00000001;
        public const int GL_ALPHA12 = 0x0000803d;
        public const int GL_ALPHA16 = 0x0000803e;
        public const int GL_CURRENT_MATRIX_ARB = 0x00008641;
        public const int GL_LOWER_LEFT = 0x00008ca1;
        public const int GL_TEXTURE30_ARB = 0x000084de;
        public const int GL_COPY_INVERTED = 0x0000150c;
        public const int GL_MAX_PROGRAM_ATTRIBS_ARB = 0x000088ad;
        public const int GL_BUMP_ROT_MATRIX_ATI = 0x00008775;
        public const int GL_CONSTANT_COLOR = 0x00008001;
        public const int GL_DETAIL_TEXTURE_LEVEL_SGIS = 0x0000809a;
        public const int GL_DUAL_ALPHA4_SGIS = 0x00008110;
        public const int GL_MAX_TEXTURE_UNITS_ARB = 0x000084e2;
        public const int GL_CURRENT_BIT = 0x00000001;
        public const int GL_COMBINER_CD_OUTPUT_NV = 0x0000854b;
        public const int GL_EVAL_VERTEX_ATTRIB15_NV = 0x000086d5;
        public const int GL_HALF_BIAS_NEGATE_NV = 0x0000853b;
        public const int GL_SPOT_EXPONENT = 0x00001205;
        public const int GL_MODULATE_ADD_ATI = 0x00008744;
        public const int GL_BLEND_DST_ALPHA_EXT = 0x000080ca;
        public const int GL_INDEX_BITS = 0x00000d51;
        public const int GL_HISTOGRAM_LUMINANCE_SIZE = 0x0000802c;
        public const int GL_UNSIGNED_BYTE_2_3_3_REV_EXT = 0x00008362;
        public const int GL_MATRIX28_ARB = 0x000088dc;
        public const int GL_ATTRIB_ARRAY_STRIDE_NV = 0x00008624;
        public const int GL_REFLECTION_MAP_EXT = 0x00008512;
        public const int GL_MAP1_VERTEX_3 = 0x00000d97;
        public const int GL_ALPHA = 0x00001906;
        public const int GL_HALF_FLOAT_ARB = 0x0000140b;
        public const int GL_POINT_FADE_THRESHOLD_SIZE_SGIS = 0x00008128;
        public const int GL_UNSIGNED_INT_8_8_8_8_REV_EXT = 0x00008367;
        public const int GL_FOG_INDEX = 0x00000b61;
        public const int GL_COMBINE_ALPHA = 0x00008572;
        public const int GL_QUAD_INTENSITY4_SGIS = 0x00008122;
        public const int GL_TEXTURE10_ARB = 0x000084ca;
        public const int GL_UNSIGNED_INT_8_8_8_8_REV = 0x00008367;
        public const int GL_PN_TRIANGLES_POINT_MODE_LINEAR_ATI = 0x000087f5;
        public const int GL_MINMAX_FORMAT_EXT = 0x0000802f;
        public const int GL_POLYGON_OFFSET_POINT = 0x00002a01;
        public const int GL_PN_TRIANGLES_ATI = 0x000087f0;
        public const int GL_MAT_DIFFUSE_BIT_PGI = 0x00400000;
        public const int GL_2PASS_0_SGIS = 0x000080a2;
        public const int GL_PIXEL_COUNTER_BITS_NV = 0x00008864;
        public const int GL_NORMAL_ARRAY_LIST_STRIDE_IBM = 0x000192a9;
        public const int GL_PIXEL_MAG_FILTER_EXT = 0x00008331;
        public const int GL_MAP2_VERTEX_ATTRIB10_4_NV = 0x0000867a;
        public const int GL_DETAIL_TEXTURE_2D_BINDING_SGIS = 0x00008096;
        public const int GL_SIGNED_LUMINANCE_NV = 0x00008701;
        public const int GL_STATIC_READ = 0x000088e5;
        public const int GL_WIDE_LINE_HINT_PGI = 0x0001a222;
        public const int GL_FEEDBACK = 0x00001c01;
        public const int GL_MAX_ELEMENTS_VERTICES_EXT = 0x000080e8;
        public const int GL_PIXEL_TEX_GEN_ALPHA_NO_REPLACE_SGIX = 0x00008188;
        public const int GL_MAX_FRAGMENT_PROGRAM_LOCAL_PARAMETERS_NV = 0x00008868;
        public const int GL_ELEMENT_ARRAY_BUFFER_ARB = 0x00008893;
        public const int GL_SECONDARY_COLOR_ARRAY_STRIDE_EXT = 0x0000845c;
        public const int GL_FLOAT_RGB_NV = 0x00008882;
        public const int GL_MAP2_VERTEX_ATTRIB6_4_NV = 0x00008676;
        public const int GL_SAMPLE_MASK_EXT = 0x000080a0;
        public const int GL_TEXTURE_COORD_ARRAY_BUFFER_BINDING_ARB = 0x0000889a;
        public const int GL_COLOR_ATTACHMENT5_EXT = 0x00008ce5;
        public const int GL_DYNAMIC_COPY_ARB = 0x000088ea;
        public const int GL_SECONDARY_COLOR_NV = 0x0000852d;
        public const int GL_VERTEX_ARRAY_STORAGE_HINT_APPLE = 0x0000851f;
        public const int GL_MAX_TEXTURE_IMAGE_UNITS_NV = 0x00008872;
        public const int GL_IMAGE_SCALE_Y_HP = 0x00008156;
        public const int GL_4X_BIT_ATI = 0x00000002;
        public const int GL_ARB_imaging = 0x00000001;
        public const int GL_SGIS_detail_texture = 0x00000001;
        public const int GL_FRAGMENT_COLOR_EXT = 0x0000834c;
        public const int GL_POST_COLOR_MATRIX_GREEN_SCALE = 0x000080b5;
        public const int GL_PACK_SKIP_ROWS = 0x00000d03;
        public const int GL_UNSIGNED_INT_8_8_S8_S8_REV_NV = 0x000086db;
        public const int GL_LIGHT2 = 0x00004002;
        public const int GL_LIGHT3 = 0x00004003;
        public const int GL_LIGHT0 = 0x00004000;
        public const int GL_LIGHT1 = 0x00004001;
        public const int GL_LIGHT6 = 0x00004006;
        public const int GL_LIGHT7 = 0x00004007;
        public const int GL_LIGHT4 = 0x00004004;
        public const int GL_LIGHT5 = 0x00004005;
        public const int GL_MIRROR_CLAMP_TO_BORDER_EXT = 0x00008912;
        public const int GL_MAP1_TEXTURE_COORD_4 = 0x00000d96;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_Y_EXT = 0x00008517;
        public const int GL_COLOR_ARRAY_POINTER = 0x00008090;
        public const int GL_UNSIGNED_SHORT_4_4_4_4 = 0x00008033;
        public const int GL_RESAMPLE_REPLICATE_SGIX = 0x0000842e;
        public const int GL_MAP1_BINORMAL_EXT = 0x00008446;
        public const int GL_SRC_COLOR = 0x00000300;
        public const int GL_POST_COLOR_MATRIX_BLUE_SCALE = 0x000080b6;
        public const int GL_SIGNED_LUMINANCE_ALPHA_NV = 0x00008703;
        public const int GL_PRIMARY_COLOR_EXT = 0x00008577;
        public const int GL_PACK_RESAMPLE_SGIX = 0x0000842c;
        public const int GL_PIXEL_MAP_I_TO_R_SIZE = 0x00000cb2;
        public const int GL_CURRENT_PROGRAM = 0x00008b8d;
        public const int GL_FRAGMENT_COLOR_MATERIAL_SGIX = 0x00008401;
        public const int GL_POSITION = 0x00001203;
        public const int GL_POST_COLOR_MATRIX_BLUE_BIAS = 0x000080ba;
        public const int GL_LOAD = 0x00000101;
        public const int GL_FOG_END = 0x00000b64;
        public const int GL_FOG_SPECULAR_TEXTURE_WIN = 0x000080ec;
        public const int GL_TEXTURE_BORDER = 0x00001005;
        public const int GL_TEXTURE17_ARB = 0x000084d1;
        public const int GL_X_EXT = 0x000087d5;
        public const int GL_TEXTURE3 = 0x000084c3;
        public const int GL_TEXTURE2 = 0x000084c2;
        public const int GL_TEXTURE1 = 0x000084c1;
        public const int GL_TEXTURE0 = 0x000084c0;
        public const int GL_TEXTURE7 = 0x000084c7;
        public const int GL_TEXTURE6 = 0x000084c6;
        public const int GL_TEXTURE5 = 0x000084c5;
        public const int GL_TEXTURE4 = 0x000084c4;
        public const int GL_MAP1_VERTEX_ATTRIB14_4_NV = 0x0000866e;
        public const int GL_TEXTURE9 = 0x000084c9;
        public const int GL_TEXTURE8 = 0x000084c8;
        public const int GL_MAP2_BINORMAL_EXT = 0x00008447;
        public const int GL_DRAW_BUFFER12_ARB = 0x00008831;
        public const int GL_MAX_PROGRAM_ENV_PARAMETERS_ARB = 0x000088b5;
        public const int GL_OP_CLAMP_EXT = 0x0000878e;
        public const int GL_SAMPLES_PASSED_ARB = 0x00008914;
        public const int GL_DECR_WRAP_EXT = 0x00008508;
        public const int GL_MATRIX19_ARB = 0x000088d3;
        public const int GL_ATTACHED_SHADERS = 0x00008b85;
        public const int GL_R1UI_N3F_V3F_SUN = 0x000085c7;
        public const int GL_MAP1_VERTEX_ATTRIB12_4_NV = 0x0000866c;
        public const int GL_CLIENT_ACTIVE_TEXTURE_ARB = 0x000084e1;
        public const int GL_FRAMEBUFFER_BINDING_EXT = 0x00008ca6;
        public const int GL_BINORMAL_ARRAY_EXT = 0x0000843a;
        public const int GL_FOG_DISTANCE_MODE_NV = 0x0000855a;
        public const int GL_SPRITE_AXIS_SGIX = 0x0000814a;
        public const int GL_2PASS_1_EXT = 0x000080a3;
        public const int GL_RED = 0x00001903;
        public const int GL_COLOR_ATTACHMENT4_EXT = 0x00008ce4;
        public const int GL_NORMAL_ARRAY_BUFFER_BINDING = 0x00008897;
        public const int GL_MODELVIEW17_ARB = 0x00008731;
        public const int GL_MAX_VERTEX_SHADER_LOCAL_CONSTANTS_EXT = 0x000087c8;
        public const int GL_QUERY_RESULT_AVAILABLE_ARB = 0x00008867;
        public const int GL_OP_SUB_EXT = 0x00008796;
        public const int GL_RENDERBUFFER_BINDING_EXT = 0x00008ca7;
        public const int GL_WEIGHT_ARRAY_STRIDE_ARB = 0x000086aa;
        public const int GL_FUNC_ADD = 0x00008006;
        public const int GL_MODELVIEW6_ARB = 0x00008726;
        public const int GL_LEFT = 0x00000406;
        public const int GL_ATTENUATION_EXT = 0x0000834d;
        public const int GL_RGB5_A1 = 0x00008057;
        public const int GL_ALLOW_DRAW_OBJ_HINT_PGI = 0x0001a20e;
        public const int GL_FRAGMENT_SHADER_DERIVATIVE_HINT_ARB = 0x00008b8b;
        public const int GL_BLEND_DST_RGB = 0x000080c8;
        public const int GL_FLOAT_R_NV = 0x00008880;
        public const int GL_MAX_PROGRAM_NATIVE_TEX_INDIRECTIONS_ARB = 0x00008810;
        public const int GL_OPERAND1_RGB_ARB = 0x00008591;
        public const int GL_MAX_PALETTE_MATRICES_ARB = 0x00008842;
        public const int GL_CULL_FRAGMENT_NV = 0x000086e7;
        public const int GL_COLOR_TABLE_BLUE_SIZE = 0x000080dc;
        public const int GL_INTENSITY32F_ARB = 0x00008817;
        public const int GL_CURRENT_BINORMAL_EXT = 0x0000843c;
        public const int GL_VERTEX_STREAM0_ATI = 0x0000876c;
        public const int GL_STREAM_READ_ARB = 0x000088e1;
        public const int GL_TEXTURE_BLUE_TYPE_ARB = 0x00008c12;
        public const int GL_LOCAL_CONSTANT_DATATYPE_EXT = 0x000087ed;
        public const int GL_MAGNITUDE_SCALE_NV = 0x00008712;
        public const int GL_INVERTED_SCREEN_W_REND = 0x00008491;
        public const int GL_CMYK_EXT = 0x0000800c;
        public const int GL_MAX_VERTEX_TEXTURE_IMAGE_UNITS = 0x00008b4c;
        public const int GL_OP_MUL_EXT = 0x00008786;
        public const int GL_SAMPLE_BUFFERS_3DFX = 0x000086b3;
        public const int GL_SIGNED_RGB8_NV = 0x000086ff;
        public const int GL_MAX_ELEMENTS_VERTICES = 0x000080e8;
        public const int GL_SIGNED_INTENSITY8_NV = 0x00008708;
        public const int GL_DYNAMIC_DRAW = 0x000088e8;
        public const int GL_GREEN_BIT_ATI = 0x00000002;
        public const int GL_UNPACK_CMYK_HINT_EXT = 0x0000800f;
        public const int GL_REG_19_ATI = 0x00008934;
        public const int GL_SHADER_TYPE = 0x00008b4f;
        public const int GL_OPERAND2_RGB = 0x00008592;
        public const int GL_TEXTURE_DEFORMATION_SGIX = 0x00008195;
        public const int GL_FRAMEBUFFER_ATTACHMENT_TEXTURE_LEVEL_EXT = 0x00008cd2;
        public const int GL_SHADING_LANGUAGE_VERSION = 0x00008b8c;
        public const int GL_INCR = 0x00001e02;
        public const int GL_UNPACK_IMAGE_HEIGHT = 0x0000806e;
        public const int GL_TEXCOORD1_BIT_PGI = 0x10000000;
        public const int GL_TEXTURE_COMPRESSED = 0x000086a1;
        public const int GL_SOURCE2_RGB = 0x00008582;
        public const int GL_NORMALIZED_RANGE_EXT = 0x000087e0;
        public const int GL_ACCUM_BUFFER_BIT = 0x00000200;
        public const int GL_SRC_ALPHA_SATURATE = 0x00000308;
        public const int GL_VERTEX_WEIGHT_ARRAY_SIZE_EXT = 0x0000850d;
        public const int GL_COMBINER_MAPPING_NV = 0x00008543;
        public const int GL_HISTOGRAM_LUMINANCE_SIZE_EXT = 0x0000802c;
        public const int GL_POLYGON = 0x00000009;
        public const int GL_TEXTURE_COORD_ARRAY_SIZE_EXT = 0x00008088;
        public const int GL_4D_COLOR_TEXTURE = 0x00000604;
        public const int GL_CON_0_ATI = 0x00008941;
        public const int GL_VERTEX_ARRAY_RANGE_VALID_NV = 0x0000851f;
        public const int GL_COLOR_INDEX4_EXT = 0x000080e4;
        public const int GL_EVAL_VERTEX_ATTRIB14_NV = 0x000086d4;
        public const int GL_TABLE_TOO_LARGE_EXT = 0x00008031;
        public const int GL_ARRAY_OBJECT_OFFSET_ATI = 0x00008767;
        public const int GL_SWIZZLE_STQ_ATI = 0x00008977;
        public const int GL_TEXTURE_DEPTH_TYPE_ARB = 0x00008c16;
        public const int GL_UNSIGNED_SHORT_1_5_5_5_REV_EXT = 0x00008366;
        public const int GL_T2F_N3F_V3F = 0x00002a2b;
        public const int GL_QUAD_ALPHA4_SGIS = 0x0000811e;
        public const int GL_OUTPUT_TEXTURE_COORD27_EXT = 0x000087b8;
        public const int GL_OUTPUT_TEXTURE_COORD16_EXT = 0x000087ad;
        public const int GL_TEXTURE_ALPHA_TYPE_ARB = 0x00008c13;
        public const int GL_TEXTURE_COMPONENTS = 0x00001003;
        public const int GL_LINE_STRIP = 0x00000003;
        public const int GL_EXT_texture3D = 0x00000001;
        public const int GL_LUMINANCE16F_ARB = 0x0000881e;
        public const int GL_WEIGHT_ARRAY_POINTER_ARB = 0x000086ac;
        public const int GL_CLIENT_ATTRIB_STACK_DEPTH = 0x00000bb1;
        public const int GL_MATRIX10_ARB = 0x000088ca;
        public const int GL_SGIX_pixel_tiles = 0x00000001;
        public const int GL_SEPARATE_SPECULAR_COLOR_EXT = 0x000081fa;
        public const int GL_DISCARD_NV = 0x00008530;
        public const int GL_MAX_CLIENT_ATTRIB_STACK_DEPTH = 0x00000d3b;
        public const int GL_ACCUM_ALPHA_BITS = 0x00000d5b;
        public const int GL_MATRIX16_ARB = 0x000088d0;
        public const int GL_MIN = 0x00008007;
        public const int GL_STENCIL_BACK_PASS_DEPTH_FAIL = 0x00008802;
        public const int GL_COLOR_MATRIX_STACK_DEPTH = 0x000080b2;
        public const int GL_EXT_rescale_normal = 0x00000001;
        public const int GL_SIGNED_IDENTITY_NV = 0x0000853c;
        public const int GL_COMBINE = 0x00008570;
        public const int GL_LINE = 0x00001b01;
        public const int GL_POLYGON_OFFSET_BIAS_EXT = 0x00008039;
        public const int GL_FRAGMENT_LIGHT4_SGIX = 0x00008410;
        public const int GL_DEPTH_PASS_INSTRUMENT_SGIX = 0x00008310;
        public const int GL_QUAD_LUMINANCE8_SGIS = 0x00008121;
        public const int GL_SOURCE1_RGB = 0x00008581;
        public const int GL_ALWAYS_SOFT_HINT_PGI = 0x0001a20d;
        public const int GL_VERTEX_ATTRIB_ARRAY_ENABLED = 0x00008622;
        public const int GL_MAP2_TANGENT_EXT = 0x00008445;
        public const int GL_T4F_V4F = 0x00002a28;
        public const int GL_OFFSET_TEXTURE_BIAS_NV = 0x000086e3;
        public const int GL_ONE_MINUS_CONSTANT_COLOR_EXT = 0x00008002;
        public const int GL_OP_DOT3_EXT = 0x00008784;
        public const int GL_DRAW_BUFFER6_ATI = 0x0000882b;
        public const int GL_PROGRAM_ERROR_STRING_ARB = 0x00008874;
        public const int GL_POST_CONVOLUTION_RED_SCALE = 0x0000801c;
        public const int GL_EXT_histogram = 0x00000001;
        public const int GL_TANGENT_ARRAY_STRIDE_EXT = 0x0000843f;
        public const int GL_COMBINER_AB_OUTPUT_NV = 0x0000854a;
        public const int GL_PROXY_COLOR_TABLE_SGI = 0x000080d3;
        public const int GL_2D = 0x00000600;
        public const int GL_RGB16F_ARB = 0x0000881b;
        public const int GL_REG_22_ATI = 0x00008937;
        public const int GL_N3F_V3F = 0x00002a25;
        public const int GL_HISTOGRAM_FORMAT = 0x00008027;
        public const int GL_MODELVIEW18_ARB = 0x00008732;
        public const int GL_YCBCR_422_APPLE = 0x000085b9;
        public const int GL_SELECTION_BUFFER_SIZE = 0x00000df4;
        public const int GL_VERTEX_WEIGHT_ARRAY_STRIDE_EXT = 0x0000850f;
        public const int GL_COLOR_ATTACHMENT0_EXT = 0x00008ce0;
        public const int GL_VERTEX_ATTRIB_ARRAY_NORMALIZED_ARB = 0x0000886a;
        public const int GL_MAX_ELEMENTS_INDICES = 0x000080e9;
        public const int GL_DSDT8_MAG8_NV = 0x0000870a;
        public const int GL_OBJECT_ACTIVE_UNIFORMS_ARB = 0x00008b86;
        public const int GL_RGBA12 = 0x0000805a;
        public const int GL_RGBA16 = 0x0000805b;
        public const int GL_CONVOLUTION_HINT_SGIX = 0x00008316;
        public const int GL_INDEX_SHIFT = 0x00000d12;
        public const int GL_IMPLEMENTATION_COLOR_READ_FORMAT_OES = 0x00008b9b;
        public const int GL_STENCIL_BACK_PASS_DEPTH_PASS_ATI = 0x00008803;
        public const int GL_SAMPLE_PATTERN_SGIS = 0x000080ac;
        public const int GL_SGIX_list_priority = 0x00000001;
        public const int GL_PROGRAM_NATIVE_TEMPORARIES_ARB = 0x000088a6;
        public const int GL_RGBA_MODE = 0x00000c31;
        public const int GL_GREEN_BIAS = 0x00000d19;
        public const int GL_RGBA4_EXT = 0x00008056;
        public const int GL_CON_22_ATI = 0x00008957;
        public const int GL_COLOR_TABLE_ALPHA_SIZE = 0x000080dd;
        public const int GL_SPECULAR = 0x00001202;
        public const int GL_COMPRESSED_LUMINANCE_ALPHA = 0x000084eb;
        public const int GL_BUFFER_SIZE_ARB = 0x00008764;
        public const int GL_UNSIGNED_SHORT_8_8_MESA = 0x000085ba;
        public const int GL_MAX_VERTEX_UNIFORM_COMPONENTS_ARB = 0x00008b4a;
        public const int GL_CON_29_ATI = 0x0000895e;
        public const int GL_OP_MOV_EXT = 0x00008799;
        public const int GL_OBJECT_VALIDATE_STATUS_ARB = 0x00008b83;
        public const int GL_TEXTURE19_ARB = 0x000084d3;
        public const int GL_RESAMPLE_DECIMATE_OML = 0x00008989;
        public const int GL_DRAW_BUFFER3 = 0x00008828;
        public const int GL_LINE_BIT = 0x00000004;
        public const int GL_MODELVIEW26_ARB = 0x0000873a;
        public const int GL_INTENSITY_FLOAT16_ATI = 0x0000881d;
        public const int GL_PIXEL_MAP_I_TO_A_SIZE = 0x00000cb5;
        public const int GL_FRAGMENT_DEPTH = 0x00008452;
        public const int GL_UNPACK_SKIP_IMAGES = 0x0000806d;
        public const int GL_SOURCE3_RGB_NV = 0x00008583;
        public const int GL_COLOR_TABLE_FORMAT_SGI = 0x000080d8;
        public const int GL_MAX_PROGRAM_CALL_DEPTH_NV = 0x000088f5;
        public const int GL_MAT_EMISSION_BIT_PGI = 0x00800000;
        public const int GL_VERTEX_ARRAY_TYPE = 0x0000807b;
        public const int GL_RGBA_UNSIGNED_DOT_PRODUCT_MAPPING_NV = 0x000086d9;
        public const int GL_SGIX_ycrcb = 0x00000001;
        public const int GL_TEXTURE_CONSTANT_DATA_SUNX = 0x000081d6;
        public const int GL_POST_CONVOLUTION_BLUE_SCALE = 0x0000801e;
        public const int GL_LINEAR_SHARPEN_COLOR_SGIS = 0x000080af;
        public const int GL_DOUBLE_EXT = 0x0000140a;
        public const int GL_STENCIL_TEST = 0x00000b90;
        public const int GL_SIGNED_ALPHA_NV = 0x00008705;
        public const int GL_SAMPLE_BUFFERS_EXT = 0x000080a8;
        public const int GL_DUAL_LUMINANCE8_SGIS = 0x00008115;
        public const int GL_FASTEST = 0x00001101;
        public const int GL_DISCARD_ATI = 0x00008763;
        public const int GL_RGBA16_EXT = 0x0000805b;
        public const int GL_VERTEX_DATA_HINT_PGI = 0x0001a22a;
        public const int GL_RASTER_POSITION_UNCLIPPED_IBM = 0x00019262;
        public const int GL_EXT_texture_object = 0x00000001;
        public const int GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS_ARB = 0x00008b4d;
        public const int GL_TEXTURE_GEN_MODE = 0x00002500;
        public const int GL_VERTEX_ATTRIB_ARRAY_SIZE = 0x00008623;
        public const int GL_MODULATE_SUBTRACT_ATI = 0x00008746;
        public const int GL_TEXT_FRAGMENT_SHADER_ATI = 0x00008200;
        public const int GL_CON_12_ATI = 0x0000894d;
        public const int GL_FOG_OFFSET_SGIX = 0x00008198;
        public const int GL_SIGNED_HILO8_NV = 0x0000885f;
        public const int GL_FLOAT_RGBA_MODE_NV = 0x0000888e;
        public const int GL_COLOR_TABLE_ALPHA_SIZE_SGI = 0x000080dd;
        public const int GL_CON_2_ATI = 0x00008943;
        public const int GL_PIXEL_FRAGMENT_ALPHA_SOURCE_SGIS = 0x00008355;
        public const int GL_GREEN_MIN_CLAMP_INGR = 0x00008561;
        public const int GL_MAX_DRAW_BUFFERS = 0x00008824;
        public const int GL_FOG_COORDINATE_ARRAY_TYPE_EXT = 0x00008454;
        public const int GL_OP_MULTIPLY_MATRIX_EXT = 0x00008798;
        public const int GL_OUTPUT_FOG_EXT = 0x000087bd;
        public const int GL_SGIX_reference_plane = 0x00000001;
        public const int GL_SGIX_async_pixel = 0x00000001;
        public const int GL_PROGRAM_TEX_INDIRECTIONS_ARB = 0x00008807;
        public const int GL_DEPTH_BUFFER_BIT = 0x00000100;
        public const int GL_R1UI_V3F_SUN = 0x000085c4;
        public const int GL_VERTEX_PROGRAM_POINT_SIZE_ARB = 0x00008642;
        public const int GL_CONSTANT_ALPHA_EXT = 0x00008003;
        public const int GL_MAX_PROGRAM_TEMPORARIES_ARB = 0x000088a5;
        public const int GL_RGBA2 = 0x00008055;
        public const int GL_RGBA4 = 0x00008056;
        public const int GL_NAME_STACK_DEPTH = 0x00000d70;
        public const int GL_RGBA8 = 0x00008058;
        public const int GL_OBJECT_DISTANCE_TO_POINT_SGIS = 0x000081f1;
        public const int GL_POLYGON_SMOOTH = 0x00000b41;
        public const int GL_PN_TRIANGLES_NORMAL_MODE_ATI = 0x000087f3;
        public const int GL_SECONDARY_COLOR_ARRAY_LIST_STRIDE_IBM = 0x000192af;
        public const int GL_EDGE_FLAG_ARRAY_COUNT_EXT = 0x0000808d;
        public const int GL_REPLACEMENT_CODE_ARRAY_STRIDE_SUN = 0x000085c2;
        public const int GL_DRAW_BUFFER5_ATI = 0x0000882a;
        public const int GL_LOGIC_OP = 0x00000bf1;
        public const int GL_INDEX_ARRAY_TYPE_EXT = 0x00008085;
        public const int GL_4_BYTES = 0x00001409;
        public const int GL_TEXTURE_COORD_ARRAY_LIST_STRIDE_IBM = 0x000192ac;
        public const int GL_TEXTURE15_ARB = 0x000084cf;
        public const int GL_PN_TRIANGLES_TESSELATION_LEVEL_ATI = 0x000087f4;
        public const int GL_MAP2_VERTEX_ATTRIB2_4_NV = 0x00008672;
        public const int GL_POINT_SIZE_MIN = 0x00008126;
        public const int GL_T2F_IUI_V2F_EXT = 0x000081b1;
        public const int GL_FRAMEBUFFER_ATTACHMENT_OBJECT_NAME_EXT = 0x00008cd1;
        public const int GL_HILO_NV = 0x000086f4;
        public const int GL_COLOR_INDEX8_EXT = 0x000080e5;
        public const int GL_EDGE_FLAG_ARRAY_BUFFER_BINDING_ARB = 0x0000889b;
        public const int GL_SAMPLER_CUBE = 0x00008b60;
        public const int GL_DEPTH_STENCIL_TO_RGBA_NV = 0x0000886e;
        public const int GL_REPLACEMENT_CODE_ARRAY_TYPE_SUN = 0x000085c1;
        public const int GL_EXT_abgr = 0x00000001;
        public const int GL_UNPACK_LSB_FIRST = 0x00000cf1;
        public const int GL_SGIX_tag_sample_buffer = 0x00000001;
        public const int GL_DOUBLEBUFFER = 0x00000c32;
        public const int GL_YCRCB_422_SGIX = 0x000081bb;
        public const int GL_CON_9_ATI = 0x0000894a;
        public const int GL_FEEDBACK_BUFFER_POINTER = 0x00000df0;
        public const int GL_COMBINER_BIAS_NV = 0x00008549;
        public const int GL_LINE_RESET_TOKEN = 0x00000707;
        public const int GL_ARRAY_ELEMENT_LOCK_COUNT_EXT = 0x000081a9;
        public const int GL_HISTOGRAM = 0x00008024;
        public const int GL_COLOR_ARRAY_POINTER_EXT = 0x00008090;
        public const int GL_SGIX_fog_offset = 0x00000001;
        public const int GL_EVAL_VERTEX_ATTRIB5_NV = 0x000086cb;
        public const int GL_DS_SCALE_NV = 0x00008710;
        public const int GL_TEXTURE_BINDING_CUBE_MAP_ARB = 0x00008514;
        public const int GL_MAX_ASYNC_HISTOGRAM_SGIX = 0x0000832d;
        public const int GL_GENERATE_MIPMAP_HINT = 0x00008192;
        public const int GL_FUNC_REVERSE_SUBTRACT = 0x0000800b;
        public const int GL_MULTISAMPLE_3DFX = 0x000086b2;
        public const int GL_EXTENSIONS = 0x00001f03;
        public const int GL_OPERAND0_RGB = 0x00008590;
        public const int GL_INDEX_MODE = 0x00000c30;
        public const int GL_DST_ALPHA = 0x00000304;
        public const int GL_SGIS_sharpen_texture = 0x00000001;
        public const int GL_TEXTURE_DT_SIZE_NV = 0x0000871e;
        public const int GL_SAMPLE_BUFFERS_SGIS = 0x000080a8;
        public const int GL_REFLECTION_MAP_ARB = 0x00008512;
        public const int GL_OPERAND3_ALPHA_NV = 0x0000859b;
        public const int GL_DYNAMIC_DRAW_ARB = 0x000088e8;
        public const int GL_TEXTURE11_ARB = 0x000084cb;
        public const int GL_DRAW_BUFFER9_ARB = 0x0000882e;
        public const int GL_OP_MADD_EXT = 0x00008788;
        public const int GL_BACK = 0x00000405;
        public const int GL_FUNC_SUBTRACT_EXT = 0x0000800a;
        public const int GL_RGB_SCALE = 0x00008573;
        public const int GL_COLOR_CLEAR_VALUE = 0x00000c22;
        public const int GL_BACK_NORMALS_HINT_PGI = 0x0001a223;
        public const int GL_TEXTURE_RED_SIZE = 0x0000805c;
        public const int GL_DOT_PRODUCT_TEXTURE_RECTANGLE_NV = 0x0000864e;
        public const int GL_SAMPLES_ARB = 0x000080a9;
        public const int GL_PIXEL_PACK_BUFFER_ARB = 0x000088eb;
        public const int GL_COLOR_TABLE_LUMINANCE_SIZE_SGI = 0x000080de;
        public const int GL_PACK_INVERT_MESA = 0x00008758;
        public const int GL_LIGHT_MODEL_SPECULAR_VECTOR_APPLE = 0x000085b0;
        public const int GL_ALPHA_TEST = 0x00000bc0;
        public const int GL_TRANSPOSE_PROJECTION_MATRIX = 0x000084e4;
        public const int GL_CUBIC_EXT = 0x00008334;
        public const int GL_INCR_WRAP_EXT = 0x00008507;
        public const int GL_OPERAND0_RGB_EXT = 0x00008590;
        public const int GL_ACTIVE_VERTEX_UNITS_ARB = 0x000086a5;
        public const int GL_PROGRAM_TARGET_NV = 0x00008646;
        public const int GL_IMAGE_TRANSLATE_X_HP = 0x00008157;
        public const int GL_DRAW_BUFFER10_ARB = 0x0000882f;
        public const int GL_FRAMEBUFFER_UNSUPPORTED_EXT = 0x00008cdd;
        public const int GL_COLOR_TABLE_RED_SIZE = 0x000080da;
        public const int GL_SHORT = 0x00001402;
        public const int GL_SECONDARY_COLOR_ARRAY_SIZE_EXT = 0x0000845a;
        public const int GL_MUL_ATI = 0x00008964;
        public const int GL_UNSIGNED_SHORT_1_5_5_5_REV = 0x00008366;
        public const int GL_VERTEX_SHADER_OPTIMIZED_EXT = 0x000087d4;
        public const int GL_SRC1_ALPHA = 0x00008589;
        public const int GL_SOURCE2_RGB_EXT = 0x00008582;
        public const int GL_REG_1_ATI = 0x00008922;
        public const int GL_MAX_ACTIVE_LIGHTS_SGIX = 0x00008405;
        public const int GL_MODELVIEW13_ARB = 0x0000872d;
        public const int GL_DOT_PRODUCT_AFFINE_DEPTH_REPLACE_NV = 0x0000885d;
        public const int GL_DOT_PRODUCT_DIFFUSE_CUBE_MAP_NV = 0x000086f1;
        public const int GL_STENCIL_BACK_FUNC_ATI = 0x00008800;
        public const int GL_OP_CROSS_PRODUCT_EXT = 0x00008797;
        public const int GL_MAX_EXT = 0x00008008;
        public const int GL_TEXTURE_COORD_ARRAY_TYPE = 0x00008089;
        public const int GL_INTERLACE_SGIX = 0x00008094;
        public const int GL_PROXY_POST_CONVOLUTION_COLOR_TABLE = 0x000080d4;
        public const int GL_SGI_color_matrix = 0x00000001;
        public const int GL_WEIGHT_ARRAY_BUFFER_BINDING_ARB = 0x0000889e;
        public const int GL_CLIENT_ACTIVE_TEXTURE = 0x000084e1;
        public const int GL_LUMINANCE4_EXT = 0x0000803f;
        public const int GL_LIGHT_MODEL_COLOR_CONTROL_EXT = 0x000081f8;
        public const int GL_PIXEL_PACK_BUFFER_BINDING_EXT = 0x000088ed;
        public const int GL_CURRENT_FOG_COORDINATE_EXT = 0x00008453;
        public const int GL_SAMPLES_EXT = 0x000080a9;
        public const int GL_FOG_COORD_ARRAY = 0x00008457;
        public const int GL_SOURCE1_ALPHA_EXT = 0x00008589;
        public const int GL_COMPRESSED_RGB_ARB = 0x000084ed;
        public const int GL_POST_CONVOLUTION_RED_BIAS_EXT = 0x00008020;
        public const int GL_INVALID_OPERATION = 0x00000502;
        public const int GL_SRC0_ALPHA = 0x00008588;
        public const int GL_SECONDARY_COLOR_ARRAY_BUFFER_BINDING = 0x0000889c;
        public const int GL_FLOAT_RGB32_NV = 0x00008889;
        public const int GL_LUMINANCE_ALPHA16F_ARB = 0x0000881f;
        public const int GL_AND = 0x00001501;
        public const int GL_VERTEX_STREAM5_ATI = 0x00008771;
        public const int GL_MODELVIEW20_ARB = 0x00008734;
        public const int GL_POST_CONVOLUTION_COLOR_TABLE = 0x000080d1;
        public const int GL_DETAIL_TEXTURE_MODE_SGIS = 0x0000809b;
        public const int GL_NUM_FRAGMENT_CONSTANTS_ATI = 0x0000896f;
        public const int GL_VERTEX_SHADER = 0x00008b31;
        public const int GL_MAX_FRAMEZOOM_FACTOR_SGIX = 0x0000818d;
        public const int GL_WRITE_PIXEL_DATA_RANGE_NV = 0x00008878;
        public const int GL_PRIMARY_COLOR = 0x00008577;
        public const int GL_STATIC_COPY = 0x000088e6;
        public const int GL_SIGNED_RGB8_UNSIGNED_ALPHA8_NV = 0x0000870d;
        public const int GL_COORD_REPLACE = 0x00008862;
        public const int GL_TEXTURE23_ARB = 0x000084d7;
        public const int GL_BUFFER_ACCESS_ARB = 0x000088bb;
        public const int GL_TEXTURE9_ARB = 0x000084c9;
        public const int GL_SWIZZLE_STQ_DQ_ATI = 0x00008979;
        public const int GL_LINE_WIDTH_RANGE = 0x00000b22;
        public const int GL_COLOR_CLEAR_UNCLAMPED_VALUE_ATI = 0x00008835;
        public const int GL_LO_BIAS_NV = 0x00008715;
        public const int GL_COLOR_INDEX = 0x00001900;
        public const int GL_VERTEX_ARRAY_RANGE_LENGTH_NV = 0x0000851e;
        public const int GL_VERTEX23_BIT_PGI = 0x00000004;
        public const int GL_MAP1_VERTEX_ATTRIB8_4_NV = 0x00008668;
        public const int GL_STREAM_COPY = 0x000088e2;
        public const int GL_TEXTURE_COMPRESSION_HINT_ARB = 0x000084ef;
        public const int GL_BOOL_VEC4_ARB = 0x00008b59;
        public const int GL_MAX_VERTEX_STREAMS_ATI = 0x0000876b;
        public const int GL_VERTEX_SHADER_INSTRUCTIONS_EXT = 0x000087cf;
        public const int GL_PACK_RESAMPLE_OML = 0x00008984;
        public const int GL_RGB12_EXT = 0x00008053;
        public const int GL_COLOR_ARRAY_TYPE = 0x00008082;
        public const int GL_FRAMEBUFFER_INCOMPLETE_FORMATS_EXT = 0x00008cda;
        public const int GL_CONVOLUTION_2D = 0x00008011;
        public const int GL_POST_CONVOLUTION_ALPHA_BIAS = 0x00008023;
        public const int GL_422_EXT = 0x000080cc;
        public const int GL_LIST_PRIORITY_SGIX = 0x00008182;
        public const int GL_OP_FLOOR_EXT = 0x0000878f;
        public const int GL_REG_30_ATI = 0x0000893f;
        public const int GL_LUMINANCE16_ALPHA16 = 0x00008048;
        public const int GL_HILO8_NV = 0x0000885e;
        public const int GL_MAX_VERTEX_SHADER_LOCALS_EXT = 0x000087c9;
        public const int GL_INDEX_ARRAY_STRIDE = 0x00008086;
        public const int GL_BUFFER_USAGE = 0x00008765;
        public const int GL_BUMP_ENVMAP_ATI = 0x0000877b;
        public const int GL_FRAGMENT_LIGHT3_SGIX = 0x0000840f;
        public const int GL_POINT_SIZE_MAX_ARB = 0x00008127;
        public const int GL_SHADER_OPERATION_NV = 0x000086df;
        public const int GL_MAX_PROGRAM_ALU_INSTRUCTIONS_ARB = 0x0000880b;
        public const int GL_MAP1_COLOR_4 = 0x00000d90;
        public const int GL_TEXTURE_INTENSITY_SIZE_EXT = 0x00008061;
        public const int GL_FILTER4_SGIS = 0x00008146;
        public const int GL_EVAL_VERTEX_ATTRIB2_NV = 0x000086c8;
        public const int GL_STENCIL_BITS = 0x00000d57;
        public const int GL_DRAW_BUFFER9_ATI = 0x0000882e;
        public const int GL_EXT_convolution = 0x00000001;
        public const int GL_OUTPUT_TEXTURE_COORD10_EXT = 0x000087a7;
        public const int GL_CON_18_ATI = 0x00008953;
        public const int GL_PROXY_HISTOGRAM = 0x00008025;
        public const int GL_RENDERBUFFER_WIDTH_EXT = 0x00008d42;
        public const int GL_CULL_FACE_MODE = 0x00000b45;
        public const int GL_MAX_CLIPMAP_VIRTUAL_DEPTH_SGIX = 0x00008178;
        public const int GL_NORMAL_ARRAY_STRIDE_EXT = 0x0000807f;
        public const int GL_COLOR_MATRIX = 0x000080b1;
        public const int GL_SOURCE1_ALPHA = 0x00008589;
        public const int GL_TEXTURE_WRAP_R = 0x00008072;
        public const int GL_CLIENT_PIXEL_STORE_BIT = 0x00000001;
        public const int GL_MAX_CUBE_MAP_TEXTURE_SIZE_ARB = 0x0000851c;
        public const int GL_TEXTURE_WRAP_T = 0x00002803;
        public const int GL_CURRENT_MATRIX_NV = 0x00008641;
        public const int GL_SGIX_subsample = 0x00000001;
        public const int GL_MAP_TESSELLATION_NV = 0x000086c2;
        public const int GL_PACK_IMAGE_HEIGHT_EXT = 0x0000806c;
        public const int GL_PIXEL_TEX_GEN_ALPHA_REPLACE_SGIX = 0x00008187;
        public const int GL_UNPACK_CONSTANT_DATA_SUNX = 0x000081d5;
        public const int GL_SGIX_interlace = 0x00000001;
        public const int GL_HISTOGRAM_BLUE_SIZE = 0x0000802a;
        public const int GL_TEXTURE_TOO_LARGE_EXT = 0x00008065;
        public const int GL_MATRIX2_NV = 0x00008632;
        public const int GL_CUBIC_HP = 0x0000815f;
        public const int GL_ZOOM_Y = 0x00000d17;
        public const int GL_TEXTURE_GREEN_TYPE_ARB = 0x00008c11;
        public const int GL_SOURCE0_RGB_ARB = 0x00008580;
        public const int GL_MAX_CONVOLUTION_WIDTH = 0x0000801a;
        public const int GL_UNSIGNED_INT_8_8_8_8 = 0x00008035;
        public const int GL_POINT_SPRITE_NV = 0x00008861;
        public const int GL_OUTPUT_TEXTURE_COORD31_EXT = 0x000087bc;
        public const int GL_POST_COLOR_MATRIX_ALPHA_BIAS = 0x000080bb;
        public const int GL_CULL_MODES_NV = 0x000086e0;
        public const int GL_MATRIX0_NV = 0x00008630;
        public const int GL_TEXTURE_COORD_ARRAY_COUNT_EXT = 0x0000808b;
        public const int GL_FRAMEBUFFER_INCOMPLETE_DUPLICATE_ATTACHMENT_EXT = 0x00008cd8;
        public const int GL_RESAMPLE_DECIMATE_SGIX = 0x00008430;
        public const int GL_COLOR_ATTACHMENT12_EXT = 0x00008cec;
        public const int GL_UNSIGNED_INT = 0x00001405;
        public const int GL_DEPTH_CLEAR_VALUE = 0x00000b73;
        public const int GL_PIXEL_MAP_R_TO_R_SIZE = 0x00000cb6;
        public const int GL_MAP1_VERTEX_ATTRIB7_4_NV = 0x00008667;
        public const int GL_FLOAT_MAT4_ARB = 0x00008b5c;
        public const int GL_UNPACK_SUBSAMPLE_RATE_SGIX = 0x000085a1;
        public const int GL_DSDT_MAG_INTENSITY_NV = 0x000086dc;
        public const int GL_YCRCBA_SGIX = 0x00008319;
        public const int GL_RESTART_SUN = 0x00000001;
        public const int GL_TEXTURE_COORD_ARRAY_STRIDE = 0x0000808a;
        public const int GL_FRAGMENT_LIGHT_MODEL_TWO_SIDE_SGIX = 0x00008409;
        public const int GL_TEXTURE_BINDING_RECTANGLE_ARB = 0x000084f6;
        public const int GL_OUTPUT_TEXTURE_COORD6_EXT = 0x000087a3;
        public const int GL_CONVOLUTION_FORMAT_EXT = 0x00008017;
        public const int GL_INDEX_TEST_EXT = 0x000081b5;
        public const int GL_MODELVIEW_PROJECTION_NV = 0x00008629;
        public const int GL_OUTPUT_TEXTURE_COORD19_EXT = 0x000087b0;
        public const int GL_RENDER = 0x00001c00;
        public const int GL_MODELVIEW_STACK_DEPTH = 0x00000ba3;
        public const int GL_POINT_BIT = 0x00000002;
        public const int GL_ADD_SIGNED_EXT = 0x00008574;
        public const int GL_FRAGMENT_LIGHT_MODEL_NORMAL_INTERPOLATION_SGIX = 0x0000840b;
        public const int GL_COMBINER_SUM_OUTPUT_NV = 0x0000854c;
        public const int GL_COLOR_TABLE_SCALE_SGI = 0x000080d6;
        public const int GL_VERTEX_ARRAY_LIST_IBM = 0x0001929e;
        public const int GL_SPARE0_NV = 0x0000852e;
        public const int GL_DRAW_BUFFER8_ATI = 0x0000882d;
        public const int GL_FOG_COORDINATE_ARRAY_POINTER = 0x00008456;
        public const int GL_FEEDBACK_BUFFER_SIZE = 0x00000df1;
        public const int GL_INDEX_ARRAY_EXT = 0x00008077;
        public const int GL_PIXEL_TEX_GEN_SGIX = 0x00008139;
        public const int GL_PIXEL_COUNT_NV = 0x00008866;
        public const int GL_SOURCE0_RGB_EXT = 0x00008580;
        public const int GL_BLUE_MAX_CLAMP_INGR = 0x00008566;
        public const int GL_SECONDARY_COLOR_ARRAY_TYPE = 0x0000845b;
        public const int GL_CONSERVE_MEMORY_HINT_PGI = 0x0001a1fd;
        public const int GL_MODELVIEW4_ARB = 0x00008724;
        public const int GL_FOG_COORDINATE_ARRAY_POINTER_EXT = 0x00008456;
        public const int GL_CURRENT_RASTER_DISTANCE = 0x00000b09;
        public const int GL_CLIP_NEAR_HINT_PGI = 0x0001a220;
        public const int GL_TEXTURE_CLIPMAP_LOD_OFFSET_SGIX = 0x00008175;
        public const int GL_COPY_PIXEL_TOKEN = 0x00000706;
        public const int GL_DEPTH_TEXTURE_MODE = 0x0000884b;
        public const int GL_ARRAY_BUFFER_BINDING = 0x00008894;
        public const int GL_MATRIX12_ARB = 0x000088cc;
        public const int GL_POINT_SMOOTH = 0x00000b10;
        public const int GL_OUTPUT_TEXTURE_COORD24_EXT = 0x000087b5;
        public const int GL_LOCAL_CONSTANT_VALUE_EXT = 0x000087ec;
        public const int GL_GEOMETRY_DEFORMATION_BIT_SGIX = 0x00000002;
        public const int GL_OP_POWER_EXT = 0x00008793;
        public const int GL_MAX_PROGRAM_NATIVE_ALU_INSTRUCTIONS_ARB = 0x0000880e;
        public const int GL_MAX_PROGRAM_NATIVE_INSTRUCTIONS_ARB = 0x000088a3;
        public const int GL_PERTURB_EXT = 0x000085ae;
        public const int GL_ADD_ATI = 0x00008963;
        public const int GL_STENCIL_INDEX_EXT = 0x00008d45;
        public const int GL_STEREO = 0x00000c33;
        public const int GL_PIXEL_MAP_G_TO_G_SIZE = 0x00000cb7;
        public const int GL_MULTISAMPLE_ARB = 0x0000809d;
        public const int GL_TEXTURE_CUBE_MAP = 0x00008513;
        public const int GL_REG_2_ATI = 0x00008923;
        public const int GL_TEXTURE13_ARB = 0x000084cd;
        public const int GL_ELEMENT_ARRAY_BUFFER_BINDING_ARB = 0x00008895;
        public const int GL_EDGE_FLAG_ARRAY_EXT = 0x00008079;
        public const int GL_OR = 0x00001507;
        public const int GL_UNSIGNED_BYTE = 0x00001401;
        public const int GL_DSDT8_NV = 0x00008709;
        public const int GL_ELEMENT_ARRAY_POINTER_ATI = 0x0000876a;
        public const int GL_COLOR_ALPHA_PAIRING_ATI = 0x00008975;
        public const int GL_QUAD_STRIP = 0x00000008;
        public const int GL_SEPARATE_SPECULAR_COLOR = 0x000081fa;
        public const int GL_COLOR_TABLE_INTENSITY_SIZE_SGI = 0x000080df;
        public const int GL_ASYNC_HISTOGRAM_SGIX = 0x0000832c;
        public const int GL_MAX_VARYING_FLOATS_ARB = 0x00008b4b;
        public const int GL_VERTEX_ARRAY_POINTER = 0x0000808e;
        public const int GL_MODELVIEW1_ARB = 0x0000850a;
        public const int GL_MATRIX_INDEX_ARRAY_STRIDE_ARB = 0x00008848;
        public const int GL_INDEX_ARRAY_BUFFER_BINDING = 0x00008899;
        public const int GL_STORAGE_SHARED_APPLE = 0x000085bf;
        public const int GL_IMAGE_ROTATE_ORIGIN_X_HP = 0x0000815a;
        public const int GL_COLOR_TABLE_BIAS_SGI = 0x000080d7;
        public const int GL_POINT_SPRITE_R_MODE_NV = 0x00008863;
        public const int GL_ALPHA_TEST_REF = 0x00000bc2;
        public const int GL_COMPRESSED_LUMINANCE = 0x000084ea;
        public const int GL_PIXEL_MAP_B_TO_B_SIZE = 0x00000cb8;
        public const int GL_MAX_MATRIX_PALETTE_STACK_DEPTH_ARB = 0x00008841;
        public const int GL_MATRIX6_NV = 0x00008636;
        public const int GL_UNSIGNED_INT_10_10_10_2_EXT = 0x00008036;
        public const int GL_TEXTURE_LOD_BIAS_R_SGIX = 0x00008190;
        public const int GL_OBJECT_COMPILE_STATUS_ARB = 0x00008b81;
        public const int GL_TEXTURE_NORMAL_EXT = 0x000085af;
        public const int GL_VERTEX_SOURCE_ATI = 0x00008774;
        public const int GL_CLIP_VOLUME_CLIPPING_HINT_EXT = 0x000080f0;
        public const int GL_POLYGON_OFFSET_FACTOR_EXT = 0x00008038;
        public const int GL_MODELVIEW11_ARB = 0x0000872b;
        public const int GL_MAP1_VERTEX_ATTRIB15_4_NV = 0x0000866f;
        public const int GL_DRAW_BUFFER2_ARB = 0x00008827;
        public const int GL_DUAL_INTENSITY8_SGIS = 0x00008119;
        public const int GL_DEPTH_WRITEMASK = 0x00000b72;
        public const int GL_COLOR_TABLE_GREEN_SIZE = 0x000080db;
        public const int GL_FLOAT_VEC2 = 0x00008b50;
        public const int GL_QUADS = 0x00000007;
        public const int GL_TEXTURE_CLIPMAP_FRAME_SGIX = 0x00008172;
        public const int GL_COMBINE_ALPHA_ARB = 0x00008572;
        public const int GL_DOT3_RGB_ARB = 0x000086ae;
        public const int GL_OFFSET_TEXTURE_2D_SCALE_NV = 0x000086e2;
        public const int GL_CURRENT_MATRIX_STACK_DEPTH_NV = 0x00008640;
        public const int GL_MAX_PROGRAM_TEX_INDIRECTIONS_ARB = 0x0000880d;
        public const int GL_FRAMEBUFFER_ATTACHMENT_TEXTURE_3D_ZOFFSET_EXT = 0x00008cd4;
        public const int GL_BLEND_COLOR = 0x00008005;
        public const int GL_FRAGMENT_DEPTH_EXT = 0x00008452;
        public const int GL_TEXTURE_WRAP_R_EXT = 0x00008072;
        public const int GL_ELEMENT_ARRAY_ATI = 0x00008768;
        public const int GL_CON_16_ATI = 0x00008951;
        public const int GL_MODELVIEW24_ARB = 0x00008738;
        public const int GL_OPERAND1_ALPHA_ARB = 0x00008599;
        public const int GL_TEXTURE_PRIORITY_EXT = 0x00008066;
        public const int GL_OBJECT_SHADER_SOURCE_LENGTH_ARB = 0x00008b88;
        public const int GL_4PASS_1_EXT = 0x000080a5;
        public const int GL_CONVOLUTION_2D_EXT = 0x00008011;
        public const int GL_CON_11_ATI = 0x0000894c;
        public const int GL_SGIX_texture_coordinate_clamp = 0x00000001;
        public const int GL_422_REV_AVERAGE_EXT = 0x000080cf;
        public const int GL_TRANSPOSE_COLOR_MATRIX = 0x000084e6;
        public const int GL_VERTEX_PROGRAM_TWO_SIDE = 0x00008643;
        public const int GL_IR_INSTRUMENT1_SGIX = 0x0000817f;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_Y_ARB = 0x00008518;
        public const int GL_PIXEL_UNPACK_BUFFER_ARB = 0x000088ec;
        public const int GL_ALPHA_BIAS = 0x00000d1d;
        public const int GL_MAX_OPTIMIZED_VERTEX_SHADER_INVARIANTS_EXT = 0x000087cd;
        public const int GL_VERTEX_ATTRIB_ARRAY3_NV = 0x00008653;
        public const int GL_PIXEL_TRANSFORM_2D_EXT = 0x00008330;
        public const int GL_DUAL_INTENSITY12_SGIS = 0x0000811a;
        public const int GL_BOOL = 0x00008b56;
        public const int GL_LUMINANCE_FLOAT32_ATI = 0x00008818;
        public const int GL_ALIASED_POINT_SIZE_RANGE = 0x0000846d;
        public const int GL_MAX_PROGRAM_EXEC_INSTRUCTIONS_NV = 0x000088f4;
        public const int GL_WRITE_PIXEL_DATA_RANGE_POINTER_NV = 0x0000887c;
        public const int GL_EQUAL = 0x00000202;
        public const int GL_SHADOW_AMBIENT_SGIX = 0x000080bf;
        public const int GL_CON_3_ATI = 0x00008944;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_X = 0x00008516;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_Y = 0x00008518;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_Z = 0x0000851a;
        public const int GL_COMPRESSED_ALPHA = 0x000084e9;
        public const int GL_TRANSPOSE_TEXTURE_MATRIX = 0x000084e5;
        public const int GL_DEPTH_BITS = 0x00000d56;
        public const int GL_COLOR_ARRAY_STRIDE_EXT = 0x00008083;
        public const int GL_MAX_OPTIMIZED_VERTEX_SHADER_VARIANTS_EXT = 0x000087cb;
        public const int GL_SAMPLER_2D_SHADOW = 0x00008b62;
        public const int GL_COLOR_TABLE_LUMINANCE_SIZE = 0x000080de;
        public const int GL_RGB_S3TC = 0x000083a0;
        public const int GL_POINTS = 0x00000000;
        public const int GL_BLEND_EQUATION_RGB_EXT = 0x00008009;
        public const int GL_FOG_COORDINATE_ARRAY_LIST_STRIDE_IBM = 0x000192ae;
        public const int GL_OBJECT_TYPE_ARB = 0x00008b4e;
        public const int GL_CONVOLUTION_FILTER_SCALE_EXT = 0x00008014;
        public const int GL_SGIX_texture_multi_buffer = 0x00000001;
        public const int GL_PROGRAM_NATIVE_TEX_INDIRECTIONS_ARB = 0x0000880a;
        public const int GL_SECONDARY_COLOR_ARRAY = 0x0000845e;
        public const int GL_NORMAL_BIT_PGI = 0x08000000;
        public const int GL_MAP_ATTRIB_U_ORDER_NV = 0x000086c3;
        public const int GL_TEXTURE_COORD_ARRAY_PARALLEL_POINTERS_INTEL = 0x000083f8;
        public const int GL_CON_17_ATI = 0x00008952;
        public const int GL_COLOR_LOGIC_OP = 0x00000bf2;
        public const int GL_FOG_COORD = 0x00008451;
        public const int GL_MAX_PIXEL_TRANSFORM_2D_STACK_DEPTH_EXT = 0x00008337;
        public const int GL_SHADE_MODEL = 0x00000b54;
        public const int GL_CONVOLUTION_1D = 0x00008010;
        public const int GL_REPLICATE_BORDER_HP = 0x00008153;
        public const int GL_OUTPUT_TEXTURE_COORD29_EXT = 0x000087ba;
        public const int GL_SIGNED_INTENSITY_NV = 0x00008707;
        public const int GL_TEXTURE_BINDING_RECTANGLE_NV = 0x000084f6;
        public const int GL_WRITE_ONLY_ARB = 0x000088b9;
        public const int GL_TEXTURE1_ARB = 0x000084c1;
        public const int GL_TEXTURE_LOD_BIAS_EXT = 0x00008501;
        public const int GL_RGB12 = 0x00008053;
        public const int GL_RGB10 = 0x00008052;
        public const int GL_RGB16 = 0x00008054;
        public const int GL_PIXEL_TILE_CACHE_SIZE_SGIX = 0x00008145;
        public const int GL_PIXEL_MAP_G_TO_G = 0x00000c77;
        public const int GL_TRANSPOSE_CURRENT_MATRIX_ARB = 0x000088b7;
        public const int GL_CONSTANT_ALPHA = 0x00008003;
        public const int GL_SECONDARY_COLOR_ARRAY_TYPE_EXT = 0x0000845b;
        public const int GL_EYE_PLANE = 0x00002502;
        public const int GL_MINMAX_EXT = 0x0000802e;
        public const int GL_TEXTURE_2D_BINDING_EXT = 0x00008069;
        public const int GL_UNSIGNED_SHORT_8_8_REV_MESA = 0x000085bb;
        public const int GL_BUFFER_ACCESS = 0x000088bb;
        public const int GL_COLOR_ARRAY_SIZE_EXT = 0x00008081;
        public const int GL_CND0_ATI = 0x0000896b;
        public const int GL_POST_COLOR_MATRIX_ALPHA_SCALE_SGI = 0x000080b7;
        public const int GL_MODELVIEW10_ARB = 0x0000872a;
        public const int GL_FRAGMENT_LIGHT_MODEL_AMBIENT_SGIX = 0x0000840a;
        public const int GL_DRAW_BUFFER0_ATI = 0x00008825;
        public const int GL_FRAGMENT_PROGRAM_ARB = 0x00008804;
        public const int GL_EVAL_VERTEX_ATTRIB4_NV = 0x000086ca;
        public const int GL_SIGNED_NEGATE_NV = 0x0000853d;
        public const int GL_POST_COLOR_MATRIX_COLOR_TABLE = 0x000080d2;
        public const int GL_TEXTURE_MATRIX = 0x00000ba8;
        public const int GL_LOCAL_EXT = 0x000087c4;
        public const int GL_INDEX_MATERIAL_EXT = 0x000081b8;
        public const int GL_COLOR3_BIT_PGI = 0x00010000;
        public const int GL_RESAMPLE_ZERO_FILL_SGIX = 0x0000842f;
        public const int GL_DRAW_BUFFER11_ARB = 0x00008830;
        public const int GL_UNSIGNED_SHORT_5_6_5_REV_EXT = 0x00008364;
        public const int GL_VERTEX_ATTRIB_ARRAY1_NV = 0x00008651;
        public const int GL_BUFFER_USAGE_ARB = 0x00008765;
        public const int GL_FOG_COORD_ARRAY_POINTER = 0x00008456;
        public const int GL_MODELVIEW22_ARB = 0x00008736;
        public const int GL_DRAW_BUFFER12_ATI = 0x00008831;
        public const int GL_MAP2_VERTEX_ATTRIB12_4_NV = 0x0000867c;
        public const int GL_SHININESS = 0x00001601;
        public const int GL_PROGRAM_ERROR_STRING_NV = 0x00008874;
        public const int GL_OPERAND3_RGB_NV = 0x00008593;
        public const int GL_2PASS_0_EXT = 0x000080a2;
        public const int GL_MAP1_INDEX = 0x00000d91;
        public const int GL_WEIGHT_ARRAY_BUFFER_BINDING = 0x0000889e;
        public const int GL_RGBA8_EXT = 0x00008058;
        public const int GL_LINEAR_SHARPEN_ALPHA_SGIS = 0x000080ae;
        public const int GL_PRIMARY_COLOR_ARB = 0x00008577;
        public const int GL_FRAGMENT_COLOR_MATERIAL_PARAMETER_SGIX = 0x00008403;
        public const int GL_CLAMP_TO_BORDER_ARB = 0x0000812d;
        public const int GL_STENCIL_PASS_DEPTH_FAIL = 0x00000b95;
        public const int GL_ELEMENT_ARRAY_TYPE_ATI = 0x00008769;
        public const int GL_TEXTURE_LIGHTING_MODE_HP = 0x00008167;
        public const int GL_ALPHA12_EXT = 0x0000803d;
        public const int GL_TEXTURE_COMPARE_OPERATOR_SGIX = 0x0000819b;
        public const int GL_SEPARABLE_2D_EXT = 0x00008012;
        public const int GL_INVERSE_TRANSPOSE_NV = 0x0000862d;
        public const int GL_STATIC_COPY_ARB = 0x000088e6;
        public const int GL_SELECT = 0x00001c02;
        public const int GL_3D = 0x00000601;
        public const int GL_VERTEX_ATTRIB_ARRAY12_NV = 0x0000865c;
        public const int GL_DOT2_ADD_ATI = 0x0000896c;
        public const int GL_SCREEN_COORDINATES_REND = 0x00008490;
        public const int GL_MAP1_VERTEX_ATTRIB11_4_NV = 0x0000866b;
        public const int GL_TEXTURE_GEQUAL_R_SGIX = 0x0000819d;
        public const int GL_MAX_PROJECTION_STACK_DEPTH = 0x00000d38;
        public const int GL_MAX_ATTRIB_STACK_DEPTH = 0x00000d35;
        public const int GL_SAMPLER_3D = 0x00008b5f;
        public const int GL_INT_VEC2_ARB = 0x00008b53;
        public const int GL_TEXTURE8_ARB = 0x000084c8;
        public const int GL_OP_DOT4_EXT = 0x00008785;
        public const int GL_HISTOGRAM_EXT = 0x00008024;
        public const int GL_NATIVE_GRAPHICS_BEGIN_HINT_PGI = 0x0001a203;
        public const int GL_OBJECT_LINEAR = 0x00002401;
        public const int GL_DETAIL_TEXTURE_2D_SGIS = 0x00008095;
        public const int GL_ALPHA_SCALE = 0x00000d1c;
        public const int GL_OFFSET_TEXTURE_RECTANGLE_NV = 0x0000864c;
        public const int GL_TEXTURE_COMPARE_SGIX = 0x0000819a;
        public const int GL_BUMP_ROT_MATRIX_SIZE_ATI = 0x00008776;
        public const int GL_ONE_MINUS_CONSTANT_COLOR = 0x00008002;
        public const int GL_BIAS_BIT_ATI = 0x00000008;
        public const int GL_NORMAL_ARRAY_POINTER = 0x0000808f;
        public const int GL_DUAL_INTENSITY16_SGIS = 0x0000811b;
        public const int GL_MINMAX_SINK_EXT = 0x00008030;
        public const int GL_DRAW_BUFFER10_ATI = 0x0000882f;
        public const int GL_BLEND_EQUATION_ALPHA_EXT = 0x0000883d;
        public const int GL_SCALE_BY_ONE_HALF_NV = 0x00008540;
        public const int GL_SPRITE_TRANSLATION_SGIX = 0x0000814b;
        public const int GL_3D_COLOR = 0x00000602;
        public const int GL_STENCIL = 0x00001802;
        public const int GL_COLOR_SUM_ARB = 0x00008458;
        public const int GL_QUERY_COUNTER_BITS = 0x00008864;
        public const int GL_VERTEX_ATTRIB_ARRAY_STRIDE_ARB = 0x00008624;
        public const int GL_PROGRAM_NATIVE_PARAMETERS_ARB = 0x000088aa;
        public const int GL_DRAW_BUFFER15_ARB = 0x00008834;
        public const int GL_STENCIL_INDEX4_EXT = 0x00008d47;
        public const int GL_CONSTANT_COLOR_EXT = 0x00008001;
        public const int GL_CURRENT_PALETTE_MATRIX_ARB = 0x00008843;
        public const int GL_CURRENT_VERTEX_WEIGHT_EXT = 0x0000850b;
        public const int GL_LUMINANCE8_EXT = 0x00008040;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_X_EXT = 0x00008515;
        public const int GL_POINT = 0x00001b00;
        public const int GL_SINGLE_COLOR = 0x000081f9;
        public const int GL_SELECTION_BUFFER_POINTER = 0x00000df3;
        public const int GL_POINT_SIZE_MAX_EXT = 0x00008127;
        public const int GL_MAP1_VERTEX_ATTRIB13_4_NV = 0x0000866d;
        public const int GL_TEXTURE_COORD_ARRAY_SIZE = 0x00008088;
        public const int GL_COMP_BIT_ATI = 0x00000002;
        public const int GL_LIGHTING_BIT = 0x00000040;
        public const int GL_TEXTURE_1D = 0x00000de0;
        public const int GL_COLOR_ATTACHMENT8_EXT = 0x00008ce8;
        public const int GL_DOT_PRODUCT_NV = 0x000086ec;
        public const int GL_EVAL_VERTEX_ATTRIB3_NV = 0x000086c9;
        public const int GL_COMPRESSED_RGBA_ARB = 0x000084ee;
        public const int GL_MAX_3D_TEXTURE_SIZE = 0x00008073;
        public const int GL_TEXTURE_RECTANGLE_NV = 0x000084f5;
        public const int GL_TEXTURE_COMPRESSED_ARB = 0x000086a1;
        public const int GL_TEXTURE_BINDING_CUBE_MAP = 0x00008514;
        public const int GL_SECONDARY_COLOR_ARRAY_EXT = 0x0000845e;
        public const int GL_COMBINE_RGB_EXT = 0x00008571;
        public const int GL_GREEN_SCALE = 0x00000d18;
        public const int GL_SOURCE2_RGB_ARB = 0x00008582;
        public const int GL_BLUE_BITS = 0x00000d54;
        public const int GL_EVAL_VERTEX_ATTRIB8_NV = 0x000086ce;
        public const int GL_DOT3_RGBA = 0x000086af;
        public const int GL_SAMPLER_1D = 0x00008b5d;
        public const int GL_LINEAR_DETAIL_COLOR_SGIS = 0x00008099;
        public const int GL_MATRIX5_ARB = 0x000088c5;
        public const int GL_DRAW_BUFFER4_ATI = 0x00008829;
        public const int GL_ELEMENT_ARRAY_POINTER_APPLE = 0x0000876a;
        public const int GL_EYE_LINEAR = 0x00002400;
        public const int GL_REG_7_ATI = 0x00008928;
        public const int GL_LUMINANCE_FLOAT16_ATI = 0x0000881e;
        public const int GL_EVAL_VERTEX_ATTRIB1_NV = 0x000086c7;
        public const int GL_NEGATE_BIT_ATI = 0x00000004;
        public const int GL_RGBA_FLOAT16_ATI = 0x0000881a;
        public const int GL_READ_WRITE = 0x000088ba;
        public const int GL_VERTEX_ATTRIB_ARRAY_POINTER = 0x00008645;
        public const int GL_YCRCB_444_SGIX = 0x000081bc;
        public const int GL_ALLOW_DRAW_MEM_HINT_PGI = 0x0001a211;
        public const int GL_VERTEX_ATTRIB_ARRAY_STRIDE = 0x00008624;
        public const int GL_LINES = 0x00000001;
        public const int GL_PIXEL_MAP_S_TO_S = 0x00000c71;
        public const int GL_SAMPLER_1D_ARB = 0x00008b5d;
        public const int GL_OP_EXP_BASE_2_EXT = 0x00008791;
        public const int GL_PROGRAM_ALU_INSTRUCTIONS_ARB = 0x00008805;
        public const int GL_GREEN = 0x00001904;
        public const int GL_COLOR_SUM_EXT = 0x00008458;
        public const int GL_TRACK_MATRIX_TRANSFORM_NV = 0x00008649;
        public const int GL_POST_CONVOLUTION_BLUE_BIAS = 0x00008022;
        public const int GL_DECR = 0x00001e03;
        public const int GL_TEXTURE_3D_BINDING_EXT = 0x0000806a;
        public const int GL_MATRIX3_NV = 0x00008633;
        public const int GL_SAMPLE_ALPHA_TO_COVERAGE_ARB = 0x0000809e;
        public const int GL_DEPTH_TEXTURE_MODE_ARB = 0x0000884b;
        public const int GL_DOT3_RGB = 0x000086ae;
        public const int GL_XOR = 0x00001506;
        public const int GL_MULTISAMPLE_BIT_ARB = 0x20000000;
        public const int GL_VERTEX_PROGRAM_TWO_SIDE_NV = 0x00008643;
        public const int GL_REG_17_ATI = 0x00008932;
        public const int GL_Q = 0x00002003;
        public const int GL_MAX_ASYNC_DRAW_PIXELS_SGIX = 0x00008360;
        public const int GL_TEXTURE_ALPHA_SIZE = 0x0000805f;
        public const int GL_COMPILE = 0x00001300;
        public const int GL_OFFSET_HILO_PROJECTIVE_TEXTURE_2D_NV = 0x00008856;
        public const int GL_MAX_PROGRAM_NATIVE_PARAMETERS_ARB = 0x000088ab;
        public const int GL_POINT_SMOOTH_HINT = 0x00000c51;
        public const int GL_SGIS_generate_mipmap = 0x00000001;
        public const int GL_PIXEL_MODE_BIT = 0x00000020;
        public const int GL_OUTPUT_TEXTURE_COORD4_EXT = 0x000087a1;
        public const int GL_COLOR_TABLE_INTENSITY_SIZE = 0x000080df;
        public const int GL_VERTEX_SHADER_INVARIANTS_EXT = 0x000087d1;
        public const int GL_COMBINER_COMPONENT_USAGE_NV = 0x00008544;
        public const int GL_SWIZZLE_STRQ_ATI = 0x0000897a;
        public const int GL_CON_8_ATI = 0x00008949;
        public const int GL_RGBA2_EXT = 0x00008055;
        public const int GL_POST_COLOR_MATRIX_RED_BIAS = 0x000080b8;
        public const int GL_MATRIX5_NV = 0x00008635;
        public const int GL_TEXTURE_CUBE_MAP_NEGATIVE_Y_EXT = 0x00008518;
        public const int GL_POLYGON_OFFSET_EXT = 0x00008037;
        public const int GL_PIXEL_GROUP_COLOR_SGIS = 0x00008356;
        public const int GL_DOT_PRODUCT_TEXTURE_2D_NV = 0x000086ee;
        public const int GL_SMOOTH = 0x00001d01;
        public const int GL_POLYGON_OFFSET_LINE = 0x00002a02;
        public const int GL_TEXTURE_MIN_LOD = 0x0000813a;
        public const int GL_BGRA = 0x000080e1;
        public const int GL_PHONG_HINT_WIN = 0x000080eb;
        public const int GL_FRONT = 0x00000404;
        public const int GL_MATRIX4_NV = 0x00008634;
        public const int GL_8X_BIT_ATI = 0x00000004;
        public const int GL_PROGRAM_ERROR_POSITION_ARB = 0x0000864b;
        public const int GL_SAMPLER_2D_ARB = 0x00008b5e;
        public const int GL_ALPHA_MAX_CLAMP_INGR = 0x00008567;
        public const int GL_R3_G3_B2 = 0x00002a10;
        public const int GL_EDGE_FLAG_ARRAY_STRIDE_EXT = 0x0000808c;
        public const int GL_COLOR_ATTACHMENT14_EXT = 0x00008cee;
        public const int GL_MAP2_VERTEX_ATTRIB5_4_NV = 0x00008675;
        public const int GL_INTENSITY4_EXT = 0x0000804a;
        public const int GL_TEXTURE_MULTI_BUFFER_HINT_SGIX = 0x0000812e;
        public const int GL_DUAL_TEXTURE_SELECT_SGIS = 0x00008124;
        public const int GL_TEXTURE_ALPHA_SIZE_EXT = 0x0000805f;
        public const int GL_GREEN_BITS = 0x00000d53;
        public const int GL_TEXTURE_1D_BINDING_EXT = 0x00008068;
        public const int GL_RENDER_MODE = 0x00000c40;
        public const int GL_OUTPUT_TEXTURE_COORD17_EXT = 0x000087ae;
        public const int GL_QUAD_ALPHA8_SGIS = 0x0000811f;
        public const int GL_DRAW_BUFFER15_ATI = 0x00008834;
        public const int GL_UNPACK_IMAGE_HEIGHT_EXT = 0x0000806e;
        public const int GL_EVAL_VERTEX_ATTRIB7_NV = 0x000086cd;
        public const int GL_VERTEX_ATTRIB_ARRAY11_NV = 0x0000865b;
        public const int GL_LIGHT_MODEL_TWO_SIDE = 0x00000b52;
        public const int GL_2X_BIT_ATI = 0x00000001;
        public const int GL_FEEDBACK_BUFFER_TYPE = 0x00000df2;
        public const int GL_OP_NEGATE_EXT = 0x00008783;
        public const int GL_BACK_LEFT = 0x00000402;
        public const int GL_DEPTH_TEST = 0x00000b71;
        public const int GL_REPLACEMENT_CODE_ARRAY_SUN = 0x000085c0;
        public const int GL_TEXTURE_MIN_LOD_SGIS = 0x0000813a;
        public const int GL_POLYGON_OFFSET_UNITS = 0x00002a00;
        public const int GL_MODELVIEW30_ARB = 0x0000873e;
        public const int GL_CLAMP_TO_BORDER = 0x0000812d;
        public const int GL_UNSIGNED_SHORT_5_6_5 = 0x00008363;
        public const int GL_SWIZZLE_STR_DR_ATI = 0x00008978;
        public const int GL_MAX_RECTANGLE_TEXTURE_SIZE_ARB = 0x000084f8;
        public const int GL_IMAGE_ROTATE_ORIGIN_Y_HP = 0x0000815b;
        public const int GL_STREAM_READ = 0x000088e1;
        public const int GL_NORMAL_ARRAY_STRIDE = 0x0000807f;
        public const int GL_GENERATE_MIPMAP = 0x00008191;
        public const int GL_VARIABLE_D_NV = 0x00008526;
        public const int GL_SPARE1_NV = 0x0000852f;
        public const int GL_CURRENT_FOG_COORD = 0x00008453;
        public const int GL_TABLE_TOO_LARGE = 0x00008031;
        public const int GL_VERTEX_ATTRIB_ARRAY15_NV = 0x0000865f;
        public const int GL_SIGNED_RGBA_NV = 0x000086fb;
        public const int GL_POST_COLOR_MATRIX_RED_SCALE = 0x000080b4;
        public const int GL_ELEMENT_ARRAY_BUFFER_BINDING = 0x00008895;
        public const int GL_EVAL_2D_NV = 0x000086c0;
        public const int GL_TEXTURE5_ARB = 0x000084c5;
        public const int GL_BACK_RIGHT = 0x00000403;
        public const int GL_MODELVIEW12_ARB = 0x0000872c;
        public const int GL_DEPTH_BIAS = 0x00000d1f;
        public const int GL_LIST_INDEX = 0x00000b33;
        public const int GL_COMBINER_CD_DOT_PRODUCT_NV = 0x00008546;
        public const int GL_MULTISAMPLE_EXT = 0x0000809d;
        public const int GL_MAP_STENCIL = 0x00000d11;
        public const int GL_3D_COLOR_TEXTURE = 0x00000603;
        public const int GL_SCALEBIAS_HINT_SGIX = 0x00008322;
        public const int GL_PIXEL_TEXTURE_SGIS = 0x00008353;
        public const int GL_SGIS_point_line_texgen = 0x00000001;
        public const int GL_RGB_FLOAT16_ATI = 0x0000881b;
        public const int GL_422_AVERAGE_EXT = 0x000080ce;
        public const int GL_CULL_FACE = 0x00000b44;
        public const int GL_COLOR_ATTACHMENT9_EXT = 0x00008ce9;
        public const int GL_SRC0_RGB = 0x00008580;
        public const int GL_NOOP = 0x00001505;
        public const int GL_SUBPIXEL_BITS = 0x00000d50;
        public const int GL_HILO16_NV = 0x000086f8;
        public const int GL_STATIC_READ_ARB = 0x000088e5;
        public const int GL_ONE_EXT = 0x000087de;
        public const int GL_DYNAMIC_COPY = 0x000088ea;
        public const int GL_POLYGON_STIPPLE = 0x00000b42;
        public const int GL_SGIX_shadow = 0x00000001;
        public const int GL_MATRIX24_ARB = 0x000088d8;
        public const int GL_PIXEL_TILE_BEST_ALIGNMENT_SGIX = 0x0000813e;
        public const int GL_REG_25_ATI = 0x0000893a;
        public const int GL_PACK_SKIP_IMAGES_EXT = 0x0000806b;
        public const int GL_MATRIX2_ARB = 0x000088c2;
        public const int GL_CLAMP_VERTEX_COLOR_ARB = 0x0000891a;
        public const int GL_SGIX_texture_add_env = 0x00000001;
        public const int GL_CLIENT_VERTEX_ARRAY_BIT = 0x00000002;
        public const int GL_MAX_PN_TRIANGLES_TESSELATION_LEVEL_ATI = 0x000087f1;
        public const int GL_COMBINER6_NV = 0x00008556;
        public const int GL_MAP1_VERTEX_ATTRIB4_4_NV = 0x00008664;
        public const int GL_CONVOLUTION_FILTER_SCALE = 0x00008014;
        public const int GL_SPHERE_MAP = 0x00002402;
        public const int GL_COMPRESSED_TEXTURE_FORMATS_ARB = 0x000086a3;
        public const int GL_LUMINANCE_ALPHA_FLOAT16_ATI = 0x0000881f;
        public const int GL_LINEAR_MIPMAP_NEAREST = 0x00002701;
        public const int GL_R = 0x00002002;
        public const int GL_TEXTURE_COMPRESSED_IMAGE_SIZE = 0x000086a0;
        public const int GL_LOGIC_OP_MODE = 0x00000bf0;
        public const int GL_MODELVIEW25_ARB = 0x00008739;
        public const int GL_MODULATE = 0x00002100;
        public const int GL_CULL_VERTEX_OBJECT_POSITION_EXT = 0x000081ac;
        public const int GL_BLUE = 0x00001905;
        public const int GL_SGIX_shadow_ambient = 0x00000001;
        public const int GL_CURRENT_WEIGHT_ARB = 0x000086a8;
        public const int GL_UNSIGNED_INT_10_10_10_2 = 0x00008036;
        public const int GL_MAP1_VERTEX_ATTRIB2_4_NV = 0x00008662;
        public const int GL_BUMP_TARGET_ATI = 0x0000877c;
        public const int GL_CONVOLUTION_BORDER_MODE_EXT = 0x00008013;
        public const int GL_TEXTURE_DEPTH_SIZE = 0x0000884a;
        public const int GL_PROGRAM_FORMAT_ASCII_ARB = 0x00008875;
        public const int GL_MATRIX1_NV = 0x00008631;
        public const int GL_MAX_ASYNC_TEX_IMAGE_SGIX = 0x0000835f;
        public const int GL_PIXEL_MAP_I_TO_B_SIZE = 0x00000cb4;
        public const int GL_WEIGHT_SUM_UNITY_ARB = 0x000086a6;
        public const int GL_RESAMPLE_AVERAGE_OML = 0x00008988;
        public const int GL_DOT3_ATI = 0x00008966;
        public const int GL_CON_15_ATI = 0x00008950;
        public const int GL_OBJECT_DELETE_STATUS_ARB = 0x00008b80;
        public const int GL_OUTPUT_TEXTURE_COORD2_EXT = 0x0000879f;
        public const int GL_PROGRAM_BINDING_ARB = 0x00008677;
        public const int GL_GLOBAL_ALPHA_SUN = 0x000081d9;
        public const int GL_MAX_VERTEX_HINT_PGI = 0x0001a22d;
        public const int GL_MAX_TEXTURE_LOD_BIAS = 0x000084fd;
        public const int GL_CURRENT_RASTER_POSITION = 0x00000b07;
        public const int GL_FRAMEBUFFER_STATUS_ERROR_EXT = 0x00008cde;
        public const int GL_CONVOLUTION_HEIGHT = 0x00008019;
        public const int GL_OUTPUT_TEXTURE_COORD26_EXT = 0x000087b7;
        public const int GL_OFFSET_TEXTURE_SCALE_NV = 0x000086e2;
        public const int GL_OP_SET_GE_EXT = 0x0000878c;
        public const int GL_COMBINER_INPUT_NV = 0x00008542;
        public const int GL_OPERAND1_ALPHA = 0x00008599;
        public const int GL_DRAW_PIXELS_APPLE = 0x00008a0a;
        public const int GL_REG_8_ATI = 0x00008929;
        public const int GL_ELEMENT_ARRAY_BUFFER = 0x00008893;
        public const int GL_TEXTURE_COORD_ARRAY_TYPE_EXT = 0x00008089;
        public const int GL_TEXTURE_COORD_ARRAY_BUFFER_BINDING = 0x0000889a;
        public const int GL_PROXY_TEXTURE_2D_EXT = 0x00008064;
        public const int GL_ALIASED_LINE_WIDTH_RANGE = 0x0000846e;
        public const int GL_ALPHA_MAX_SGIX = 0x00008321;
        public const int GL_INFO_LOG_LENGTH = 0x00008b84;
        public const int GL_OFFSET_TEXTURE_RECTANGLE_SCALE_NV = 0x0000864d;
        public const int GL_RIGHT = 0x00000407;
        public const int GL_COLOR_TABLE_BIAS = 0x000080d7;
        public const int GL_FLOAT = 0x00001406;
        public const int GL_TRIANGLE_FAN = 0x00000006;
        public const int GL_TEXTURE_COMPARE_MODE = 0x0000884c;
        public const int GL_DYNAMIC_READ_ARB = 0x000088e9;
        public const int GL_SCALE_BY_TWO_NV = 0x0000853e;
        public const int GL_TEXTURE_2D = 0x00000de1;
        public const int GL_SUB_ATI = 0x00008965;
        public const int GL_TEXTURE_RED_TYPE_ARB = 0x00008c10;
        public const int GL_GREEN_MAX_CLAMP_INGR = 0x00008565;
        public const int GL_PROXY_POST_IMAGE_TRANSFORM_COLOR_TABLE_HP = 0x00008163;
        public const int GL_DSDT8_MAG8_INTENSITY8_NV = 0x0000870b;
        public const int GL_BGR = 0x000080e0;
        public const int GL_RED_BIT_ATI = 0x00000001;
        public const int GL_EXP = 0x00000800;
        public const int GL_COMPRESSED_INTENSITY = 0x000084ec;
        public const int GL_NUM_PASSES_ATI = 0x00008970;
        public const int GL_ALPHA_TEST_FUNC = 0x00000bc1;
        public const int GL_COMBINER3_NV = 0x00008553;
        public const int GL_ALPHA16_EXT = 0x0000803e;
        public const int GL_INVARIANT_DATATYPE_EXT = 0x000087eb;
        public const int GL_TEXTURE_INTENSITY_TYPE_ARB = 0x00008c15;
        public const int GL_VERTEX_ATTRIB_ARRAY2_NV = 0x00008652;
        public const int GL_VERTEX_SHADER_LOCAL_CONSTANTS_EXT = 0x000087d2;
        public const int GL_OBJECT_ACTIVE_ATTRIBUTE_MAX_LENGTH_ARB = 0x00008b8a;
        public const int GL_NEAREST_CLIPMAP_LINEAR_SGIX = 0x0000844e;
        public const int GL_POST_CONVOLUTION_RED_SCALE_EXT = 0x0000801c;
        public const int GL_PIXEL_MAP_B_TO_B = 0x00000c78;
        public const int GL_UNPACK_SKIP_VOLUMES_SGIS = 0x00008132;
        public const int GL_VERTEX_STREAM6_ATI = 0x00008772;
        public const int GL_COMBINE_ALPHA_EXT = 0x00008572;
        public const int GL_ARRAY_BUFFER = 0x00008892;
        public const int GL_MODELVIEW23_ARB = 0x00008737;
        public const int GL_FOG_COORDINATE_ARRAY_TYPE = 0x00008454;
        public const int GL_CURRENT_QUERY_ARB = 0x00008865;
        public const int GL_EYE_DISTANCE_TO_POINT_SGIS = 0x000081f0;
        public const int GL_PIXEL_SUBSAMPLE_4242_SGIX = 0x000085a4;
        public const int GL_TEXTURE_GREEN_SIZE_EXT = 0x0000805d;
        public const int GL_FRAGMENT_PROGRAM_NV = 0x00008870;
        public const int GL_ONE_MINUS_DST_ALPHA = 0x00000305;
        public const int GL_SAMPLE_ALPHA_TO_MASK_SGIS = 0x0000809e;
        public const int GL_COMBINER1_NV = 0x00008551;
        public const int GL_RGB_SCALE_EXT = 0x00008573;
        public const int GL_MODELVIEW_MATRIX = 0x00000ba6;
        public const int GL_TEXTURE31 = 0x000084df;
        public const int GL_TEXTURE30 = 0x000084de;
        public const int GL_RETURN = 0x00000102;
        public const int GL_MATRIX3_ARB = 0x000088c3;
        public const int GL_VARIANT_ARRAY_POINTER_EXT = 0x000087e9;
        public const int GL_DYNAMIC_READ = 0x000088e9;
        public const int GL_VERTEX_SHADER_EXT = 0x00008780;
        public const int GL_DSDT_MAG_NV = 0x000086f6;
        public const int GL_COMBINER5_NV = 0x00008555;
        public const int GL_E_TIMES_F_NV = 0x00008531;
        public const int GL_TEXTURE_BINDING_2D = 0x00008069;
        public const int GL_MODELVIEW28_ARB = 0x0000873c;
        public const int GL_TEXTURE_COORD_ARRAY_EXT = 0x00008078;
        public const int GL_MAX_ELEMENTS_INDICES_EXT = 0x000080e9;
        public const int GL_NONE = 0x00000000;
        public const int GL_UNSIGNED_INT_2_10_10_10_REV_EXT = 0x00008368;
        public const int GL_LINEAR_DETAIL_SGIS = 0x00008097;
        public const int GL_TEXTURE_GEN_T = 0x00000c61;
        public const int GL_PROXY_TEXTURE_RECTANGLE_NV = 0x000084f7;
        public const int GL_TEXTURE_FILTER4_SIZE_SGIS = 0x00008147;
        public const int GL_VERTEX_ATTRIB_ARRAY_BUFFER_BINDING = 0x0000889f;
        public const int GL_PIXEL_TEX_GEN_ALPHA_MS_SGIX = 0x0000818a;
        public const int GL_TEXTURE_GEN_R = 0x00000c62;
        public const int GL_W_EXT = 0x000087d8;
        public const int GL_FILL = 0x00001b02;
        public const int GL_TEXTURE_MAX_ANISOTROPY_EXT = 0x000084fe;
        public const int GL_SCISSOR_BOX = 0x00000c10;
        public const int GL_READ_WRITE_ARB = 0x000088ba;
        public const int GL_MAX_PIXEL_MAP_TABLE = 0x00000d34;
        public const int GL_FLOAT_MAT2 = 0x00008b5a;
        public const int GL_FLOAT_MAT4 = 0x00008b5c;
        public const int GL_DUAL_LUMINANCE_ALPHA8_SGIS = 0x0000811d;
        public const int GL_S = 0x00002000;
        public const int GL_FLOAT_VEC2_ARB = 0x00008b50;
        public const int GL_MAP2_VERTEX_ATTRIB13_4_NV = 0x0000867d;
        public const int GL_TEXTURE_RED_SIZE_EXT = 0x0000805c;
        public const int GL_PACK_IMAGE_DEPTH_SGIS = 0x00008131;
        public const int GL_WEIGHT_ARRAY_ARB = 0x000086ad;
        public const int GL_SECONDARY_COLOR_ARRAY_BUFFER_BINDING_ARB = 0x0000889c;
        public const int GL_TEXTURE_COORD_ARRAY_POINTER_EXT = 0x00008092;
        public const int GL_INDEX_ARRAY_TYPE = 0x00008085;
        public const int GL_FRAGMENT_LIGHT0_SGIX = 0x0000840c;
        public const int GL_ONE_MINUS_SRC_COLOR = 0x00000301;
        public const int GL_TEXTURE_CUBE_MAP_EXT = 0x00008513;
        public const int GL_PREVIOUS_TEXTURE_INPUT_NV = 0x000086e4;
        public const int GL_DISTANCE_ATTENUATION_SGIS = 0x00008129;
        public const int GL_REG_13_ATI = 0x0000892e;
        public const int GL_FUNC_SUBTRACT = 0x0000800a;
        public const int GL_MODELVIEW27_ARB = 0x0000873b;
        public const int GL_EYE_POINT_SGIS = 0x000081f4;
        public const int GL_OBJECT_ATTACHED_OBJECTS_ARB = 0x00008b85;
        public const int GL_SPRITE_MODE_SGIX = 0x00008149;
        public const int GL_MAX_FRAGMENT_UNIFORM_COMPONENTS = 0x00008b49;
        public const int GL_FRONT_FACE = 0x00000b46;
        public const int GL_MIRRORED_REPEAT = 0x00008370;
        public const int GL_VERTEX_SHADER_LOCALS_EXT = 0x000087d3;
        public const int GL_PROGRAM_PARAMETERS_ARB = 0x000088a8;
        public const int GL_SAMPLER_2D_RECT_SHADOW_ARB = 0x00008b64;
        public const int GL_SRC_ALPHA = 0x00000302;
        public const int GL_OBJECT_BUFFER_SIZE_ATI = 0x00008764;
        public const int GL_CONVOLUTION_FORMAT = 0x00008017;
        public const int GL_STENCIL_FUNC = 0x00000b92;
        public const int GL_SIGNED_LUMINANCE8_NV = 0x00008702;
        public const int GL_DEPTH_ATTACHMENT_EXT = 0x00008d00;
        public const int GL_STENCIL_BACK_FAIL = 0x00008801;
        public const int GL_DIFFUSE = 0x00001201;
        public const int GL_COLOR_ATTACHMENT3_EXT = 0x00008ce3;
        public const int GL_T2F_C4UB_V3F = 0x00002a29;
        public const int GL_POST_CONVOLUTION_BLUE_SCALE_EXT = 0x0000801e;
        public const int GL_VERTEX_ATTRIB_ARRAY_TYPE_ARB = 0x00008625;
        public const int GL_COMBINER0_NV = 0x00008550;
        public const int GL_VERTEX_ATTRIB_ARRAY13_NV = 0x0000865d;
        public const int GL_RESCALE_NORMAL_EXT = 0x0000803a;
        public const int GL_POST_CONVOLUTION_ALPHA_BIAS_EXT = 0x00008023;
        public const int GL_LIGHTING = 0x00000b50;
        public const int GL_V2F = 0x00002a20;
        public const int GL_PIXEL_MAP_A_TO_A = 0x00000c79;
        public const int GL_SAMPLER_1D_SHADOW = 0x00008b61;
        public const int GL_CURRENT_COLOR = 0x00000b00;
        public const int GL_POST_COLOR_MATRIX_BLUE_SCALE_SGI = 0x000080b6;
        public const int GL_UNSIGNED_INVERT_NV = 0x00008537;
        public const int GL_COMPRESSED_ALPHA_ARB = 0x000084e9;
        public const int GL_TEXTURE_CUBE_MAP_ARB = 0x00008513;
        public const int GL_CURRENT_VERTEX_EXT = 0x000087e2;
        public const int GL_CON_23_ATI = 0x00008958;
        public const int GL_ELEMENT_ARRAY_TYPE_APPLE = 0x00008769;
        public const int GL_MULTISAMPLE_FILTER_HINT_NV = 0x00008534;
        public const int GL_SGIX_depth_texture = 0x00000001;
        public const int GL_ARRAY_BUFFER_ARB = 0x00008892;
        public const int GL_FRAGMENT_PROGRAM_BINDING_NV = 0x00008873;
        public const int GL_RENDERBUFFER_EXT = 0x00008d41;
        public const int GL_CON_24_ATI = 0x00008959;
        public const int GL_OUTPUT_TEXTURE_COORD9_EXT = 0x000087a6;
        public const int GL_INDEX_BIT_PGI = 0x00080000;
        public const int GL_SEPARABLE_2D = 0x00008012;
        public const int GL_CON_13_ATI = 0x0000894e;
        public const int GL_FOG_COORDINATE_EXT = 0x00008451;
        public const int GL_R1UI_C4UB_V3F_SUN = 0x000085c5;
        public const int GL_FOG_COORDINATE = 0x00008451;
        public const int GL_READ_ONLY = 0x000088b8;
        public const int GL_MATRIX7_ARB = 0x000088c7;
        public const int GL_COLOR_MATERIAL_FACE = 0x00000b55;
        public const int GL_HISTOGRAM_RED_SIZE = 0x00008028;
        public const int GL_TEXTURE_MAX_CLAMP_R_SGIX = 0x0000836b;
        public const int GL_COLOR_INDEX12_EXT = 0x000080e6;
        public const int GL_PROGRAM_TEMPORARIES_ARB = 0x000088a4;
        public const int GL_MAP1_VERTEX_ATTRIB9_4_NV = 0x00008669;
        public const int GL_MODELVIEW3_ARB = 0x00008723;
        public const int GL_OUTPUT_TEXTURE_COORD18_EXT = 0x000087af;
        public const int GL_SRC2_RGB = 0x00008582;
        public const int GL_TEXTURE_3D = 0x0000806f;
        public const int GL_REG_10_ATI = 0x0000892b;
        public const int GL_MAX_PROGRAM_ADDRESS_REGISTERS_ARB = 0x000088b1;
        public const int GL_EVAL_FRACTIONAL_TESSELLATION_NV = 0x000086c5;
        public const int GL_COMPRESSED_RGB = 0x000084ed;
        public const int GL_CONSTANT_COLOR0_NV = 0x0000852a;
        public const int GL_TEXTURE_4DSIZE_SGIS = 0x00008136;
        public const int GL_ASYNC_TEX_IMAGE_SGIX = 0x0000835c;
        public const int GL_STENCIL_BACK_REF = 0x00008ca3;
        public const int GL_COLOR_ARRAY_SIZE = 0x00008081;
        public const int GL_NUM_INSTRUCTIONS_TOTAL_ATI = 0x00008972;
        public const int GL_SECONDARY_COLOR_ARRAY_POINTER_EXT = 0x0000845d;
        public const int GL_WRITE_ONLY = 0x000088b9;
        public const int GL_TEXTURE_ENV = 0x00002300;
        public const int GL_POST_CONVOLUTION_GREEN_BIAS_EXT = 0x00008021;
        public const int GL_NUM_GENERAL_COMBINERS_NV = 0x0000854e;
        public const int GL_MATRIX0_ARB = 0x000088c0;
        public const int GL_ALWAYS = 0x00000207;
        public const int GL_MAX_TEXTURE_SIZE = 0x00000d33;
        public const int GL_COLOR_MATRIX_SGI = 0x000080b1;
        public const int GL_ASYNC_READ_PIXELS_SGIX = 0x0000835e;
        public const int GL_TEXTURE_MATERIAL_FACE_EXT = 0x00008351;
        public const int GL_TEXCOORD3_BIT_PGI = 0x40000000;
        public const int GL_LUMINANCE4_ALPHA4_EXT = 0x00008043;
        public const int GL_CON_7_ATI = 0x00008948;
        public const int GL_DRAW_BUFFER13_ATI = 0x00008832;
        public const int GL_TEXTURE_COORD_ARRAY = 0x00008078;
        public const int GL_MAP2_GRID_DOMAIN = 0x00000dd2;
        public const int GL_MAX_TEXTURE_LOD_BIAS_EXT = 0x000084fd;
        public const int GL_FOG_FACTOR_TO_ALPHA_SGIX = 0x0000836f;
        public const int GL_READ_PIXEL_DATA_RANGE_LENGTH_NV = 0x0000887b;
        public const int GL_LUMINANCE4_ALPHA4 = 0x00008043;
        public const int GL_UNSIGNED_SHORT_5_6_5_EXT = 0x00008363;
        public const int GL_LERP_ATI = 0x00008969;
        public const int GL_SHADING_LANGUAGE_VERSION_ARB = 0x00008b8c;
        public const int GL_SUBTRACT = 0x000084e7;
        public const int GL_OUTPUT_TEXTURE_COORD28_EXT = 0x000087b9;
        public const int GL_DEPTH_PASS_INSTRUMENT_MAX_SGIX = 0x00008312;
        public const int GL_TANGENT_ARRAY_TYPE_EXT = 0x0000843e;
        public const int GL_SAMPLE_ALPHA_TO_ONE_EXT = 0x0000809f;
        public const int GL_POST_TEXTURE_FILTER_SCALE_RANGE_SGIX = 0x0000817c;
        public const int GL_SECONDARY_COLOR_ARRAY_STRIDE = 0x0000845c;
        public const int GL_HISTOGRAM_GREEN_SIZE = 0x00008029;
        public const int GL_OR_INVERTED = 0x0000150d;
        public const int GL_TEXTURE_SHADER_NV = 0x000086de;
        public const int GL_DRAW_BUFFER1 = 0x00008826;
        public const int GL_DRAW_BUFFER0 = 0x00008825;
        public const int GL_AUTO_NORMAL = 0x00000d80;
        public const int GL_DRAW_BUFFER2 = 0x00008827;
        public const int GL_DRAW_BUFFER5 = 0x0000882a;
        public const int GL_DRAW_BUFFER4 = 0x00008829;
        public const int GL_DRAW_BUFFER7 = 0x0000882c;
        public const int GL_DRAW_BUFFER6 = 0x0000882b;
        public const int GL_DRAW_BUFFER9 = 0x0000882e;
        public const int GL_DRAW_BUFFER8 = 0x0000882d;
        public const int GL_FLOAT_VEC4_ARB = 0x00008b52;
        public const int GL_PROXY_POST_CONVOLUTION_COLOR_TABLE_SGI = 0x000080d4;
        public const int GL_VERTEX_CONSISTENT_HINT_PGI = 0x0001a22b;
        public const int GL_VENDOR = 0x00001f00;
        public const int GL_VERTEX_ARRAY_RANGE_POINTER_NV = 0x00008521;
        public const int GL_Y_EXT = 0x000087d6;
        public const int GL_CLEAR = 0x00001500;
        public const int GL_RESAMPLE_ZERO_FILL_OML = 0x00008987;
        public const int GL_TEXTURE_COORD_ARRAY_POINTER = 0x00008092;
        public const int GL_CLAMP_TO_EDGE = 0x0000812f;
        public const int GL_T = 0x00002001;
        public const int GL_OP_ROUND_EXT = 0x00008790;
        public const int GL_SHADOW_ATTENUATION_EXT = 0x0000834e;
        public const int GL_VERTEX4_BIT_PGI = 0x00000008;
        public const int GL_OUTPUT_TEXTURE_COORD20_EXT = 0x000087b1;
        public const int GL_PIXEL_TILE_GRID_DEPTH_SGIX = 0x00008144;
        public const int GL_QUARTER_BIT_ATI = 0x00000010;
        public const int GL_MODELVIEW9_ARB = 0x00008729;
        public const int GL_COLOR_MATERIAL = 0x00000b57;
        public const int GL_HINT_BIT = 0x00008000;
        public const int GL_DOUBLE = 0x0000140a;
        public const int GL_RGB32F_ARB = 0x00008815;
        public const int GL_RED_BIAS = 0x00000d15;
        public const int GL_TEXTURE_MAG_SIZE_NV = 0x0000871f;
        public const int GL_TEXTURE_CLIPMAP_DEPTH_SGIX = 0x00008176;
        public const int GL_SECONDARY_INTERPOLATOR_ATI = 0x0000896d;
        public const int GL_MAP2_NORMAL = 0x00000db2;
        public const int GL_OP_SET_LT_EXT = 0x0000878d;
        public const int GL_REG_28_ATI = 0x0000893d;
        public const int GL_PIXEL_PACK_BUFFER_EXT = 0x000088eb;
        public const int GL_BLUE_BIAS = 0x00000d1b;
        public const int GL_MATRIX9_ARB = 0x000088c9;
        public const int GL_MATRIX30_ARB = 0x000088de;
        public const int GL_VERTEX_ARRAY_RANGE_LENGTH_APPLE = 0x0000851e;
        public const int GL_PIXEL_MAP_I_TO_R = 0x00000c72;
        public const int GL_T2F_C4F_N3F_V3F = 0x00002a2c;
        public const int GL_BITMAP = 0x00001a00;
        public const int GL_VALIDATE_STATUS = 0x00008b83;
        public const int GL_PIXEL_MAP_I_TO_I = 0x00000c70;
        public const int GL_PIXEL_MIN_FILTER_EXT = 0x00008332;
        public const int GL_SOURCE2_ALPHA_EXT = 0x0000858a;
        public const int GL_ASYNC_MARKER_SGIX = 0x00008329;
        public const int GL_OBJECT_LINK_STATUS_ARB = 0x00008b82;
        public const int GL_OFFSET_TEXTURE_2D_MATRIX_NV = 0x000086e1;
        public const int GL_PIXEL_MAP_I_TO_A = 0x00000c75;
        public const int GL_PIXEL_MAP_I_TO_B = 0x00000c74;
        public const int GL_PROXY_POST_COLOR_MATRIX_COLOR_TABLE = 0x000080d5;
        public const int GL_PIXEL_MAP_I_TO_G = 0x00000c73;
        public const int GL_ELEMENT_ARRAY_APPLE = 0x00008768;
        public const int GL_TEXTURE_CLIPMAP_VIRTUAL_DEPTH_SGIX = 0x00008174;
        public const int GL_TEXTURE3_ARB = 0x000084c3;
        public const int GL_LINEAR_DETAIL_ALPHA_SGIS = 0x00008098;
        public const int GL_POST_CONVOLUTION_COLOR_TABLE_SGI = 0x000080d1;
        public const int GL_MAX_VERTEX_ATTRIBS = 0x00008869;
        public const int GL_LINEAR_ATTENUATION = 0x00001208;
        public const int GL_SAMPLER_2D_SHADOW_ARB = 0x00008b62;
        public const int GL_SGIX_async_histogram = 0x00000001;
        public const int GL_CMYKA_EXT = 0x0000800d;
        public const int GL_MAP1_TEXTURE_COORD_1 = 0x00000d93;
        public const int GL_MAP1_TEXTURE_COORD_2 = 0x00000d94;
        public const int GL_MAP1_TEXTURE_COORD_3 = 0x00000d95;
        public const int GL_VERTEX_PROGRAM_TWO_SIDE_ARB = 0x00008643;
        public const int GL_MODELVIEW0_MATRIX_EXT = 0x00000ba6;
        public const int GL_SAMPLE_MASK_VALUE_EXT = 0x000080aa;
        public const int GL_TEXTURE22 = 0x000084d6;
        public const int GL_TEXTURE23 = 0x000084d7;
        public const int GL_TEXTURE20 = 0x000084d4;
        public const int GL_TEXTURE21 = 0x000084d5;
        public const int GL_TEXTURE26 = 0x000084da;
        public const int GL_TEXTURE27 = 0x000084db;
        public const int GL_TEXTURE24 = 0x000084d8;
        public const int GL_TEXTURE25 = 0x000084d9;
        public const int GL_TEXTURE28 = 0x000084dc;
        public const int GL_TEXTURE29 = 0x000084dd;
        public const int GL_DEPTH_COMPONENT16_SGIX = 0x000081a5;
        public const int GL_PIXEL_TILE_GRID_WIDTH_SGIX = 0x00008142;
        public const int GL_ALPHA_MIN_SGIX = 0x00008320;
        public const int GL_SMOOTH_POINT_SIZE_GRANULARITY = 0x00000b13;
        public const int GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS = 0x00008b4d;
        public const int GL_MAT_SHININESS_BIT_PGI = 0x02000000;
        public const int GL_COLOR_INDEX16_EXT = 0x000080e7;
        public const int GL_MAX_TEXTURE_STACK_DEPTH = 0x00000d39;
        public const int GL_COLOR_ATTACHMENT13_EXT = 0x00008ced;
        public const int GL_OBJECT_BUFFER_USAGE_ATI = 0x00008765;
        public const int GL_PN_TRIANGLES_POINT_MODE_CUBIC_ATI = 0x000087f6;
        public const int GL_MAP1_VERTEX_ATTRIB1_4_NV = 0x00008661;
        public const int GL_REG_5_ATI = 0x00008926;
        public const int GL_PIXEL_TILE_CACHE_INCREMENT_SGIX = 0x0000813f;
        public const int GL_VIBRANCE_SCALE_NV = 0x00008713;
        public const int GL_MAP1_NORMAL = 0x00000d92;
        public const int GL_READ_BUFFER = 0x00000c02;
        public const int GL_POLYGON_BIT = 0x00000008;
        public const int GL_DEPTH_CLAMP_NV = 0x0000864f;
        public const int GL_TEXTURE16_ARB = 0x000084d0;
        public const int GL_ONE_MINUS_CONSTANT_ALPHA = 0x00008004;
        public const int GL_PACK_SUBSAMPLE_RATE_SGIX = 0x000085a0;
        public const int GL_MAP2_VERTEX_ATTRIB3_4_NV = 0x00008673;
        public const int GL_TEXTURE_4D_BINDING_SGIS = 0x0000814f;
        public const int GL_MAX_FRAGMENT_UNIFORM_COMPONENTS_ARB = 0x00008b49;
        public const int GL_DEPTH_BOUNDS_TEST_EXT = 0x00008890;
        public const int GL_QUERY_COUNTER_BITS_ARB = 0x00008864;
        public const int GL_OBJECT_ACTIVE_UNIFORM_MAX_LENGTH_ARB = 0x00008b87;
        public const int GL_VERTEX_STREAM4_ATI = 0x00008770;
        public const int GL_ALL_COMPLETED_NV = 0x000084f2;
        public const int GL_V3F = 0x00002a21;
        public const int GL_R1UI_T2F_N3F_V3F_SUN = 0x000085ca;
        public const int GL_RED_BITS = 0x00000d52;
        public const int GL_CONSTANT = 0x00008576;
        public const int GL_LINE_STIPPLE_REPEAT = 0x00000b26;
        public const int GL_COLOR_ATTACHMENT10_EXT = 0x00008cea;
        public const int GL_UNSIGNED_SHORT_8_8_REV_APPLE = 0x000085bb;
        public const int GL_SAMPLES_PASSED = 0x00008914;
        public const int GL_COLOR_ARRAY_LIST_STRIDE_IBM = 0x000192aa;
        public const int GL_BINORMAL_ARRAY_TYPE_EXT = 0x00008440;
        public const int GL_COLOR_TABLE_SCALE = 0x000080d6;
        public const int GL_REPLACE = 0x00001e01;
        public const int GL_COMBINER7_NV = 0x00008557;
        public const int GL_422_REV_EXT = 0x000080cd;
        public const int GL_PHONG_WIN = 0x000080ea;
        public const int GL_INVALID_ENUM = 0x00000500;
        public const int GL_EXT_cmyka = 0x00000001;
        public const int GL_DOT_PRODUCT_DEPTH_REPLACE_NV = 0x000086ed;
        public const int GL_PROGRAM_PARAMETER_NV = 0x00008644;
        public const int GL_DEPENDENT_GB_TEXTURE_2D_NV = 0x000086ea;
        public const int GL_TEXTURE_BINDING_3D = 0x0000806a;
        public const int GL_PIXEL_UNPACK_BUFFER_BINDING_EXT = 0x000088ef;
        public const int GL_QUERY_RESULT = 0x00008866;
        public const int GL_LUMINANCE16_EXT = 0x00008042;
        public const int GL_REG_29_ATI = 0x0000893e;
        public const int GL_PER_STAGE_CONSTANTS_NV = 0x00008535;
        public const int GL_POINT_SIZE_MIN_SGIS = 0x00008126;
        public const int GL_DEPTH_FUNC = 0x00000b74;
        public const int GL_NICEST = 0x00001102;
        public const int GL_TEXTURE_GEN_S = 0x00000c60;
        public const int GL_TEXTURE_GEN_Q = 0x00000c63;
        public const int GL_FOG_COORDINATE_ARRAY_BUFFER_BINDING_ARB = 0x0000889d;
        public const int GL_TEXTURE_ENV_BIAS_SGIX = 0x000080be;
        public const int GL_FOG_OFFSET_VALUE_SGIX = 0x00008199;
        public const int GL_COMBINE_RGB = 0x00008571;
        public const int GL_POINT_SPRITE = 0x00008861;
        public const int GL_Z_EXT = 0x000087d7;
        public const int GL_CLAMP_FRAGMENT_COLOR_ARB = 0x0000891b;
        public const int GL_BLEND_SRC_RGB_EXT = 0x000080c9;
        public const int GL_ALWAYS_FAST_HINT_PGI = 0x0001a20c;
        public const int GL_FRAGMENT_SHADER_ATI = 0x00008920;
        public const int GL_EDGE_FLAG_ARRAY_BUFFER_BINDING = 0x0000889b;
        public const int GL_SAMPLE_ALPHA_TO_COVERAGE = 0x0000809e;
        public const int GL_UNSIGNED_SHORT_5_6_5_REV = 0x00008364;
        public const int GL_REG_20_ATI = 0x00008935;
        public const int GL_STRICT_SCISSOR_HINT_PGI = 0x0001a218;
        public const int GL_SAMPLES_3DFX = 0x000086b4;
        public const int GL_COLOR_ARRAY = 0x00008076;
        public const int GL_MAP1_VERTEX_4 = 0x00000d98;
        public const int GL_CONVOLUTION_1D_EXT = 0x00008010;
        public const int GL_CURRENT_MATRIX_STACK_DEPTH_ARB = 0x00008640;
        public const int GL_MAX_COLOR_ATTACHMENTS_EXT = 0x00008cdf;
        public const int GL_PIXEL_MAP_A_TO_A_SIZE = 0x00000cb9;
        public const int GL_CON_6_ATI = 0x00008947;
        public const int GL_OPERAND1_RGB = 0x00008591;
        public const int GL_TEXTURE_MAG_FILTER = 0x00002800;
        public const int GL_CURRENT_NORMAL = 0x00000b02;
        public const int GL_POST_CONVOLUTION_GREEN_SCALE_EXT = 0x0000801d;
        public const int GL_COLOR_INDEX2_EXT = 0x000080e3;
        public const int GL_TEXTURE_CUBE_MAP_POSITIVE_Y_ARB = 0x00008517;
        public const int GL_MAP1_VERTEX_ATTRIB10_4_NV = 0x0000866a;
        public const int GL_TRANSPOSE_MODELVIEW_MATRIX = 0x000084e3;
        public const int GL_TEXTURE_RESIDENT_EXT = 0x00008067;
        public const int GL_ACTIVE_UNIFORM_MAX_LENGTH = 0x00008b87;
        public const int GL_ARRAY_ELEMENT_LOCK_FIRST_EXT = 0x000081a8;
        public const int GL_FRONT_AND_BACK = 0x00000408;
        public const int GL_COLOR_TABLE_FORMAT = 0x000080d8;
        public const int GL_MAX_VERTEX_UNIFORM_COMPONENTS = 0x00008b4a;
        public const int GL_OFFSET_PROJECTIVE_TEXTURE_RECTANGLE_SCALE_NV = 0x00008853;
        public const int GL_HISTOGRAM_GREEN_SIZE_EXT = 0x00008029;
        public const int GL_INTENSITY16F_ARB = 0x0000881d;
        public const int GL_ADD_SIGNED = 0x00008574;
        public const int GL_PASS_THROUGH_NV = 0x000086e6;
        public const int GL_FRAGMENT_LIGHT6_SGIX = 0x00008412;
        public const int GL_RED_SCALE = 0x00000d14;
        public const int GL_COMPARE_R_TO_TEXTURE = 0x0000884e;
        public const int GL_VERTEX_PROGRAM_BINDING_NV = 0x0000864a;
        public const int GL_VERTEX_ATTRIB_ARRAY8_NV = 0x00008658;
        public const int GL_DOT4_ATI = 0x00008967;
        public const int GL_ATTRIB_ARRAY_POINTER_NV = 0x00008645;
        public const int GL_NEAREST_CLIPMAP_NEAREST_SGIX = 0x0000844d;
        public const int GL_DEPTH_SCALE = 0x00000d1e;

        //
        // Functions
        //


        [DllImport("opengl32.dll", EntryPoint = "glArrayElement"), SuppressUnmanagedCodeSecurity]
        public static extern void glArrayElement(int i);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, bool[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, byte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, short[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, int[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, float[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, double[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, string pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, IntPtr pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref sbyte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, sbyte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, sbyte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, sbyte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref ushort pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ushort[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ushort[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ushort[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref uint pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, uint[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, uint[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, uint[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref bool pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, bool[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, bool[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref byte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, byte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, byte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref short pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, short[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, short[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref int pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, int[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, int[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref float pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, float[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, float[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, ref double pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, double[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glColorPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorPointer(int size, int type, int stride, double[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glDisableClientState"), SuppressUnmanagedCodeSecurity]
        public static extern void glDisableClientState(int array);

        [DllImport("opengl32.dll", EntryPoint = "glDrawArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawArrays(int mode, int first, int count);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, bool[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, byte[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, short[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, int[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, float[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, double[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, string indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, IntPtr indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref sbyte indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, sbyte[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, sbyte[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, sbyte[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref ushort indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ushort[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ushort[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ushort[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref uint indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, uint[] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, uint[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, uint[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref bool indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, bool[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, bool[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref byte indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, byte[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, byte[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref short indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, short[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, short[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref int indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, int[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, int[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref float indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, float[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, float[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, ref double indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, double[,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(int mode, int count, int type, double[,,] indices);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, bool[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, byte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, short[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, int[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, float[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, double[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, string pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, IntPtr pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref sbyte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, sbyte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, sbyte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, sbyte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref ushort pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ushort[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ushort[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ushort[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref uint pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, uint[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, uint[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, uint[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref bool pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, bool[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, bool[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref byte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, byte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, byte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref short pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, short[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, short[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref int pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, int[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, int[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref float pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, float[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, float[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, ref double pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, double[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagPointer(int stride, double[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glEnableClientState"), SuppressUnmanagedCodeSecurity]
        public static extern void glEnableClientState(int array);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] bool[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] byte[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] short[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] double[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, IntPtr arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out bool arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out byte arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out short arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out double arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out sbyte arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] sbyte[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out ushort arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] ushort[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, out uint arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPointerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPointerv(int pname, [Out] uint[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, bool[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, byte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, short[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, int[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, float[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, double[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, string pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, IntPtr pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref sbyte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, sbyte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, sbyte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, sbyte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref ushort pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ushort[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ushort[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ushort[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref uint pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, uint[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, uint[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, uint[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref bool pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, bool[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, bool[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref byte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, byte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, byte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref short pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, short[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, short[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref int pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, int[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, int[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref float pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, float[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, float[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, ref double pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, double[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glIndexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexPointer(int type, int stride, double[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, bool[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, byte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, short[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, int[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, float[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, double[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, string pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, IntPtr pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref sbyte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, sbyte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, sbyte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, sbyte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref ushort pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ushort[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ushort[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ushort[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref uint pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, uint[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, uint[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, uint[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref bool pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, bool[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, bool[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref byte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, byte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, byte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref short pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, short[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, short[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref int pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, int[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, int[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref float pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, float[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, float[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, ref double pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, double[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glInterleavedArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glInterleavedArrays(int format, int stride, double[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, bool[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, byte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, short[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, int[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, float[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, double[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, string pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, IntPtr pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref sbyte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, sbyte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, sbyte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, sbyte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref ushort pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ushort[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ushort[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ushort[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref uint pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, uint[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, uint[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, uint[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref bool pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, bool[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, bool[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref byte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, byte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, byte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref short pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, short[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, short[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref int pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, int[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, int[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref float pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, float[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, float[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, ref double pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, double[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glNormalPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormalPointer(int type, int stride, double[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, bool[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, byte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, short[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, int[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, float[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, double[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, string pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, IntPtr pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref sbyte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, sbyte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, sbyte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, sbyte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref ushort pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ushort[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ushort[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ushort[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref uint pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, uint[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, uint[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, uint[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref bool pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, bool[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, bool[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref byte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, byte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, byte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref short pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, short[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, short[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref int pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, int[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, int[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref float pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, float[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, float[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, ref double pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, double[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoordPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoordPointer(int size, int type, int stride, double[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, bool[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, byte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, short[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, int[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, float[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, double[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, string pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, IntPtr pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref sbyte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, sbyte[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, sbyte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, sbyte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref ushort pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ushort[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ushort[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ushort[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref uint pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, uint[] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, uint[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, uint[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref bool pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, bool[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, bool[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref byte pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, byte[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, byte[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref short pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, short[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, short[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref int pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, int[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, int[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref float pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, float[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, float[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, ref double pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, double[,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glVertexPointer"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertexPointer(int size, int type, int stride, double[,,] pointer);

        [DllImport("opengl32.dll", EntryPoint = "glPolygonOffset"), SuppressUnmanagedCodeSecurity]
        public static extern void glPolygonOffset(float factor, float units);

        [DllImport("opengl32.dll", EntryPoint = "glCopyTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glCopyTexImage1D(int target, int level, int internalformat, int x, int y, int width, int border);

        [DllImport("opengl32.dll", EntryPoint = "glCopyTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glCopyTexImage2D(int target, int level, int internalformat, int x, int y, int width, int height, int border);

        [DllImport("opengl32.dll", EntryPoint = "glCopyTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glCopyTexSubImage1D(int target, int level, int xoffset, int x, int y, int width);

        [DllImport("opengl32.dll", EntryPoint = "glCopyTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glCopyTexSubImage2D(int target, int level, int xoffset, int yoffset, int x, int y, int width, int height);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, bool[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, byte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, short[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, int[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, float[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, double[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, string pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref sbyte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, sbyte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, sbyte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, sbyte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref ushort pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ushort[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ushort[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ushort[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref uint pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, uint[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, uint[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, uint[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref bool pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, bool[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, bool[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref byte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, byte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, byte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref short pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, short[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, short[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref int pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, int[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, int[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref float pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, float[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, float[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, ref double pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, double[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage1D(int target, int level, int xoffset, int width, int format, int type, double[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, bool[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, byte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, short[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, int[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, float[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, double[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, string pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref sbyte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, sbyte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, sbyte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, sbyte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref ushort pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ushort[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ushort[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ushort[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref uint pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, uint[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, uint[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, uint[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref bool pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, bool[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, bool[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref byte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, byte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, byte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref short pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, short[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, short[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref int pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, int[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, int[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref float pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, float[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, float[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, ref double pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, double[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexSubImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, double[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, ref int textures, out int residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, int[] textures, out int residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, ref uint textures, out int residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, uint[] textures, out int residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, ref int textures, [Out] int[] residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, int[] textures, [Out] int[] residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, ref uint textures, [Out] int[] residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, uint[] textures, [Out] int[] residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, ref int textures, out bool residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, int[] textures, out bool residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, ref uint textures, out bool residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, uint[] textures, out bool residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, ref int textures, [Out] bool[] residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, int[] textures, [Out] bool[] residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, ref uint textures, [Out] bool[] residences);

        [DllImport("opengl32.dll", EntryPoint = "glAreTexturesResident"), SuppressUnmanagedCodeSecurity]
        public static extern int glAreTexturesResident(int n, uint[] textures, [Out] bool[] residences);

        [DllImport("opengl32.dll", EntryPoint = "glBindTexture"), SuppressUnmanagedCodeSecurity]
        public static extern void glBindTexture(int target, int texture);

        [DllImport("opengl32.dll", EntryPoint = "glBindTexture"), SuppressUnmanagedCodeSecurity]
        public static extern void glBindTexture(int target, uint texture);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteTextures(int n, ref int textures);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteTextures(int n, int[] textures);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteTextures(int n, ref uint textures);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteTextures(int n, uint[] textures);

        [DllImport("opengl32.dll", EntryPoint = "glGenTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glGenTextures(int n, out int textures);

        [DllImport("opengl32.dll", EntryPoint = "glGenTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glGenTextures(int n, [Out] int[] textures);

        [DllImport("opengl32.dll", EntryPoint = "glGenTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glGenTextures(int n, out uint textures);

        [DllImport("opengl32.dll", EntryPoint = "glGenTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glGenTextures(int n, [Out] uint[] textures);

        [DllImport("opengl32.dll", EntryPoint = "glIsTexture"), SuppressUnmanagedCodeSecurity]
        public static extern int glIsTexture(int texture);

        [DllImport("opengl32.dll", EntryPoint = "glIsTexture"), SuppressUnmanagedCodeSecurity]
        public static extern int glIsTexture(uint texture);

        [DllImport("opengl32.dll", EntryPoint = "glPrioritizeTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glPrioritizeTextures(int n, ref int textures, ref float priorities);

        [DllImport("opengl32.dll", EntryPoint = "glPrioritizeTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glPrioritizeTextures(int n, int[] textures, ref float priorities);

        [DllImport("opengl32.dll", EntryPoint = "glPrioritizeTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glPrioritizeTextures(int n, ref uint textures, ref float priorities);

        [DllImport("opengl32.dll", EntryPoint = "glPrioritizeTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glPrioritizeTextures(int n, uint[] textures, ref float priorities);

        [DllImport("opengl32.dll", EntryPoint = "glPrioritizeTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glPrioritizeTextures(int n, ref int textures, float[] priorities);

        [DllImport("opengl32.dll", EntryPoint = "glPrioritizeTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glPrioritizeTextures(int n, int[] textures, float[] priorities);

        [DllImport("opengl32.dll", EntryPoint = "glPrioritizeTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glPrioritizeTextures(int n, ref uint textures, float[] priorities);

        [DllImport("opengl32.dll", EntryPoint = "glPrioritizeTextures"), SuppressUnmanagedCodeSecurity]
        public static extern void glPrioritizeTextures(int n, uint[] textures, float[] priorities);

        [DllImport("opengl32.dll", EntryPoint = "glIndexub"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexub(byte c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexubv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexubv(ref byte c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexubv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexubv(byte[] c);

        [DllImport("opengl32.dll", EntryPoint = "glPopClientAttrib"), SuppressUnmanagedCodeSecurity]
        public static extern void glPopClientAttrib();

        [DllImport("opengl32.dll", EntryPoint = "glPushClientAttrib"), SuppressUnmanagedCodeSecurity]
        public static extern void glPushClientAttrib(int mask);

        [DllImport("opengl32.dll", EntryPoint = "glPushClientAttrib"), SuppressUnmanagedCodeSecurity]
        public static extern void glPushClientAttrib(uint mask);

        [DllImport("opengl32.dll", EntryPoint = "glNewList"), SuppressUnmanagedCodeSecurity]
        public static extern void glNewList(int list, int mode);

        [DllImport("opengl32.dll", EntryPoint = "glNewList"), SuppressUnmanagedCodeSecurity]
        public static extern void glNewList(uint list, int mode);

        [DllImport("opengl32.dll", EntryPoint = "glEndList"), SuppressUnmanagedCodeSecurity]
        public static extern void glEndList();

        [DllImport("opengl32.dll", EntryPoint = "glCallList"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallList(int list);

        [DllImport("opengl32.dll", EntryPoint = "glCallList"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallList(uint list);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, bool[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, byte[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, short[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, int[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, float[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, double[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, string lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, IntPtr lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref sbyte lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, sbyte[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, sbyte[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, sbyte[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref ushort lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ushort[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ushort[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ushort[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref uint lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, uint[] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, uint[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, uint[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref bool lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, bool[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, bool[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref byte lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, byte[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, byte[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref short lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, short[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, short[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref int lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, int[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, int[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref float lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, float[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, float[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, ref double lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, double[,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glCallLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glCallLists(int n, int type, double[,,] lists);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteLists(int list, int range);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteLists"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteLists(uint list, int range);

        [DllImport("opengl32.dll", EntryPoint = "glGenLists"), SuppressUnmanagedCodeSecurity]
        public static extern int glGenLists(int range);

        // Buffer Object Functions
        [DllImport("opengl32.dll", EntryPoint = "glGenBuffers"), SuppressUnmanagedCodeSecurity]
        public static extern void glGenBuffers(int n, [Out] uint[] buffers);

        [DllImport("opengl32.dll", EntryPoint = "glBindBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glBindBuffer(int target, uint buffer);

        [DllImport("opengl32.dll", EntryPoint = "glBufferData"), SuppressUnmanagedCodeSecurity]
        public static extern void glBufferData(int target, IntPtr size, IntPtr data, int usage);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteBuffers"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteBuffers(int n, [In] uint[] buffers);

        // Drawing Functions
        [DllImport("opengl32.dll", EntryPoint = "glDrawElements"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawElements(uint mode, int count, int type, IntPtr indices);

        [DllImport("opengl32.dll", EntryPoint = "glDrawArrays"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawArrays(uint mode, int first, int count);

        // Shader Program Functions
        [DllImport("opengl32.dll", EntryPoint = "glCreateProgram"), SuppressUnmanagedCodeSecurity]
        public static extern uint glCreateProgram();

        [DllImport("opengl32.dll", EntryPoint = "glDeleteProgram"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteProgram(uint program);

        [DllImport("opengl32.dll", EntryPoint = "glLinkProgram"), SuppressUnmanagedCodeSecurity]
        public static extern void glLinkProgram(uint program);

        [DllImport("opengl32.dll", EntryPoint = "glUseProgram"), SuppressUnmanagedCodeSecurity]
        public static extern void glUseProgram(uint program);

        [DllImport("opengl32.dll", EntryPoint = "glAttachShader"), SuppressUnmanagedCodeSecurity]
        public static extern void glAttachShader(uint program, uint shader);

        [DllImport("opengl32.dll", EntryPoint = "glBindAttribLocation"), SuppressUnmanagedCodeSecurity]
        public static extern void glBindAttribLocation(uint program, uint index, string name);

        [DllImport("opengl32.dll", EntryPoint = "glGetProgramiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetProgramiv(uint program, int pname, [Out] out int param);

        [DllImport("opengl32.dll", EntryPoint = "glGetProgramInfoLog"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetProgramInfoLog(uint program, int bufSize, [Out] out int length, [Out] StringBuilder infoLog);

        // Shader Functions
        [DllImport("opengl32.dll", EntryPoint = "glCreateShader"), SuppressUnmanagedCodeSecurity]
        public static extern uint glCreateShader(uint type);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteShader"), SuppressUnmanagedCodeSecurity]
        public static extern void glDeleteShader(uint shader);

        [DllImport("opengl32.dll", EntryPoint = "glShaderSource"), SuppressUnmanagedCodeSecurity]
        public static extern void glShaderSource(uint shader, int count, string[] strings, int[] lengths);

        [DllImport("opengl32.dll", EntryPoint = "glCompileShader"), SuppressUnmanagedCodeSecurity]
        public static extern void glCompileShader(uint shader);

        [DllImport("opengl32.dll", EntryPoint = "glGetShaderiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetShaderiv(uint shader, int pname, [Out] out int param);

        [DllImport("opengl32.dll", EntryPoint = "glGetShaderInfoLog"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetShaderInfoLog(uint shader, int bufSize, [Out] out int length, [Out] StringBuilder infoLog);

        // Uniform/Attribute Functions
        [DllImport("opengl32.dll", EntryPoint = "glGetUniformLocation"), SuppressUnmanagedCodeSecurity]
        public static extern int glGetUniformLocation(uint program, string name);

        [DllImport("opengl32.dll", EntryPoint = "glUniform1f"), SuppressUnmanagedCodeSecurity]
        public static extern void glUniform1f(int location, float v0);

        [DllImport("opengl32.dll", EntryPoint = "glUniform1i"), SuppressUnmanagedCodeSecurity]
        public static extern void glUniform1i(int location, int v0);

        [DllImport("opengl32.dll", EntryPoint = "glUniform2f"), SuppressUnmanagedCodeSecurity]
        public static extern void glUniform2f(int location, float v0, float v1);

        [DllImport("opengl32.dll", EntryPoint = "glUniform3f"), SuppressUnmanagedCodeSecurity]
        public static extern void glUniform3f(int location, float v0, float v1, float v2);

        [DllImport("opengl32.dll", EntryPoint = "glUniform4f"), SuppressUnmanagedCodeSecurity]
        public static extern void glUniform4f(int location, float v0, float v1, float v2, float v3);

        [DllImport("opengl32.dll", EntryPoint = "glUniformMatrix4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glUniformMatrix4fv(int location, int count, bool transpose, [In] float[] value);

        [DllImport("opengl32.dll", EntryPoint = "glListBase"), SuppressUnmanagedCodeSecurity]
        public static extern void glListBase(int arg_base);

        [DllImport("opengl32.dll", EntryPoint = "glListBase"), SuppressUnmanagedCodeSecurity]
        public static extern void glListBase(uint arg_base);

        [DllImport("opengl32.dll", EntryPoint = "glBegin"), SuppressUnmanagedCodeSecurity]
        public static extern void glBegin(int mode);

        [DllImport("opengl32.dll", EntryPoint = "glBitmap"), SuppressUnmanagedCodeSecurity]
        public static extern void glBitmap(int width, int height, float xorig, float yorig, float xmove, float ymove, ref byte bitmap);

        [DllImport("opengl32.dll", EntryPoint = "glBitmap"), SuppressUnmanagedCodeSecurity]
        public static extern void glBitmap(int width, int height, float xorig, float yorig, float xmove, float ymove, byte[] bitmap);

        [DllImport("opengl32.dll", EntryPoint = "glColor3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3b(byte red, byte green, byte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3b(sbyte red, byte green, byte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3b(byte red, sbyte green, byte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3b(sbyte red, sbyte green, byte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3b(byte red, byte green, sbyte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3b(sbyte red, byte green, sbyte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3b(byte red, sbyte green, sbyte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3b(sbyte red, sbyte green, sbyte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3bv(ref byte v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3bv(byte[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3bv(ref sbyte v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3bv(sbyte[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3d"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3d(double red, double green, double blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3f"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3f(float red, float green, float blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3i"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3i(int red, int green, int blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3s"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3s(short red, short green, short blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ub"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ub(byte red, byte green, byte blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ubv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ubv(ref byte v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ubv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ubv(byte[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ui(int red, int green, int blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ui(uint red, int green, int blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ui(int red, uint green, int blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ui(uint red, uint green, int blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ui(int red, int green, uint blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ui(uint red, int green, uint blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ui(int red, uint green, uint blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3ui(uint red, uint green, uint blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3uiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3uiv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3uiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3uiv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3uiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3uiv(ref uint v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3uiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3uiv(uint[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3us(short red, short green, short blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3us(ushort red, short green, short blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3us(short red, ushort green, short blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3us(ushort red, ushort green, short blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3us(short red, short green, ushort blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3us(ushort red, short green, ushort blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3us(short red, ushort green, ushort blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3us(ushort red, ushort green, ushort blue);

        [DllImport("opengl32.dll", EntryPoint = "glColor3usv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3usv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3usv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3usv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3usv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3usv(ref ushort v);

        [DllImport("opengl32.dll", EntryPoint = "glColor3usv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor3usv(ushort[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(byte red, byte green, byte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(sbyte red, byte green, byte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(byte red, sbyte green, byte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(sbyte red, sbyte green, byte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(byte red, byte green, sbyte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(sbyte red, byte green, sbyte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(byte red, sbyte green, sbyte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(sbyte red, sbyte green, sbyte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(byte red, byte green, byte blue, sbyte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(sbyte red, byte green, byte blue, sbyte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(byte red, sbyte green, byte blue, sbyte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(sbyte red, sbyte green, byte blue, sbyte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(byte red, byte green, sbyte blue, sbyte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(sbyte red, byte green, sbyte blue, sbyte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(byte red, sbyte green, sbyte blue, sbyte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4b"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4b(sbyte red, sbyte green, sbyte blue, sbyte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4bv(ref byte v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4bv(byte[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4bv(ref sbyte v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4bv(sbyte[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4d"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4d(double red, double green, double blue, double alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4f"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4f(float red, float green, float blue, float alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4i"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4i(int red, int green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4s"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4s(short red, short green, short blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ub"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ub(byte red, byte green, byte blue, byte alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ubv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ubv(ref byte v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ubv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ubv(byte[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(int red, int green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(uint red, int green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(int red, uint green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(uint red, uint green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(int red, int green, uint blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(uint red, int green, uint blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(int red, uint green, uint blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(uint red, uint green, uint blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(int red, int green, int blue, uint alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(uint red, int green, int blue, uint alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(int red, uint green, int blue, uint alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(uint red, uint green, int blue, uint alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(int red, int green, uint blue, uint alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(uint red, int green, uint blue, uint alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(int red, uint green, uint blue, uint alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4ui"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4ui(uint red, uint green, uint blue, uint alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4uiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4uiv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4uiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4uiv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4uiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4uiv(ref uint v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4uiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4uiv(uint[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(short red, short green, short blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(ushort red, short green, short blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(short red, ushort green, short blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(ushort red, ushort green, short blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(short red, short green, ushort blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(ushort red, short green, ushort blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(short red, ushort green, ushort blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(ushort red, ushort green, ushort blue, short alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(short red, short green, short blue, ushort alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(ushort red, short green, short blue, ushort alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(short red, ushort green, short blue, ushort alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(ushort red, ushort green, short blue, ushort alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(short red, short green, ushort blue, ushort alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(ushort red, short green, ushort blue, ushort alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(short red, ushort green, ushort blue, ushort alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4us"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4us(ushort red, ushort green, ushort blue, ushort alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColor4usv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4usv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4usv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4usv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4usv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4usv(ref ushort v);

        [DllImport("opengl32.dll", EntryPoint = "glColor4usv"), SuppressUnmanagedCodeSecurity]
        public static extern void glColor4usv(ushort[] v);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlag"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlag(int flag);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlag"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlag(bool flag);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagv(ref int flag);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagv(int[] flag);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagv(ref bool flag);

        [DllImport("opengl32.dll", EntryPoint = "glEdgeFlagv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEdgeFlagv(bool[] flag);

        [DllImport("opengl32.dll", EntryPoint = "glEnd"), SuppressUnmanagedCodeSecurity]
        public static extern void glEnd();

        [DllImport("opengl32.dll", EntryPoint = "glIndexd"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexd(double c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexdv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexdv(ref double c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexdv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexdv(double[] c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexf"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexf(float c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexfv(ref float c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexfv(float[] c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexi"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexi(int c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexiv(ref int c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexiv(int[] c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexs"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexs(short c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexsv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexsv(ref short c);

        [DllImport("opengl32.dll", EntryPoint = "glIndexsv"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexsv(short[] c);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3b(byte nx, byte ny, byte nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3b(sbyte nx, byte ny, byte nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3b(byte nx, sbyte ny, byte nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3b(sbyte nx, sbyte ny, byte nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3b(byte nx, byte ny, sbyte nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3b(sbyte nx, byte ny, sbyte nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3b(byte nx, sbyte ny, sbyte nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3b"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3b(sbyte nx, sbyte ny, sbyte nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3bv(ref byte v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3bv(byte[] v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3bv(ref sbyte v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3bv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3bv(sbyte[] v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3d"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3d(double nx, double ny, double nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3f"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3f(float nx, float ny, float nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3i"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3i(int nx, int ny, int nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3s"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3s(short nx, short ny, short nz);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glNormal3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glNormal3sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2d"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2d(double x, double y);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2f"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2f(float x, float y);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2i"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2i(int x, int y);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2s"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2s(short x, short y);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos2sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos2sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3d"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3d(double x, double y, double z);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3f"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3f(float x, float y, float z);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3i"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3i(int x, int y, int z);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3s"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3s(short x, short y, short z);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos3sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4d"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4d(double x, double y, double z, double w);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4f"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4f(float x, float y, float z, float w);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4i"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4i(int x, int y, int z, int w);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4s"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4s(short x, short y, short z, short w);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glRasterPos4sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRasterPos4sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glRectd"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectd(double x1, double y1, double x2, double y2);

        [DllImport("opengl32.dll", EntryPoint = "glRectdv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectdv(ref double v1, ref double v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectdv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectdv(double[] v1, ref double v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectdv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectdv(ref double v1, double[] v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectdv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectdv(double[] v1, double[] v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectf"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectf(float x1, float y1, float x2, float y2);

        [DllImport("opengl32.dll", EntryPoint = "glRectfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectfv(ref float v1, ref float v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectfv(float[] v1, ref float v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectfv(ref float v1, float[] v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectfv(float[] v1, float[] v2);

        [DllImport("opengl32.dll", EntryPoint = "glRecti"), SuppressUnmanagedCodeSecurity]
        public static extern void glRecti(int x1, int y1, int x2, int y2);

        [DllImport("opengl32.dll", EntryPoint = "glRectiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectiv(ref int v1, ref int v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectiv(int[] v1, ref int v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectiv(ref int v1, int[] v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectiv(int[] v1, int[] v2);

        [DllImport("opengl32.dll", EntryPoint = "glRects"), SuppressUnmanagedCodeSecurity]
        public static extern void glRects(short x1, short y1, short x2, short y2);

        [DllImport("opengl32.dll", EntryPoint = "glRectsv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectsv(ref short v1, ref short v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectsv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectsv(short[] v1, ref short v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectsv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectsv(ref short v1, short[] v2);

        [DllImport("opengl32.dll", EntryPoint = "glRectsv"), SuppressUnmanagedCodeSecurity]
        public static extern void glRectsv(short[] v1, short[] v2);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1d"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1d(double s);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1f"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1f(float s);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1i"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1i(int s);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1s"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1s(short s);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord1sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord1sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2d"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2d(double s, double t);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2f"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2f(float s, float t);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2i"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2i(int s, int t);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2s"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2s(short s, short t);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord2sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3d"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3d(double s, double t, double r);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3f"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3f(float s, float t, float r);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3i"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3i(int s, int t, int r);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3s"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3s(short s, short t, short r);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord3sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4d"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4d(double s, double t, double r, double q);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4f"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4f(float s, float t, float r, float q);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4i"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4i(int s, int t, int r, int q);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4s"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4s(short s, short t, short r, short q);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord4sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexCoord4sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2d"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2d(double x, double y);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2f"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2f(float x, float y);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2i"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2i(int x, int y);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2s"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2s(short x, short y);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex2sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3d"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3d(double x, double y, double z);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3f"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3f(float x, float y, float z);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3i"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3i(int x, int y, int z);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3s"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3s(short x, short y, short z);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex3sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex3sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4d"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4d(double x, double y, double z, double w);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4dv(ref double v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4dv(double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4f"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4f(float x, float y, float z, float w);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4fv(ref float v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4fv(float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4i"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4i(int x, int y, int z, int w);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4iv(ref int v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4iv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4iv(int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4s"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4s(short x, short y, short z, short w);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4sv(ref short v);

        [DllImport("opengl32.dll", EntryPoint = "glVertex4sv"), SuppressUnmanagedCodeSecurity]
        public static extern void glVertex4sv(short[] v);

        [DllImport("opengl32.dll", EntryPoint = "glClipPlane"), SuppressUnmanagedCodeSecurity]
        public static extern void glClipPlane(int plane, ref double equation);

        [DllImport("opengl32.dll", EntryPoint = "glClipPlane"), SuppressUnmanagedCodeSecurity]
        public static extern void glClipPlane(int plane, double[] equation);

        [DllImport("opengl32.dll", EntryPoint = "glColorMaterial"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMaterial(int face, int mode);

        [DllImport("opengl32.dll", EntryPoint = "glCullFace"), SuppressUnmanagedCodeSecurity]
        public static extern void glCullFace(int mode);

        [DllImport("opengl32.dll", EntryPoint = "glFogf"), SuppressUnmanagedCodeSecurity]
        public static extern void glFogf(int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glFogfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glFogfv(int pname, ref float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glFogfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glFogfv(int pname, float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glFogi"), SuppressUnmanagedCodeSecurity]
        public static extern void glFogi(int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glFogiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glFogiv(int pname, ref int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glFogiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glFogiv(int pname, int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glFrontFace"), SuppressUnmanagedCodeSecurity]
        public static extern void glFrontFace(int mode);

        [DllImport("opengl32.dll", EntryPoint = "glHint"), SuppressUnmanagedCodeSecurity]
        public static extern void glHint(int target, int mode);

        [DllImport("opengl32.dll", EntryPoint = "glLightf"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightf(int light, int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glLightfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightfv(int light, int pname, ref float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glLightfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightfv(int light, int pname, float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glLighti"), SuppressUnmanagedCodeSecurity]
        public static extern void glLighti(int light, int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glLightiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightiv(int light, int pname, ref int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glLightiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightiv(int light, int pname, int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glLightModelf"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightModelf(int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glLightModelfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightModelfv(int pname, ref float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glLightModelfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightModelfv(int pname, float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glLightModeli"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightModeli(int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glLightModeliv"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightModeliv(int pname, ref int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glLightModeliv"), SuppressUnmanagedCodeSecurity]
        public static extern void glLightModeliv(int pname, int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glLineStipple"), SuppressUnmanagedCodeSecurity]
        public static extern void glLineStipple(int factor, short pattern);

        [DllImport("opengl32.dll", EntryPoint = "glLineStipple"), SuppressUnmanagedCodeSecurity]
        public static extern void glLineStipple(int factor, ushort pattern);

        [DllImport("opengl32.dll", EntryPoint = "glLineWidth"), SuppressUnmanagedCodeSecurity]
        public static extern void glLineWidth(float width);

        [DllImport("opengl32.dll", EntryPoint = "glMaterialf"), SuppressUnmanagedCodeSecurity]
        public static extern void glMaterialf(int face, int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glMaterialfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glMaterialfv(int face, int pname, ref float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glMaterialfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glMaterialfv(int face, int pname, float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glMateriali"), SuppressUnmanagedCodeSecurity]
        public static extern void glMateriali(int face, int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glMaterialiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glMaterialiv(int face, int pname, ref int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glMaterialiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glMaterialiv(int face, int pname, int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glPointSize"), SuppressUnmanagedCodeSecurity]
        public static extern void glPointSize(float size);

        [DllImport("opengl32.dll", EntryPoint = "glPolygonMode"), SuppressUnmanagedCodeSecurity]
        public static extern void glPolygonMode(int face, int mode);

        [DllImport("opengl32.dll", EntryPoint = "glPolygonStipple"), SuppressUnmanagedCodeSecurity]
        public static extern void glPolygonStipple(ref byte mask);

        [DllImport("opengl32.dll", EntryPoint = "glPolygonStipple"), SuppressUnmanagedCodeSecurity]
        public static extern void glPolygonStipple(byte[] mask);

        [DllImport("opengl32.dll", EntryPoint = "glScissor"), SuppressUnmanagedCodeSecurity]
        public static extern void glScissor(int x, int y, int width, int height);

        [DllImport("opengl32.dll", EntryPoint = "glShadeModel"), SuppressUnmanagedCodeSecurity]
        public static extern void glShadeModel(int mode);

        [DllImport("opengl32.dll", EntryPoint = "glTexParameterf"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexParameterf(int target, int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glTexParameterfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexParameterfv(int target, int pname, ref float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexParameterfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexParameterfv(int target, int pname, float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexParameteri"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexParameteri(int target, int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glTexParameteriv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexParameteriv(int target, int pname, ref int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexParameteriv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexParameteriv(int target, int pname, int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, bool[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, byte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, short[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, int[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, float[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, double[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, string pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref sbyte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, sbyte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, sbyte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, sbyte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref ushort pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ushort[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ushort[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ushort[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref uint pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, uint[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, uint[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, uint[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref bool pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, bool[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, bool[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref byte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, byte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, byte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref short pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, short[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, short[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref int pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, int[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, int[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref float pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, float[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, float[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, ref double pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, double[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage1D(int target, int level, int internalformat, int width, int border, int format, int type, double[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, bool[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, byte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, short[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, int[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, float[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, double[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, string pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref sbyte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, sbyte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, sbyte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, sbyte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref ushort pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ushort[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ushort[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ushort[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref uint pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, uint[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, uint[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, uint[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref bool pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, bool[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, bool[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref byte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, byte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, byte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref short pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, short[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, short[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref int pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, int[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, int[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref float pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, float[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, float[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, ref double pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, double[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, double[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexEnvf"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexEnvf(int target, int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glTexEnvfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexEnvfv(int target, int pname, ref float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexEnvfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexEnvfv(int target, int pname, float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexEnvi"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexEnvi(int target, int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glTexEnviv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexEnviv(int target, int pname, ref int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexEnviv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexEnviv(int target, int pname, int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexGend"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGend(int coord, int pname, double param);

        [DllImport("opengl32.dll", EntryPoint = "glTexGendv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGendv(int coord, int pname, ref double arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexGendv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGendv(int coord, int pname, double[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexGenf"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGenf(int coord, int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glTexGenfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGenfv(int coord, int pname, ref float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexGenfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGenfv(int coord, int pname, float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexGeni"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGeni(int coord, int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glTexGeniv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGeniv(int coord, int pname, ref int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glTexGeniv"), SuppressUnmanagedCodeSecurity]
        public static extern void glTexGeniv(int coord, int pname, int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glFeedbackBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glFeedbackBuffer(int size, int type, out float buffer);

        [DllImport("opengl32.dll", EntryPoint = "glFeedbackBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glFeedbackBuffer(int size, int type, [Out] float[] buffer);

        [DllImport("opengl32.dll", EntryPoint = "glSelectBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glSelectBuffer(int size, out int buffer);

        [DllImport("opengl32.dll", EntryPoint = "glSelectBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glSelectBuffer(int size, [Out] int[] buffer);

        [DllImport("opengl32.dll", EntryPoint = "glSelectBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glSelectBuffer(int size, out uint buffer);

        [DllImport("opengl32.dll", EntryPoint = "glSelectBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glSelectBuffer(int size, [Out] uint[] buffer);

        [DllImport("opengl32.dll", EntryPoint = "glRenderMode"), SuppressUnmanagedCodeSecurity]
        public static extern int glRenderMode(int mode);

        [DllImport("opengl32.dll", EntryPoint = "glInitNames"), SuppressUnmanagedCodeSecurity]
        public static extern void glInitNames();

        [DllImport("opengl32.dll", EntryPoint = "glLoadName"), SuppressUnmanagedCodeSecurity]
        public static extern void glLoadName(int name);

        [DllImport("opengl32.dll", EntryPoint = "glLoadName"), SuppressUnmanagedCodeSecurity]
        public static extern void glLoadName(uint name);

        [DllImport("opengl32.dll", EntryPoint = "glPassThrough"), SuppressUnmanagedCodeSecurity]
        public static extern void glPassThrough(float token);

        [DllImport("opengl32.dll", EntryPoint = "glPopName"), SuppressUnmanagedCodeSecurity]
        public static extern void glPopName();

        [DllImport("opengl32.dll", EntryPoint = "glPushName"), SuppressUnmanagedCodeSecurity]
        public static extern void glPushName(int name);

        [DllImport("opengl32.dll", EntryPoint = "glPushName"), SuppressUnmanagedCodeSecurity]
        public static extern void glPushName(uint name);

        [DllImport("opengl32.dll", EntryPoint = "glDrawBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawBuffer(int mode);

        [DllImport("opengl32.dll", EntryPoint = "glClear"), SuppressUnmanagedCodeSecurity]
        public static extern void glClear(int mask);

        [DllImport("opengl32.dll", EntryPoint = "glClear"), SuppressUnmanagedCodeSecurity]
        public static extern void glClear(uint mask);

        [DllImport("opengl32.dll", EntryPoint = "glClearAccum"), SuppressUnmanagedCodeSecurity]
        public static extern void glClearAccum(float red, float green, float blue, float alpha);

        [DllImport("opengl32.dll", EntryPoint = "glClearIndex"), SuppressUnmanagedCodeSecurity]
        public static extern void glClearIndex(float c);

        [DllImport("opengl32.dll", EntryPoint = "glClearColor"), SuppressUnmanagedCodeSecurity]
        public static extern void glClearColor(float red, float green, float blue, float alpha);

        [DllImport("opengl32.dll", EntryPoint = "glClearStencil"), SuppressUnmanagedCodeSecurity]
        public static extern void glClearStencil(int s);

        [DllImport("opengl32.dll", EntryPoint = "glClearDepth"), SuppressUnmanagedCodeSecurity]
        public static extern void glClearDepth(double depth);

        [DllImport("opengl32.dll", EntryPoint = "glStencilMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glStencilMask(int mask);

        [DllImport("opengl32.dll", EntryPoint = "glStencilMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glStencilMask(uint mask);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(int red, int green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(bool red, int green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(int red, bool green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(bool red, bool green, int blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(int red, int green, bool blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(bool red, int green, bool blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(int red, bool green, bool blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(bool red, bool green, bool blue, int alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(int red, int green, int blue, bool alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(bool red, int green, int blue, bool alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(int red, bool green, int blue, bool alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(bool red, bool green, int blue, bool alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(int red, int green, bool blue, bool alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(bool red, int green, bool blue, bool alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(int red, bool green, bool blue, bool alpha);

        [DllImport("opengl32.dll", EntryPoint = "glColorMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glColorMask(bool red, bool green, bool blue, bool alpha);

        [DllImport("opengl32.dll", EntryPoint = "glDepthMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glDepthMask(int flag);

        [DllImport("opengl32.dll", EntryPoint = "glDepthMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glDepthMask(bool flag);

        [DllImport("opengl32.dll", EntryPoint = "glIndexMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexMask(int mask);

        [DllImport("opengl32.dll", EntryPoint = "glIndexMask"), SuppressUnmanagedCodeSecurity]
        public static extern void glIndexMask(uint mask);

        [DllImport("opengl32.dll", EntryPoint = "glAccum"), SuppressUnmanagedCodeSecurity]
        public static extern void glAccum(int op, float value);

        [DllImport("opengl32.dll", EntryPoint = "glDisable"), SuppressUnmanagedCodeSecurity]
        public static extern void glDisable(int cap);

        [DllImport("opengl32.dll", EntryPoint = "glEnable"), SuppressUnmanagedCodeSecurity]
        public static extern void glEnable(int cap);

        [DllImport("opengl32.dll", EntryPoint = "glFinish"), SuppressUnmanagedCodeSecurity]
        public static extern void glFinish();

        [DllImport("opengl32.dll", EntryPoint = "glFlush"), SuppressUnmanagedCodeSecurity]
        public static extern void glFlush();

        [DllImport("opengl32.dll", EntryPoint = "glPopAttrib"), SuppressUnmanagedCodeSecurity]
        public static extern void glPopAttrib();

        [DllImport("opengl32.dll", EntryPoint = "glPushAttrib"), SuppressUnmanagedCodeSecurity]
        public static extern void glPushAttrib(int mask);

        [DllImport("opengl32.dll", EntryPoint = "glPushAttrib"), SuppressUnmanagedCodeSecurity]
        public static extern void glPushAttrib(uint mask);

        [DllImport("opengl32.dll", EntryPoint = "glMap1d"), SuppressUnmanagedCodeSecurity]
        public static extern void glMap1d(int target, double u1, double u2, int stride, int order, ref double points);

        [DllImport("opengl32.dll", EntryPoint = "glMap1d"), SuppressUnmanagedCodeSecurity]
        public static extern void glMap1d(int target, double u1, double u2, int stride, int order, double[] points);

        [DllImport("opengl32.dll", EntryPoint = "glMap1f"), SuppressUnmanagedCodeSecurity]
        public static extern void glMap1f(int target, float u1, float u2, int stride, int order, ref float points);

        [DllImport("opengl32.dll", EntryPoint = "glMap1f"), SuppressUnmanagedCodeSecurity]
        public static extern void glMap1f(int target, float u1, float u2, int stride, int order, float[] points);

        [DllImport("opengl32.dll", EntryPoint = "glMap2d"), SuppressUnmanagedCodeSecurity]
        public static extern void glMap2d(int target, double u1, double u2, int ustride, int uorder, double v1, double v2, int vstride, int vorder, ref double points);

        [DllImport("opengl32.dll", EntryPoint = "glMap2d"), SuppressUnmanagedCodeSecurity]
        public static extern void glMap2d(int target, double u1, double u2, int ustride, int uorder, double v1, double v2, int vstride, int vorder, double[] points);

        [DllImport("opengl32.dll", EntryPoint = "glMap2f"), SuppressUnmanagedCodeSecurity]
        public static extern void glMap2f(int target, float u1, float u2, int ustride, int uorder, float v1, float v2, int vstride, int vorder, ref float points);

        [DllImport("opengl32.dll", EntryPoint = "glMap2f"), SuppressUnmanagedCodeSecurity]
        public static extern void glMap2f(int target, float u1, float u2, int ustride, int uorder, float v1, float v2, int vstride, int vorder, float[] points);

        [DllImport("opengl32.dll", EntryPoint = "glMapGrid1d"), SuppressUnmanagedCodeSecurity]
        public static extern void glMapGrid1d(int un, double u1, double u2);

        [DllImport("opengl32.dll", EntryPoint = "glMapGrid1f"), SuppressUnmanagedCodeSecurity]
        public static extern void glMapGrid1f(int un, float u1, float u2);

        [DllImport("opengl32.dll", EntryPoint = "glMapGrid2d"), SuppressUnmanagedCodeSecurity]
        public static extern void glMapGrid2d(int un, double u1, double u2, int vn, double v1, double v2);

        [DllImport("opengl32.dll", EntryPoint = "glMapGrid2f"), SuppressUnmanagedCodeSecurity]
        public static extern void glMapGrid2f(int un, float u1, float u2, int vn, float v1, float v2);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord1d"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord1d(double u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord1dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord1dv(ref double u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord1dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord1dv(double[] u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord1f"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord1f(float u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord1fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord1fv(ref float u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord1fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord1fv(float[] u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord2d"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord2d(double u, double v);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord2dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord2dv(ref double u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord2dv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord2dv(double[] u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord2f"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord2f(float u, float v);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord2fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord2fv(ref float u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalCoord2fv"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalCoord2fv(float[] u);

        [DllImport("opengl32.dll", EntryPoint = "glEvalMesh1"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalMesh1(int mode, int i1, int i2);

        [DllImport("opengl32.dll", EntryPoint = "glEvalPoint1"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalPoint1(int i);

        [DllImport("opengl32.dll", EntryPoint = "glEvalMesh2"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalMesh2(int mode, int i1, int i2, int j1, int j2);

        [DllImport("opengl32.dll", EntryPoint = "glEvalPoint2"), SuppressUnmanagedCodeSecurity]
        public static extern void glEvalPoint2(int i, int j);

        [DllImport("opengl32.dll", EntryPoint = "glAlphaFunc"), SuppressUnmanagedCodeSecurity]
        public static extern void glAlphaFunc(int func, float arg_ref);

        [DllImport("opengl32.dll", EntryPoint = "glBlendFunc"), SuppressUnmanagedCodeSecurity]
        public static extern void glBlendFunc(int sfactor, int dfactor);

        [DllImport("opengl32.dll", EntryPoint = "glLogicOp"), SuppressUnmanagedCodeSecurity]
        public static extern void glLogicOp(int opcode);

        [DllImport("opengl32.dll", EntryPoint = "glStencilFunc"), SuppressUnmanagedCodeSecurity]
        public static extern void glStencilFunc(int func, int arg_ref, int mask);

        [DllImport("opengl32.dll", EntryPoint = "glStencilFunc"), SuppressUnmanagedCodeSecurity]
        public static extern void glStencilFunc(int func, int arg_ref, uint mask);

        [DllImport("opengl32.dll", EntryPoint = "glStencilOp"), SuppressUnmanagedCodeSecurity]
        public static extern void glStencilOp(int fail, int zfail, int zpass);

        [DllImport("opengl32.dll", EntryPoint = "glDepthFunc"), SuppressUnmanagedCodeSecurity]
        public static extern void glDepthFunc(int func);

        [DllImport("opengl32.dll", EntryPoint = "glPixelZoom"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelZoom(float xfactor, float yfactor);

        [DllImport("opengl32.dll", EntryPoint = "glPixelTransferf"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelTransferf(int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glPixelTransferi"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelTransferi(int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glPixelStoref"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelStoref(int pname, float param);

        [DllImport("opengl32.dll", EntryPoint = "glPixelStorei"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelStorei(int pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapfv(int map, int mapsize, ref float values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapfv(int map, int mapsize, float[] values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapuiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapuiv(int map, int mapsize, ref int values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapuiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapuiv(int map, int mapsize, int[] values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapuiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapuiv(int map, int mapsize, ref uint values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapuiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapuiv(int map, int mapsize, uint[] values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapusv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapusv(int map, int mapsize, ref short values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapusv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapusv(int map, int mapsize, short[] values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapusv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapusv(int map, int mapsize, ref ushort values);

        [DllImport("opengl32.dll", EntryPoint = "glPixelMapusv"), SuppressUnmanagedCodeSecurity]
        public static extern void glPixelMapusv(int map, int mapsize, ushort[] values);

        [DllImport("opengl32.dll", EntryPoint = "glReadBuffer"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadBuffer(int mode);

        [DllImport("opengl32.dll", EntryPoint = "glCopyPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glCopyPixels(int x, int y, int width, int height, int type);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] bool[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] byte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] short[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] int[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] float[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] double[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out bool pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out byte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out short pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out int pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out float pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out double pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out sbyte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] sbyte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out ushort pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] ushort[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, out uint pixels);

        [DllImport("opengl32.dll", EntryPoint = "glReadPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, [Out] uint[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, bool[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, byte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, short[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, int[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, float[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, double[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, string pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref sbyte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, sbyte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, sbyte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, sbyte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref ushort pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ushort[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ushort[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ushort[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref uint pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, uint[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, uint[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, uint[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref bool pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, bool[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, bool[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref byte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, byte[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, byte[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref short pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, short[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, short[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref int pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, int[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, int[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref float pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, float[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, float[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, ref double pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, double[,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glDrawPixels"), SuppressUnmanagedCodeSecurity]
        public static extern void glDrawPixels(int width, int height, int format, int type, double[,,] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetBooleanv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetBooleanv(int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetBooleanv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetBooleanv(int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetBooleanv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetBooleanv(int pname, out bool arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetBooleanv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetBooleanv(int pname, [Out] bool[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetClipPlane"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetClipPlane(int plane, out double equation);

        [DllImport("opengl32.dll", EntryPoint = "glGetClipPlane"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetClipPlane(int plane, [Out] double[] equation);

        [DllImport("opengl32.dll", EntryPoint = "glGetDoublev"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetDoublev(int pname, out double arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetDoublev"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetDoublev(int pname, [Out] double[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetError"), SuppressUnmanagedCodeSecurity]
        public static extern int glGetError();

        [DllImport("opengl32.dll", EntryPoint = "glGetFloatv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetFloatv(int pname, out float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetFloatv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetFloatv(int pname, [Out] float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetIntegerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetIntegerv(int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetIntegerv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetIntegerv(int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetLightfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetLightfv(int light, int pname, out float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetLightfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetLightfv(int light, int pname, [Out] float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetLightiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetLightiv(int light, int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetLightiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetLightiv(int light, int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetMapdv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMapdv(int target, int query, out double v);

        [DllImport("opengl32.dll", EntryPoint = "glGetMapdv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMapdv(int target, int query, [Out] double[] v);

        [DllImport("opengl32.dll", EntryPoint = "glGetMapfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMapfv(int target, int query, out float v);

        [DllImport("opengl32.dll", EntryPoint = "glGetMapfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMapfv(int target, int query, [Out] float[] v);

        [DllImport("opengl32.dll", EntryPoint = "glGetMapiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMapiv(int target, int query, out int v);

        [DllImport("opengl32.dll", EntryPoint = "glGetMapiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMapiv(int target, int query, [Out] int[] v);

        [DllImport("opengl32.dll", EntryPoint = "glGetMaterialfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMaterialfv(int face, int pname, out float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetMaterialfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMaterialfv(int face, int pname, [Out] float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetMaterialiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMaterialiv(int face, int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetMaterialiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetMaterialiv(int face, int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapfv(int map, out float values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapfv(int map, [Out] float[] values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapuiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapuiv(int map, out int values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapuiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapuiv(int map, [Out] int[] values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapuiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapuiv(int map, out uint values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapuiv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapuiv(int map, [Out] uint[] values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapusv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapusv(int map, out short values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapusv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapusv(int map, [Out] short[] values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapusv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapusv(int map, out ushort values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPixelMapusv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPixelMapusv(int map, [Out] ushort[] values);

        [DllImport("opengl32.dll", EntryPoint = "glGetPolygonStipple"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPolygonStipple(out byte mask);

        [DllImport("opengl32.dll", EntryPoint = "glGetPolygonStipple"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetPolygonStipple([Out] byte[] mask);

        [DllImport("opengl32.dll", EntryPoint = "glGetString"), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr glGetString(int name);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexEnvfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexEnvfv(int target, int pname, out float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexEnvfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexEnvfv(int target, int pname, [Out] float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexEnviv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexEnviv(int target, int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexEnviv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexEnviv(int target, int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexGendv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexGendv(int coord, int pname, out double arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexGendv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexGendv(int coord, int pname, [Out] double[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexGenfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexGenfv(int coord, int pname, out float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexGenfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexGenfv(int coord, int pname, [Out] float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexGeniv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexGeniv(int coord, int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexGeniv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexGeniv(int coord, int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] bool[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] byte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] short[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] int[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] float[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] double[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out bool pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out byte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out short pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out int pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out float pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out double pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out sbyte pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] sbyte[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out ushort pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] ushort[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, out uint pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexImage(int target, int level, int format, int type, [Out] uint[] pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexParameterfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexParameterfv(int target, int pname, out float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexParameterfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexParameterfv(int target, int pname, [Out] float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexParameteriv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexParameteriv(int target, int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexParameteriv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexParameteriv(int target, int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexLevelParameterfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexLevelParameterfv(int target, int level, int pname, out float arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexLevelParameterfv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexLevelParameterfv(int target, int level, int pname, [Out] float[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexLevelParameteriv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexLevelParameteriv(int target, int level, int pname, out int arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexLevelParameteriv"), SuppressUnmanagedCodeSecurity]
        public static extern void glGetTexLevelParameteriv(int target, int level, int pname, [Out] int[] arg_params);

        [DllImport("opengl32.dll", EntryPoint = "glIsEnabled"), SuppressUnmanagedCodeSecurity]
        public static extern int glIsEnabled(int cap);

        [DllImport("opengl32.dll", EntryPoint = "glIsList"), SuppressUnmanagedCodeSecurity]
        public static extern int glIsList(int list);

        [DllImport("opengl32.dll", EntryPoint = "glIsList"), SuppressUnmanagedCodeSecurity]
        public static extern int glIsList(uint list);

        [DllImport("opengl32.dll", EntryPoint = "glDepthRange"), SuppressUnmanagedCodeSecurity]
        public static extern void glDepthRange(double near, double far);

        [DllImport("opengl32.dll", EntryPoint = "glFrustum"), SuppressUnmanagedCodeSecurity]
        public static extern void glFrustum(double left, double right, double bottom, double top, double zNear, double zFar);

        [DllImport("opengl32.dll", EntryPoint = "glLoadIdentity"), SuppressUnmanagedCodeSecurity]
        public static extern void glLoadIdentity();

        [DllImport("opengl32.dll", EntryPoint = "glLoadMatrixf"), SuppressUnmanagedCodeSecurity]
        public static extern void glLoadMatrixf(ref float m);

        [DllImport("opengl32.dll", EntryPoint = "glLoadMatrixf"), SuppressUnmanagedCodeSecurity]
        public static extern void glLoadMatrixf(float[] m);

        [DllImport("opengl32.dll", EntryPoint = "glLoadMatrixd"), SuppressUnmanagedCodeSecurity]
        public static extern void glLoadMatrixd(ref double m);

        [DllImport("opengl32.dll", EntryPoint = "glLoadMatrixd"), SuppressUnmanagedCodeSecurity]
        public static extern void glLoadMatrixd(double[] m);

        [DllImport("opengl32.dll", EntryPoint = "glMatrixMode"), SuppressUnmanagedCodeSecurity]
        public static extern void glMatrixMode(int mode);

        [DllImport("opengl32.dll", EntryPoint = "glMultMatrixf"), SuppressUnmanagedCodeSecurity]
        public static extern void glMultMatrixf(ref float m);

        [DllImport("opengl32.dll", EntryPoint = "glMultMatrixf"), SuppressUnmanagedCodeSecurity]
        public static extern void glMultMatrixf(float[] m);

        [DllImport("opengl32.dll", EntryPoint = "glMultMatrixd"), SuppressUnmanagedCodeSecurity]
        public static extern void glMultMatrixd(ref double m);

        [DllImport("opengl32.dll", EntryPoint = "glMultMatrixd"), SuppressUnmanagedCodeSecurity]
        public static extern void glMultMatrixd(double[] m);

        [DllImport("opengl32.dll", EntryPoint = "glOrtho"), SuppressUnmanagedCodeSecurity]
        public static extern void glOrtho(double left, double right, double bottom, double top, double zNear, double zFar);

        [DllImport("opengl32.dll", EntryPoint = "glPopMatrix"), SuppressUnmanagedCodeSecurity]
        public static extern void glPopMatrix();

        [DllImport("opengl32.dll", EntryPoint = "glPushMatrix"), SuppressUnmanagedCodeSecurity]
        public static extern void glPushMatrix();

        [DllImport("opengl32.dll", EntryPoint = "glRotated"), SuppressUnmanagedCodeSecurity]
        public static extern void glRotated(double angle, double x, double y, double z);

        [DllImport("opengl32.dll", EntryPoint = "glRotatef"), SuppressUnmanagedCodeSecurity]
        public static extern void glRotatef(float angle, float x, float y, float z);

        [DllImport("opengl32.dll", EntryPoint = "glScaled"), SuppressUnmanagedCodeSecurity]
        public static extern void glScaled(double x, double y, double z);

        [DllImport("opengl32.dll", EntryPoint = "glScalef"), SuppressUnmanagedCodeSecurity]
        public static extern void glScalef(float x, float y, float z);

        [DllImport("opengl32.dll", EntryPoint = "glTranslated"), SuppressUnmanagedCodeSecurity]
        public static extern void glTranslated(double x, double y, double z);

        [DllImport("opengl32.dll", EntryPoint = "glTranslatef"), SuppressUnmanagedCodeSecurity]
        public static extern void glTranslatef(float x, float y, float z);

        [DllImport("opengl32.dll", EntryPoint = "glViewport"), SuppressUnmanagedCodeSecurity]
        public static extern void glViewport(int x, int y, int width, int height);
    }

    public static class Glu
    {
        private const CallingConvention CALLING_CONVENTION = CallingConvention.Winapi;
        [StructLayout(LayoutKind.Sequential)]
        public struct GLUnurbs
        {
            /// <summary>
            ///     Keeps the struct from being garbage collected prematurely.
            /// </summary>
            private IntPtr Data;
        }
        public const int GLU_SAMPLING_TOLERANCE = 100203;
        [DllImport("glu32.dll", CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern GLUnurbs gluNewNurbsRenderer();
        [DllImport("glu32.dll", CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern void gluNurbsProperty([In] GLUnurbs nurb, int property, float val);
        [DllImport("glu32.dll", CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern void gluBeginCurve([In] GLUnurbs nurb);
        [DllImport("glu32.dll", CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern void gluNurbsCurve([In] GLUnurbs nurb, int knotCount, [In] float[] knots, int stride, [In] float[] control, int order, int type);
        [DllImport("glu32.dll", CallingConvention = CALLING_CONVENTION), CLSCompliant(false), SuppressUnmanagedCodeSecurity]
        public static extern void gluNurbsCurve([In] GLUnurbs nurb, int knotCount, [In] float[] knots, int stride, [In] float[,] control, int order, int type);
        [DllImport("glu32.dll", CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern void gluEndCurve([In] GLUnurbs nurb);
        [DllImport("glu32.dll", CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern void gluOrtho2D(double left, double right, double bottom, double top);
        [DllImport("glu32.dll", CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern void gluPickMatrix(double x, double y, double width, double height, [In] int[] viewport);
    }

    public static class User
    {
        private const CallingConvention CALLING_CONVENTION = CallingConvention.StdCall;
        private const string USER_NATIVE_LIBRARY = "user32.dll";
        [DllImport(USER_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION), SuppressUnmanagedCodeSecurity]
        public static extern bool ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);
        [DllImport(USER_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr GetDC(IntPtr windowHandle);
    }

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
        ///         When this flag is set, you must call <see cref="SetSystemPaletteUse" /> in
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
        ///         flag is set, <see cref="Wgl.wglSwapLayerBuffers" /> is supported.
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

    public static class Wgl
    {
        private const CallingConvention CALLING_CONVENTION = CallingConvention.StdCall;
        private const string WGL_NATIVE_LIBRARY = "opengl32.dll";
        public const int WGL_FONT_LINES = 0;
        public const int WGL_FONT_POLYGONS = 1;
        [DllImport(WGL_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool wglUseFontOutlines(IntPtr deviceContext, int first, int count, int listBase, float deviation, float extrusion, int format, [Out, MarshalAs(UnmanagedType.LPArray)] Gdi.GLYPHMETRICSFLOAT[] glyphMetrics);
        [DllImport(WGL_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool wglMakeCurrent(IntPtr deviceContext, IntPtr renderingContext);
        [DllImport(WGL_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool wglDeleteContext(IntPtr renderingContext);
        [DllImport(WGL_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr wglCreateContext(IntPtr deviceContext);
        [DllImport(WGL_NATIVE_LIBRARY), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr wglGetCurrentContext();
        [DllImport(WGL_NATIVE_LIBRARY, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern bool wglShareLists(IntPtr source, IntPtr destination);
        [DllImport(WGL_NATIVE_LIBRARY, CharSet = CharSet.Ansi, SetLastError = true), SuppressUnmanagedCodeSecurity]
        public static extern IntPtr wglGetProcAddress(string functionName);
    }
}