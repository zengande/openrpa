﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenRPA.Interfaces
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Security;

    [System.Security.SuppressUnmanagedCodeSecurity]
    public static class NativeMethods
    {
        public const int SW_FORCEMINIMIZE = 11; // Minimizes a window, even if the thread that owns the window is not responding. This flag should only be used when minimizing windows from a different thread.
        public const int SW_HIDE = 0; // Hides the window and activates another window.
        public const int SW_MAXIMIZE = 3; // Maximizes the specified window.
        public const int SW_MINIMIZE = 2; // Minimizes the specified window and activates the next top-level window in the Z order.
        public const int SW_RESTORE = 9; // Activates and displays the window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when restoring a minimized window.
        public const int SW_SHOW = 5; // Activates the window and displays it in its current size and position.
        public const int SW_SHOWDEFAULT = 10; // Sets the show state based on the SW_ value specified in the STARTUPINFO structure passed to the CreateProcess function by the program that started the application.
        public const int SW_SHOWMAXIMIZED = 3; // Activates the window and displays it as a maximized window.
        public const int SW_SHOWMINIMIZED = 2; // Activates the window and displays it as a minimized window.
        public const int SW_SHOWMINNOACTIVE = 7; // Displays the window as a minimized window. This value is similar to SW_SHOWMINIMIZED, except the window is not activated.
        public const int SW_SHOWNA = 8; // Displays the window in its current size and position. This value is similar to SW_SHOW, except that the window is not activated.
        public const int SW_SHOWNOACTIVATE = 4; // Displays a window in its most recent size and position. This value is similar to SW_SHOWNORMAL, except that the window is not activated.
        public const int SW_SHOWNORMAL = 1; // Activates and displays a window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when displaying the window for the first time.

        public static readonly int GWL_WNDPROC = -4; // Retrieves the address of the window procedure, or a handle representing the address of the window procedure. You must use the CallWindowProc function to call the window procedure.
        public static readonly int GWL_HINSTANCE = -6;
        public static readonly int GWL_HWNDPARENT = -8;
        public static readonly int GWL_STYLE = -16; // Retrieves the window styles.
        public static readonly int GWL_EXSTYLE = -20; // Retrieves the extended window styles.
        public static readonly int GWL_USERDATA = -21; // Gets the 32-bit value associated with the window. Each window has a corresponding 32-bit value intended for use by the application that created the window. Windows CE versions 2.0 and later support this value, but Windows CE versions 1.0 and 1.01 do not.
        public static readonly int GWL_ID = -12; // Not supported.

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        public static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);
        //public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        // This static method is required because legacy OSes do not support
        // SetWindowLongPtr
        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, (int)dwNewLong));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, long dwNewLong);
        [DllImport("user32")]
        public static extern int UpdateWindow(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetDoubleClickTime();
        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSDisconnectSession(IntPtr hServer, int sessionId, bool bWait);
        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSLogoffSession(IntPtr hServer, int SessionId, bool bWait);
        [DllImport("wtsapi32.dll")]
        public static extern void WTSFreeMemory(IntPtr pMemory);
        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern int WTSEnumerateSessions(
                System.IntPtr hServer,
                int Reserved,
                int Version,
                ref System.IntPtr ppSessionInfo,
                ref int pCount);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern IntPtr WTSOpenServer(string pServerName);


        [DllImport("wtsapi32.dll")]
        public static extern void WTSCloseServer(IntPtr hServer);
        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_SESSION_INFO
        {
            public Int32 SessionID;

            [MarshalAs(UnmanagedType.LPStr)]
            public String pWinStationName;

            public WTS_CONNECTSTATE_CLASS State;
        }
        public enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }
        public static bool Launch(System.Diagnostics.Process p, string CurrentDirectory, string appCmdLine)
        {
            var priToken = GetPrimaryToken(p);
            if (priToken == IntPtr.Zero) return false;
            IntPtr envBlock = IntPtr.Zero;
            try
            {
                if (!CreateEnvironmentBlock(out envBlock, priToken, false)) return false;
                if (!LaunchProcessAsUser(CurrentDirectory, appCmdLine, priToken, envBlock, out System.Diagnostics.Process newProcess, true, out string ErrorMessage))
                {
                    Log.Error(ErrorMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                throw;
            }
            finally
            {
                if (envBlock != IntPtr.Zero) DestroyEnvironmentBlock(envBlock);
                if(priToken != IntPtr.Zero) CloseHandle(priToken);
            }
            return true;
        }
        public static bool LaunchProcessAsUser(string CurrentDirectory, string cmdLine, IntPtr token, IntPtr envBlock, out System.Diagnostics.Process newProcess, bool AllowClosingProcess, out string ErrorMessage)
        {
            newProcess = null;
            ErrorMessage = null;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
            var pi = new PROCESS_INFORMATION();
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            var saProcess = new SECURITY_ATTRIBUTES();
            var saThread = new SECURITY_ATTRIBUTES();
            saProcess.nLength = uint.Parse(Marshal.SizeOf(saProcess).ToString());
            saThread.nLength = uint.Parse(Marshal.SizeOf(saThread).ToString());
            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "WinSta0\\Default";
            if (AllowClosingProcess) si.lpDesktop = "";
            si.dwFlags = (int)(STARTF.STARTF_USESHOWWINDOW | STARTF.STARTF_FORCEONFEEDBACK);
            si.wShowWindow = SW_SHOW;

            bool result = CreateProcessAsUser(token, null, cmdLine, ref saProcess, ref saThread, false,
    (uint)CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT, envBlock, CurrentDirectory, ref si, out pi);
            if (result == false)
            {
                var hResult = Marshal.GetLastWin32Error();
                ErrorMessage = String.Format("CreateProcessAsUser Error: {0}", hResult);
            }
            else
            {
                newProcess = System.Diagnostics.Process.GetProcessById(pi.dwProcessId);
            }
            return result;
        }
        public static string GetDomainName() => System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
        public static string GetHostName() => System.Net.Dns.GetHostName();
        public static string GetFQDN()
        {
            string domainName = GetDomainName();
            string hostName = GetHostName().ToLower();
            if (!hostName.EndsWith(domainName) && !string.IsNullOrEmpty(domainName)) // if hostname does not already include domain name
            {
                domainName = "." + domainName.ToLower();
                hostName += domainName; // add the domain name part
            }
            return hostName; // return the fully qualified name
        }
        public static bool MachineMemberOfDomain() => GetDomainName().Contains(".");
        public static bool IsAdmin(string username)
        {
            var wi = new System.Security.Principal.WindowsIdentity(username);
            var wp = new System.Security.Principal.WindowsPrincipal(wi);
            return wp.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        public static bool IsAdmin(IntPtr p_token)
        {
            var wi = new System.Security.Principal.WindowsIdentity(p_token);
            var wp = new System.Security.Principal.WindowsPrincipal(wi);
            return wp.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        public static bool IsDomainAdmin(IntPtr p_token)
        {
            var DomainName = GetDomainName();
            if (string.IsNullOrEmpty(DomainName)) return false;
            var d = System.DirectoryServices.ActiveDirectory.Domain.GetDomain(new System.DirectoryServices.ActiveDirectory.DirectoryContext(System.DirectoryServices.ActiveDirectory.DirectoryContextType.Domain, DomainName));
            var de = d.GetDirectoryEntry();
            byte[] domainSidArray = de.Properties["objectSid"].Value as byte[];
            var domainSid = new System.Security.Principal.SecurityIdentifier(domainSidArray, 0);
            var domainAdminsSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.AccountDomainAdminsSid, domainSid);

            var wi = new System.Security.Principal.WindowsIdentity(p_token);
            var wp = new System.Security.Principal.WindowsPrincipal(wi);
            return wp.IsInRole(domainAdminsSid);
        }
        public static IntPtr GetPrimaryToken(System.Diagnostics.Process p)
        {
            IntPtr primaryToken = IntPtr.Zero;
            //Gets impersonation token 
            bool retVal = OpenProcessToken(p.Handle, TOKEN_DUPLICATE, out IntPtr token);
            if (retVal == true)
            {
                SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                sa.nLength = (uint)Marshal.SizeOf(sa);
                //Convert the impersonation token into Primary token 
                retVal = DuplicateTokenEx(
                    token,
                    TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY,
                    ref sa,
                    (int)SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
                    (int)TOKEN_TYPE.TokenPrimary,
                    ref primaryToken);

                //Close the Token that was previously opened. 
                CloseHandle(token);
                if (retVal == false)
                {
                    string message = String.Format("DuplicateTokenEx Error: {0}", Marshal.GetLastWin32Error());
                    System.Diagnostics.Debug.WriteLine(message);
                }
            }
            else
            {
                string message = String.Format("OpenProcessToken Error: {0}", Marshal.GetLastWin32Error());
                System.Diagnostics.Debug.WriteLine(message);

            }
            //We'll Close this token after it is used. 
            return primaryToken;
        }
        public static System.Diagnostics.Process GetParentProcessId(System.Diagnostics.Process p = null)
        {
            if (p == null) p = System.Diagnostics.Process.GetCurrentProcess();
            NativeMethods.PROCESS_BASIC_INFORMATION pbi = new NativeMethods.PROCESS_BASIC_INFORMATION();

            //Get a handle to our own process
            IntPtr hProc = p.Handle; // OpenProcess((ProcessAccessFlags)0x001F0FFF, false, Process.GetCurrentProcess().Id);

            try
            {
                int queryStatus = NativeMethods.NtQueryInformationProcess(hProc, 0, ref pbi, pbi.Size, out int sizeInfoReturned);
            }
            finally
            {
                if (!hProc.Equals(IntPtr.Zero))
                {
                    //Close handle and free allocated memory
                    // CloseHandle(hProc);
                }
            }
            var pid = (int)pbi.InheritedFromUniqueProcessId;
            if (pid < 1) return null;
            return System.Diagnostics.Process.GetProcessById(pid);
        }
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out SafeTokenHandle TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(SafeTokenHandle TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);
        public static byte[] GetSIDByteArr(IntPtr processHandle)
        {
            int MAX_INTPTR_BYTE_ARR_SIZE = 512;
            byte[] sidBytes;

            // Get the Process Token
            if (!OpenProcessToken(processHandle, TOKEN_READ, out IntPtr tokenHandle))
                throw new ApplicationException("Could not get process token.  Win32 Error Code: " + Marshal.GetLastWin32Error());

            _ = GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenUser, IntPtr.Zero, 0, out uint tokenInfoLength);  // get the token info length
            IntPtr tokenInfo = Marshal.AllocHGlobal((int)tokenInfoLength);

            // Get the User SID
            if (GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenUser, tokenInfo, 0, out _))
            {
                TOKEN_USER tokenUser = (TOKEN_USER)Marshal.PtrToStructure(tokenInfo, typeof(TOKEN_USER));
                sidBytes = new byte[MAX_INTPTR_BYTE_ARR_SIZE];  // Since I don't yet know how to be more precise w/ the size of the byte arr, it is being set to 512
                Marshal.Copy(tokenUser.User.Sid, sidBytes, 0, MAX_INTPTR_BYTE_ARR_SIZE);  // get a byte[] representation of the SID
            }
            else throw new ApplicationException("Could not get process token.  Win32 Error Code: " + Marshal.GetLastWin32Error());

            return sidBytes;
        }
        // Using IntPtr for pSID insted of Byte[]
        [DllImport("advapi32", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool ConvertSidToStringSid(IntPtr pSID, out IntPtr ptrSid);
        [System.Runtime.InteropServices.DllImport("NTDLL.DLL", SetLastError = true)]
        public static extern int NtQueryInformationProcess(IntPtr hProcess, PROCESSINFOCLASS pic, ref PROCESS_BASIC_INFORMATION pbi, int cb, out int pSize);
        public static string GetProcessSID(System.Diagnostics.Process process = null)
        {
            if (process == null) process = System.Diagnostics.Process.GetCurrentProcess();
            SafeTokenHandle hToken;
            try
            {
                if (!NativeMethods.OpenProcessToken(process.Handle, NativeMethods.TOKEN_QUERY, out hToken))
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
            // first call gets lenght of TokenInformation
            _ = GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenUser, IntPtr.Zero, 0, out uint TokenInfLength);
            IntPtr TokenInformation = Marshal.AllocHGlobal(int.Parse(TokenInfLength.ToString()));
            if (GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenUser, TokenInformation, TokenInfLength, out _))
            {
                TOKEN_USER TokenUser = (TOKEN_USER)Marshal.PtrToStructure(TokenInformation, typeof(TOKEN_USER));
                _ = ConvertSidToStringSid(TokenUser.User.Sid, out IntPtr pstr);
                string sidstr = Marshal.PtrToStringAuto(pstr);
                LocalFree(pstr);
                return sidstr;
            }
            return "";
        }
        public static string GetProcessUserName(System.Diagnostics.Process process = null)
        {
            if (process == null) process = System.Diagnostics.Process.GetCurrentProcess();
            if (process.HasExited) return "";
            SafeTokenHandle hToken;
            try
            {
                if (!NativeMethods.OpenProcessToken(process.Handle, NativeMethods.TOKEN_QUERY, out hToken))
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
            // first call gets lenght of TokenInformation
            _ = GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenUser, IntPtr.Zero, 0, out uint TokenInfLength);
            IntPtr TokenInformation = Marshal.AllocHGlobal(int.Parse(TokenInfLength.ToString()));
            if (GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenUser, TokenInformation, TokenInfLength, out _))
            {
                TOKEN_USER TokenUser = (TOKEN_USER)Marshal.PtrToStructure(TokenInformation, typeof(TOKEN_USER));

                var a = new System.Security.Principal.SecurityIdentifier(TokenUser.User.Sid);
                return a.Translate(typeof(System.Security.Principal.NTAccount)).ToString();
            }
            return "";
        }
        [DllImport("kernel32.dll")]
        public static extern IntPtr LocalFree(IntPtr hMem);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern Boolean CloseHandle(IntPtr hObject);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern Boolean AllocConsole();
        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx", SetLastError = true)]
        private static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, ref SECURITY_ATTRIBUTES lpThreadAttributes, Int32 ImpersonationLevel,
            Int32 dwTokenType, ref IntPtr phNewToken);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsImmersiveProcess(IntPtr hWnd);

        // Win32 RECT
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // GetWindowRect gets the win32 RECT by a window handle.
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        // MoveWindow moves a window or changes its size based on a window handle.
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);


        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
    ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
    ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
        [DllImport("userenv.dll", SetLastError = true)]
        public static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);
        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
        [Flags]
        public enum ExitWindows : uint
        {
            // ONE of the following five:
            LogOff = 0x00,
            ShutDown = 0x01,
            Reboot = 0x02,
            PowerOff = 0x08,
            RestartApps = 0x40,
            // plus AT MOST ONE of the following two:
            Force = 0x04,
            ForceIfHung = 0x10,
        }
        [Flags]
        public enum ShutdownReason : uint
        {
            MajorApplication = 0x00040000,
            MajorHardware = 0x00010000,
            MajorLegacyApi = 0x00070000,
            MajorOperatingSystem = 0x00020000,
            MajorOther = 0x00000000,
            MajorPower = 0x00060000,
            MajorSoftware = 0x00030000,
            MajorSystem = 0x00050000,

            MinorBlueScreen = 0x0000000F,
            MinorCordUnplugged = 0x0000000b,
            MinorDisk = 0x00000007,
            MinorEnvironment = 0x0000000c,
            MinorHardwareDriver = 0x0000000d,
            MinorHotfix = 0x00000011,
            MinorHung = 0x00000005,
            MinorInstallation = 0x00000002,
            MinorMaintenance = 0x00000001,
            MinorMMC = 0x00000019,
            MinorNetworkConnectivity = 0x00000014,
            MinorNetworkCard = 0x00000009,
            MinorOther = 0x00000000,
            MinorOtherDriver = 0x0000000e,
            MinorPowerSupply = 0x0000000a,
            MinorProcessor = 0x00000008,
            MinorReconfig = 0x00000004,
            MinorSecurity = 0x00000013,
            MinorSecurityFix = 0x00000012,
            MinorSecurityFixUninstall = 0x00000018,
            MinorServicePack = 0x00000010,
            MinorServicePackUninstall = 0x00000016,
            MinorTermSrv = 0x00000020,
            MinorUnstable = 0x00000006,
            MinorUpgrade = 0x00000003,
            MinorWMI = 0x00000015,

            FlagUserDefined = 0x40000000,
            FlagPlanned = 0x80000000
        }

        public const int PROCESSBASICINFORMATION = 0;
        private static readonly uint STANDARD_RIGHTS_READ = 0x00020000;
        private static readonly uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private static readonly uint TOKEN_DUPLICATE = 0x0002;
        private static readonly uint TOKEN_QUERY = 0x0008;
        private static readonly uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
        //private static readonly uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        //private static readonly uint TOKEN_IMPERSONATE = 0x0004;
        //private static readonly uint TOKEN_QUERY_SOURCE = 0x0010;
        //private static readonly uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        //private static readonly uint TOKEN_ADJUST_GROUPS = 0x0040;
        //private static readonly uint TOKEN_ADJUST_DEFAULT = 0x0080;
        //private static readonly uint TOKEN_ADJUST_SESSIONID = 0x0100;
        //public const int SW_HIDE = 0;
        //public const int SW_SHOWNORMAL = 1;
        //public const int SW_SHOWMINIMIZED = 2;
        //public const int SW_SHOWMAXIMIZED = 3;
        //public const int SW_MINIMIZE = 6;
        //public const int SW_RESTORE = 9;
        //public const int SW_SHOWDEFAULT = 10;
        //public const int SW_SHOWMINNOACTIVE = 7;
        //public const int SW_SHOWNA = 8;
        //public const int SW_SHOWNOACTIVATE = 4;

        //public const int SW_NORMAL = 1;
        //public const int SW_MAXIMIZE = 3;
        //public const int SW_SHOW = 5;
        [Flags]
        enum CreateProcessFlags
        {
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NEW_CONSOLE = 0x00000010,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_NO_WINDOW = 0x08000000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_SEPARATE_WOW_VDM = 0x00000800,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            CREATE_SUSPENDED = 0x00000004,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            DEBUG_PROCESS = 0x00000001,
            DETACHED_PROCESS = 0x00000008,
            EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
            INHERIT_PARENT_AFFINITY = 0x00010000
        }
        [Flags]
        public enum STARTF : uint
        {
            STARTF_USESHOWWINDOW = 0x00000001,
            STARTF_USESIZE = 0x00000002,
            STARTF_USEPOSITION = 0x00000004,
            STARTF_USECOUNTCHARS = 0x00000008,
            STARTF_USEFILLATTRIBUTE = 0x00000010,
            STARTF_RUNFULLSCREEN = 0x00000020,  // ignored for non-x86 platforms
            STARTF_FORCEONFEEDBACK = 0x00000040,
            STARTF_FORCEOFFFEEDBACK = 0x00000080,
            STARTF_USESTDHANDLES = 0x00000100,
        }

        // This also works with CharSet.Ansi as long as the calling function uses the same character set.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        // This also works with CharSet.Ansi as long as the calling function uses the same character set.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }
        internal enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }
        internal enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_ATTRIBUTES
        {
            public uint nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct SID_AND_ATTRIBUTES
        {
            public IntPtr Sid;
            public int Attributes;
        }
        public struct TOKEN_USER
        {
            public SID_AND_ATTRIBUTES User;
        }
        public class SafeTokenHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeTokenHandle() : base(true) { }
            public SafeTokenHandle(IntPtr handle) : base(true)
            {
                SetHandle(handle);
            }
            protected override bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
        public struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public UIntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
            public int Size
            {
                get { return (int)System.Runtime.InteropServices.Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)); }
                // get { return (1 * sizeof(int) + 5 * IntPtr.Size); }
            }
        }
        public enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin
        }
        public enum PROCESSINFOCLASS : int
        {
            ProcessBasicInformation = 0, // 0, q: PROCESS_BASIC_INFORMATION, PROCESS_EXTENDED_BASIC_INFORMATION
            ProcessQuotaLimits, // qs: QUOTA_LIMITS, QUOTA_LIMITS_EX
            ProcessIoCounters, // q: IO_COUNTERS
            ProcessVmCounters, // q: VM_COUNTERS, VM_COUNTERS_EX
            ProcessTimes, // q: KERNEL_USER_TIMES
            ProcessBasePriority, // s: KPRIORITY
            ProcessRaisePriority, // s: ULONG
            ProcessDebugPort, // q: HANDLE
            ProcessExceptionPort, // s: HANDLE
            ProcessAccessToken, // s: PROCESS_ACCESS_TOKEN
            ProcessLdtInformation, // 10
            ProcessLdtSize,
            ProcessDefaultHardErrorMode, // qs: ULONG
            ProcessIoPortHandlers, // (kernel-mode only)
            ProcessPooledUsageAndLimits, // q: POOLED_USAGE_AND_LIMITS
            ProcessWorkingSetWatch, // q: PROCESS_WS_WATCH_INFORMATION[]; s: void
            ProcessUserModeIOPL,
            ProcessEnableAlignmentFaultFixup, // s: BOOLEAN
            ProcessPriorityClass, // qs: PROCESS_PRIORITY_CLASS
            ProcessWx86Information,
            ProcessHandleCount, // 20, q: ULONG, PROCESS_HANDLE_INFORMATION
            ProcessAffinityMask, // s: KAFFINITY
            ProcessPriorityBoost, // qs: ULONG
            ProcessDeviceMap, // qs: PROCESS_DEVICEMAP_INFORMATION, PROCESS_DEVICEMAP_INFORMATION_EX
            ProcessSessionInformation, // q: PROCESS_SESSION_INFORMATION
            ProcessForegroundInformation, // s: PROCESS_FOREGROUND_BACKGROUND
            ProcessWow64Information, // q: ULONG_PTR
            ProcessImageFileName, // q: UNICODE_STRING
            ProcessLUIDDeviceMapsEnabled, // q: ULONG
            ProcessBreakOnTermination, // qs: ULONG
            ProcessDebugObjectHandle, // 30, q: HANDLE
            ProcessDebugFlags, // qs: ULONG
            ProcessHandleTracing, // q: PROCESS_HANDLE_TRACING_QUERY; s: size 0 disables, otherwise enables
            ProcessIoPriority, // qs: ULONG
            ProcessExecuteFlags, // qs: ULONG
            ProcessResourceManagement,
            ProcessCookie, // q: ULONG
            ProcessImageInformation, // q: SECTION_IMAGE_INFORMATION
            ProcessCycleTime, // q: PROCESS_CYCLE_TIME_INFORMATION
            ProcessPagePriority, // q: ULONG
            ProcessInstrumentationCallback, // 40
            ProcessThreadStackAllocation, // s: PROCESS_STACK_ALLOCATION_INFORMATION, PROCESS_STACK_ALLOCATION_INFORMATION_EX
            ProcessWorkingSetWatchEx, // q: PROCESS_WS_WATCH_INFORMATION_EX[]
            ProcessImageFileNameWin32, // q: UNICODE_STRING
            ProcessImageFileMapping, // q: HANDLE (input)
            ProcessAffinityUpdateMode, // qs: PROCESS_AFFINITY_UPDATE_MODE
            ProcessMemoryAllocationMode, // qs: PROCESS_MEMORY_ALLOCATION_MODE
            ProcessGroupInformation, // q: USHORT[]
            ProcessTokenVirtualizationEnabled, // s: ULONG
            ProcessConsoleHostProcess, // q: ULONG_PTR
            ProcessWindowInformation, // 50, q: PROCESS_WINDOW_INFORMATION
            ProcessHandleInformation, // q: PROCESS_HANDLE_SNAPSHOT_INFORMATION // since WIN8
            ProcessMitigationPolicy, // s: PROCESS_MITIGATION_POLICY_INFORMATION
            ProcessDynamicFunctionTableInformation,
            ProcessHandleCheckingMode,
            ProcessKeepAliveCount, // q: PROCESS_KEEPALIVE_COUNT_INFORMATION
            ProcessRevokeFileHandles, // s: PROCESS_REVOKE_FILE_HANDLES_INFORMATION
            MaxProcessInfoClass
        };



        public static class LsaPrivateData
        {
            public static void RemoveAutologin()
            {
                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true);
                key.SetValue("AutoAdminLogon", "0");
                SetValue("DefaultPassword", null);
                var v = key.GetValue("AutoLogonCount");
                if (v != null)
                {
                    key.DeleteValue("AutoLogonCount");
                }
            }
            public static void SetAutologin(string Username, string Password, int AutoLogonCount)
            {
                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true);
                key.SetValue("AutoAdminLogon", "1");
                key.SetValue("DefaultUsername", Username);
                // key.SetValue("DefaultPassword", Password);
                SetValue("DefaultPassword", Password);
                if (AutoLogonCount > 0)
                {
                    key.SetValue("AutoLogonCount", AutoLogonCount);
                }
                else
                {
                    var v = key.GetValue("AutoLogonCount");
                    if (v != null)
                    {
                        key.DeleteValue("AutoLogonCount");
                    }
                }
            }

            [Flags]
            private enum CryptProtectFlags
            {
                // for remote-access situations where ui is not an option
                // if UI was specified on protect or unprotect operation, the call
                // will fail and GetLastError() will indicate ERROR_PASSWORD_RESTRICTION
                CRYPTPROTECT_UI_FORBIDDEN = 0x1,

                // per machine protected data -- any user on machine where CryptProtectData
                // took place may CryptUnprotectData
                CRYPTPROTECT_LOCAL_MACHINE = 0x4,

                // force credential synchronize during CryptProtectData()
                // Synchronize is only operation that occurs during this operation
                CRYPTPROTECT_CRED_SYNC = 0x8,

                // Generate an Audit on protect and unprotect operations
                CRYPTPROTECT_AUDIT = 0x10,

                // Protect data with a non-recoverable key
                CRYPTPROTECT_NO_RECOVERY = 0x20,


                // Verify the protection of a protected blob
                CRYPTPROTECT_VERIFY_PROTECTION = 0x40
            }
            [Flags]
            private enum CryptProtectPromptFlags
            {
                // prompt on unprotect
                CRYPTPROTECT_PROMPT_ON_UNPROTECT = 0x1,

                // prompt on protect
                CRYPTPROTECT_PROMPT_ON_PROTECT = 0x2
            }
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct CRYPTPROTECT_PROMPTSTRUCT
            {
                public int cbSize;
                public CryptProtectPromptFlags dwPromptFlags;
                public IntPtr hwndApp;
                public String szPrompt;
            }
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct DATA_BLOB
            {
                public int cbData;
                public IntPtr pbData;
            }
            [DllImport("Crypt32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, String szDataDescr, ref DATA_BLOB pOptionalEntropy,
                IntPtr pvReserved, ref CRYPTPROTECT_PROMPTSTRUCT pPromptStruct, CryptProtectFlags dwFlags, ref DATA_BLOB pDataOut);
            [DllImport("Crypt32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, StringBuilder szDataDescr, ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved,
                ref CRYPTPROTECT_PROMPTSTRUCT pPromptStruct, CryptProtectFlags dwFlags, ref DATA_BLOB pDataOut);
            public static void RemoveValue(string key)
            {
                SetValue(key, value: null);
            }

            public static void SetValue(string key, string value = null)
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key));

                if (key.Length == 0)
                    throw new ArgumentException($"{nameof(key)} must not be empty", nameof(key));

                var objectAttributes = new LSA_OBJECT_ATTRIBUTES();
                var localsystem = new LSA_UNICODE_STRING();
                var secretName = new LSA_UNICODE_STRING(key);

                var lusSecretData = !string.IsNullOrEmpty(value) ? new LSA_UNICODE_STRING(value) : default;

                var lsaPolicyHandle = GetLsaPolicy(ref objectAttributes, ref localsystem);

                var result = LsaStorePrivateData(lsaPolicyHandle, ref secretName, ref lusSecretData);
                ReleaseLsaPolicy(lsaPolicyHandle);

                var winErrorCode = LsaNtStatusToWinError(result);
                if (winErrorCode != 0)
                    throw new Win32Exception(winErrorCode, "StorePrivateData failed: " + winErrorCode);
            }

            public static string GetValue(string key)
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key));

                if (key.Length == 0)
                    throw new ArgumentException($"{nameof(key)} must not be empty", nameof(key));

                var objectAttributes = new LSA_OBJECT_ATTRIBUTES();
                var localsystem = new LSA_UNICODE_STRING();
                var secretName = new LSA_UNICODE_STRING(key);

                // Get LSA policy
                var lsaPolicyHandle = GetLsaPolicy(ref objectAttributes, ref localsystem);

                var result = LsaRetrievePrivateData(lsaPolicyHandle, ref secretName, out var privateData);
                ReleaseLsaPolicy(lsaPolicyHandle);

                if (result == STATUS_OBJECT_NAME_NOT_FOUND)
                    return null;

                var winErrorCode = LsaNtStatusToWinError(result);
                if (winErrorCode != 0)
                    throw new Win32Exception(winErrorCode, "LsaRetrievePrivateData failed: " + winErrorCode);

                if (privateData == IntPtr.Zero)
                    return null;

                var lusSecretData = Marshal.PtrToStructure<LSA_UNICODE_STRING>(privateData);
                var value = Marshal.PtrToStringAuto(lusSecretData.Buffer)?.Substring(0, lusSecretData.Length / UnicodeEncoding.CharSize);

                FreeMemory(privateData);

                return value;
            }

            private static IntPtr GetLsaPolicy(ref LSA_OBJECT_ATTRIBUTES objectAttributes, ref LSA_UNICODE_STRING localsystem)
            {
                var ntsResult = LsaOpenPolicy(ref localsystem, ref objectAttributes, (uint)LSA_AccessPolicy.POLICY_GET_PRIVATE_INFORMATION, out var lsaPolicyHandle);
                var winErrorCode = LsaNtStatusToWinError(ntsResult);
                if (winErrorCode != 0)
                    throw new Win32Exception(winErrorCode, "LsaOpenPolicy failed: " + winErrorCode);

                return lsaPolicyHandle;
            }

            private static void ReleaseLsaPolicy(IntPtr lsaPolicyHandle)
            {
                var ntsResult = LsaClose(lsaPolicyHandle);
                var winErrorCode = LsaNtStatusToWinError(ntsResult);
                if (winErrorCode != 0)
                    throw new Win32Exception(winErrorCode, "LsaClose failed: " + winErrorCode);
            }

            private static void FreeMemory(IntPtr buffer)
            {
                var ntsResult = LsaFreeMemory(buffer);
                var winErrorCode = LsaNtStatusToWinError(ntsResult);
                if (winErrorCode != 0)
                    throw new Win32Exception(winErrorCode, "LsaFreeMemory failed: " + winErrorCode);
            }

            private const uint STATUS_OBJECT_NAME_NOT_FOUND = 0xC0000034;

            [StructLayout(LayoutKind.Sequential)]
            private struct LSA_UNICODE_STRING
            {
                public LSA_UNICODE_STRING(string value)
                {
                    Buffer = Marshal.StringToHGlobalUni(value);
                    Length = (ushort)(value.Length * UnicodeEncoding.CharSize);
                    MaximumLength = (ushort)((value.Length + 1) * UnicodeEncoding.CharSize);
                }

                public ushort Length;
                public ushort MaximumLength;
                public IntPtr Buffer;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct LSA_OBJECT_ATTRIBUTES
            {
                public int Length;
                public IntPtr RootDirectory;
                public LSA_UNICODE_STRING ObjectName;
                public uint Attributes;
                public IntPtr SecurityDescriptor;
                public IntPtr SecurityQualityOfService;
            }

            private enum LSA_AccessPolicy : long
            {
                POLICY_VIEW_LOCAL_INFORMATION = 0x00000001L,
                POLICY_VIEW_AUDIT_INFORMATION = 0x00000002L,
                POLICY_GET_PRIVATE_INFORMATION = 0x00000004L,
                POLICY_TRUST_ADMIN = 0x00000008L,
                POLICY_CREATE_ACCOUNT = 0x00000010L,
                POLICY_CREATE_SECRET = 0x00000020L,
                POLICY_CREATE_PRIVILEGE = 0x00000040L,
                POLICY_SET_DEFAULT_QUOTA_LIMITS = 0x00000080L,
                POLICY_SET_AUDIT_REQUIREMENTS = 0x00000100L,
                POLICY_AUDIT_LOG_ADMIN = 0x00000200L,
                POLICY_SERVER_ADMIN = 0x00000400L,
                POLICY_LOOKUP_NAMES = 0x00000800L,
                POLICY_NOTIFICATION = 0x00001000L,
            }

            [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
            private static extern uint LsaRetrievePrivateData(IntPtr PolicyHandle, ref LSA_UNICODE_STRING KeyName, out IntPtr PrivateData);

            [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
            private static extern uint LsaStorePrivateData(IntPtr policyHandle, ref LSA_UNICODE_STRING KeyName, ref LSA_UNICODE_STRING PrivateData);

            [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
            private static extern uint LsaOpenPolicy(ref LSA_UNICODE_STRING SystemName, ref LSA_OBJECT_ATTRIBUTES ObjectAttributes, uint DesiredAccess, out IntPtr PolicyHandle);

            [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
            private static extern int LsaNtStatusToWinError(uint status);

            [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
            private static extern uint LsaClose(IntPtr policyHandle);

            [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
            private static extern uint LsaFreeMemory(IntPtr buffer);
        }



        public const Int32 HC_ACTION = 0;
        public const Int32 WH_KEYBOARD_LL = 13;
        public const Int32 WH_MOUSE_LL = 14;
        public delegate IntPtr LLProc(Int32 nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(Int32 idHook, LLProc lpfn, IntPtr hMod, UInt32 dwThreadId);
        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, Int32 nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern Boolean UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        public static extern Boolean GetPhysicalCursorPos(ref POINT pt);
        [DllImport("user32.dll")]
        public static extern Int16 GetKeyState(Int32 nVirtKey);
        public struct POINT
        {
            public int x;
            public int y;
        }


        public const Int32 VK_EXTENDED = 0x100;

        public const Int32 VK_LSHIFT = 0xA0;

        public const Int32 VK_RSHIFT = 0xA1;

        public const Int32 VK_LCONTROL = 0xA2;

        public const Int32 VK_RCONTROL = 0xA3;

        public const Int32 VK_LMENU = 0xA4;

        public const Int32 VK_RMENU = 0xA5;

        public const Int32 VK_LWIN = 0x5B;

        public const Int32 VK_RWIN = 0x5C;

        public const Int32 VK_PACKET = 0xE7;

        public const Int32 WM_KEYUP = 0x0101;

        public const Int32 WM_SYSKEYUP = 0x0105;

        public const Int32 WM_KEYDOWN = 0x0100;

        public const Int32 WM_SYSKEYDOWN = 0x0104;

        public const Int32 WM_LBUTTONUP = 0x0202;

        public const Int32 WM_LBUTTONDOWN = 0x0201;

        public const Int32 WM_MBUTTONUP = 0x0208;

        public const Int32 WM_MBUTTONDOWN = 0x0207;

        public const Int32 WM_MOUSEMOVE = 0x0200;

        public const Int32 WM_RBUTTONUP = 0x0205;

        public const Int32 WM_RBUTTONDOWN = 0x0204;

        public const Int32 LLKHF_EXTENDED = 0x01;
        public const Int32 WM_MouseWheel = 522;

        public abstract class WindowStyles
        {
            public const uint WS_OVERLAPPED = 0x00000000;
            public const uint WS_POPUP = 0x80000000;
            public const uint WS_CHILD = 0x40000000;
            public const uint WS_MINIMIZE = 0x20000000;
            public const uint WS_VISIBLE = 0x10000000;
            public const uint WS_DISABLED = 0x08000000;
            public const uint WS_CLIPSIBLINGS = 0x04000000;
            public const uint WS_CLIPCHILDREN = 0x02000000;
            public const uint WS_MAXIMIZE = 0x01000000;
            public const uint WS_CAPTION = 0x00C00000;     /* WS_BORDER | WS_DLGFRAME  */
            public const uint WS_BORDER = 0x00800000;
            public const uint WS_DLGFRAME = 0x00400000;
            public const uint WS_VSCROLL = 0x00200000;
            public const uint WS_HSCROLL = 0x00100000;
            public const uint WS_SYSMENU = 0x00080000;
            public const uint WS_THICKFRAME = 0x00040000;
            public const uint WS_GROUP = 0x00020000;
            public const uint WS_TABSTOP = 0x00010000;

            public const uint WS_MINIMIZEBOX = 0x00020000;
            public const uint WS_MAXIMIZEBOX = 0x00010000;

            public const uint WS_TILED = WS_OVERLAPPED;
            public const uint WS_ICONIC = WS_MINIMIZE;
            public const uint WS_SIZEBOX = WS_THICKFRAME;
            public const uint WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW;

            // Common Window Styles

            public const uint WS_OVERLAPPEDWINDOW =
                (WS_OVERLAPPED |
                  WS_CAPTION |
                  WS_SYSMENU |
                  WS_THICKFRAME |
                  WS_MINIMIZEBOX |
                  WS_MAXIMIZEBOX);

            public const uint WS_POPUPWINDOW =
                (WS_POPUP |
                  WS_BORDER |
                  WS_SYSMENU);

            public const uint WS_CHILDWINDOW = WS_CHILD;

            //Extended Window Styles

            public const uint WS_EX_DLGMODALFRAME = 0x00000001;
            public const uint WS_EX_NOPARENTNOTIFY = 0x00000004;
            public const uint WS_EX_TOPMOST = 0x00000008;
            public const uint WS_EX_ACCEPTFILES = 0x00000010;
            public const uint WS_EX_TRANSPARENT = 0x00000020;

            //#if(WINVER >= 0x0400)
            public const uint WS_EX_MDICHILD = 0x00000040;
            public const uint WS_EX_TOOLWINDOW = 0x00000080;
            public const uint WS_EX_WINDOWEDGE = 0x00000100;
            public const uint WS_EX_CLIENTEDGE = 0x00000200;
            public const uint WS_EX_CONTEXTHELP = 0x00000400;

            public const uint WS_EX_RIGHT = 0x00001000;
            public const uint WS_EX_LEFT = 0x00000000;
            public const uint WS_EX_RTLREADING = 0x00002000;
            public const uint WS_EX_LTRREADING = 0x00000000;
            public const uint WS_EX_LEFTSCROLLBAR = 0x00004000;
            public const uint WS_EX_RIGHTSCROLLBAR = 0x00000000;

            public const uint WS_EX_CONTROLPARENT = 0x00010000;
            public const uint WS_EX_STATICEDGE = 0x00020000;
            public const uint WS_EX_APPWINDOW = 0x00040000;

            public const uint WS_EX_OVERLAPPEDWINDOW = (WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE);
            public const uint WS_EX_PALETTEWINDOW = (WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
            //#endif /* WINVER >= 0x0400 */

            //#if(_WIN32_WINNT >= 0x0500)
            public const uint WS_EX_LAYERED = 0x00080000;
            //#endif /* _WIN32_WINNT >= 0x0500 */

            //#if(WINVER >= 0x0500)
            public const uint WS_EX_NOINHERITLAYOUT = 0x00100000; // Disable inheritence of mirroring by children
            public const uint WS_EX_LAYOUTRTL = 0x00400000; // Right to left mirroring
                                                            //#endif /* WINVER >= 0x0500 */

            //#if(_WIN32_WINNT >= 0x0500)
            public const uint WS_EX_COMPOSITED = 0x02000000;
            public const uint WS_EX_NOACTIVATE = 0x08000000;
            //#endif /* _WIN32_WINNT >= 0x0500 */
        }

        public struct KBDLLHOOKSTRUCT
        {
#pragma warning disable 649
            public UInt32 vkCode;
            public UInt32 scanCode;
            public LLKHF flags;
            public UInt32 time;
            public UIntPtr dwExtraInfo;
        }

        [Flags]
        public enum LLKHF : UInt32
        {
            LLKHF_EXTENDED = 0x01,
            LLKHF_INJECTED = 0x10,
            LLKHF_ALTDOWN = 0x20,
            LLKHF_UP = 0x80
        }

        public static Int32 LOBYTE(Int32 x) => x & 0xFF;

        public static Int32 HIBYTE(Int32 x) => x >> 8;

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "IDE1006")]
        //public static extern void mouse_event(long dwFlags, long dx, long dy, long cButtons, long dwExtraInfo);
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
            public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);
        /// <summary>
        /// Delegate declaration that matches WndProc signatures.
        /// </summary>
        public delegate IntPtr MessageHandler(WM uMsg, IntPtr wParam, IntPtr lParam, out bool handled);
        [DllImport("shell32.dll", EntryPoint = "CommandLineToArgvW", CharSet = CharSet.Unicode)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "IDE1006")]
        private static extern IntPtr _CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string cmdLine, out int numArgs);
        [DllImport("kernel32.dll", EntryPoint = "LocalFree", SetLastError = true)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "IDE1006")]
        private static extern IntPtr _LocalFree(IntPtr hMem);
        public static string[] CommandLineToArgvW(string cmdLine)
        {
            IntPtr argv = IntPtr.Zero;
            try
            {

                argv = _CommandLineToArgvW(cmdLine, out int numArgs);
                if (argv == IntPtr.Zero)
                {
                    throw new Win32Exception();
                }
                var result = new string[numArgs];

                for (int i = 0; i < numArgs; i++)
                {
                    IntPtr currArg = Marshal.ReadIntPtr(argv, i * Marshal.SizeOf(typeof(IntPtr)));
                    result[i] = Marshal.PtrToStringUni(currArg);
                }

                return result;
            }
            finally
            {
                _ = _LocalFree(argv);
                // Otherwise LocalFree failed.
                // Assert.AreEqual(IntPtr.Zero, p);
            }
        }
        public enum ErrorFlags
        {
            ERROR_INSUFFICIENT_BUFFER = 122,
            ERROR_INVALID_PARAMETER = 87,
            ERROR_NO_MORE_ITEMS = 259
        }
        public enum InternetFlags
        {
            INTERNET_COOKIE_HTTPONLY = 8192, //Requires IE 8 or higher   
            INTERNET_COOKIE_THIRD_PARTY = 131072,
            INTERNET_FLAG_RESTRICTED_ZONE = 16
        }
        [SuppressUnmanagedCodeSecurity, SecurityCritical, DllImport("wininet.dll", EntryPoint = "InternetGetCookieExW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        public static extern bool InternetGetCookieEx([In] string Url, [In] string cookieName, [Out] StringBuilder cookieData, [In, Out] ref uint pchCookieData, uint flags, IntPtr reserved);
        [DllImport("user32.dll")]
        public static extern UInt32 SendInput(UInt32 numberOfInputs, INPUT[] inputs, Int32 sizeOfInputStructure);
        [DllImport("user32.dll")]
        public static extern UInt16 VkKeyScanEx(Char ch, IntPtr dwhkl);
        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(UInt32 idThread);
        [DllImport("user32.dll")]
        public static extern Boolean SetPhysicalCursorPos(Int32 X, Int32 Y);
        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT
        {
            public const UInt32 INPUT_MOUSE = 0;

            public const UInt32 INPUT_KEYBOARD = 1;

            [FieldOffset(0)]
            public UInt32 type;

            [FieldOffset(4)]
            public MOUSEINPUT mi;

            [FieldOffset(4)]
            public KEYBDINPUT ki;
        }
        public struct MOUSEINPUT
        {
            public Int32 dx;
            public Int32 dy;
            public UInt32 mouseData;
            public MOUSEEVENTF dwFlags;
            public UInt32 time;
            public UIntPtr dwExtraInfo;
        }
        public struct KEYBDINPUT
        {
            public UInt16 wVk;
            public UInt16 wScan;
            public KEYEVENTF dwFlags;
            public UInt32 time;
            public UIntPtr dwExtraInfo;
        }
        [Flags]
        public enum KEYEVENTF : UInt32
        {
            KEYEVENTF_EXTENDEDKEY = 0x0001,
            KEYEVENTF_KEYUP = 0x0002,
            KEYEVENTF_UNICODE = 0x0004,
            KEYEVENTF_SCANCODE = 0x0008,
        }
        [Flags]
        public enum MOUSEEVENTF : UInt32
        {
            MOUSEEVENTF_ABSOLUTE = 0x8000,
            MOUSEEVENTF_HWHEEL = 0x01000,
            MOUSEEVENTF_MOVE = 0x0001,
            MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000,
            MOUSEEVENTF_LEFTDOWN = 0x0002,
            MOUSEEVENTF_LEFTUP = 0x0004,
            MOUSEEVENTF_RIGHTDOWN = 0x0008,
            MOUSEEVENTF_RIGHTUP = 0x0010,
            MOUSEEVENTF_MIDDLEDOWN = 0x0020,
            MOUSEEVENTF_MIDDLEUP = 0x0040,
            MOUSEEVENTF_VIRTUALDESK = 0x4000,
            MOUSEEVENTF_WHEEL = 0x0800,
            MOUSEEVENTF_XDOWN = 0x0080,
            MOUSEEVENTF_XUP = 0x0100
        }
    }

    public enum WM
    {
        NULL = 0x0000,
        CREATE = 0x0001,
        DESTROY = 0x0002,
        MOVE = 0x0003,
        SIZE = 0x0005,
        ACTIVATE = 0x0006,
        SETFOCUS = 0x0007,
        KILLFOCUS = 0x0008,
        ENABLE = 0x000A,
        SETREDRAW = 0x000B,
        SETTEXT = 0x000C,
        GETTEXT = 0x000D,
        GETTEXTLENGTH = 0x000E,
        PAINT = 0x000F,
        CLOSE = 0x0010,
        QUERYENDSESSION = 0x0011,
        QUIT = 0x0012,
        QUERYOPEN = 0x0013,
        ERASEBKGND = 0x0014,
        SYSCOLORCHANGE = 0x0015,
        SHOWWINDOW = 0x0018,
        ACTIVATEAPP = 0x001C,
        SETCURSOR = 0x0020,
        MOUSEACTIVATE = 0x0021,
        CHILDACTIVATE = 0x0022,
        QUEUESYNC = 0x0023,
        GETMINMAXINFO = 0x0024,

        WINDOWPOSCHANGING = 0x0046,
        WINDOWPOSCHANGED = 0x0047,

        CONTEXTMENU = 0x007B,
        STYLECHANGING = 0x007C,
        STYLECHANGED = 0x007D,
        DISPLAYCHANGE = 0x007E,
        GETICON = 0x007F,
        SETICON = 0x0080,
        NCCREATE = 0x0081,
        NCDESTROY = 0x0082,
        NCCALCSIZE = 0x0083,
        NCHITTEST = 0x0084,
        NCPAINT = 0x0085,
        NCACTIVATE = 0x0086,
        GETDLGCODE = 0x0087,
        SYNCPAINT = 0x0088,
        NCMOUSEMOVE = 0x00A0,
        NCLBUTTONDOWN = 0x00A1,
        NCLBUTTONUP = 0x00A2,
        NCLBUTTONDBLCLK = 0x00A3,
        NCRBUTTONDOWN = 0x00A4,
        NCRBUTTONUP = 0x00A5,
        NCRBUTTONDBLCLK = 0x00A6,
        NCMBUTTONDOWN = 0x00A7,
        NCMBUTTONUP = 0x00A8,
        NCMBUTTONDBLCLK = 0x00A9,

        SYSKEYDOWN = 0x0104,
        SYSKEYUP = 0x0105,
        SYSCHAR = 0x0106,
        SYSDEADCHAR = 0x0107,
        COMMAND = 0x0111,
        SYSCOMMAND = 0x0112,

        MOUSEMOVE = 0x0200,
        LBUTTONDOWN = 0x0201,
        LBUTTONUP = 0x0202,
        LBUTTONDBLCLK = 0x0203,
        RBUTTONDOWN = 0x0204,
        RBUTTONUP = 0x0205,
        RBUTTONDBLCLK = 0x0206,
        MBUTTONDOWN = 0x0207,
        MBUTTONUP = 0x0208,
        MBUTTONDBLCLK = 0x0209,
        MOUSEWHEEL = 0x020A,
        XBUTTONDOWN = 0x020B,
        XBUTTONUP = 0x020C,
        XBUTTONDBLCLK = 0x020D,
        MOUSEHWHEEL = 0x020E,


        CAPTURECHANGED = 0x0215,

        ENTERSIZEMOVE = 0x0231,
        EXITSIZEMOVE = 0x0232,

        IME_SETCONTEXT = 0x0281,
        IME_NOTIFY = 0x0282,
        IME_CONTROL = 0x0283,
        IME_COMPOSITIONFULL = 0x0284,
        IME_SELECT = 0x0285,
        IME_CHAR = 0x0286,
        IME_REQUEST = 0x0288,
        IME_KEYDOWN = 0x0290,
        IME_KEYUP = 0x0291,

        NCMOUSELEAVE = 0x02A2,

        DWMCOMPOSITIONCHANGED = 0x031E,
        DWMNCRENDERINGCHANGED = 0x031F,
        DWMCOLORIZATIONCOLORCHANGED = 0x0320,
        DWMWINDOWMAXIMIZEDCHANGE = 0x0321,

        #region Windows 7
        DWMSENDICONICTHUMBNAIL = 0x0323,
        DWMSENDICONICLIVEPREVIEWBITMAP = 0x0326,
        #endregion

        USER = 0x0400,

        // This is the hard-coded message value used by WinForms for Shell_NotifyIcon.
        // It's relatively safe to reuse.
        TRAYMOUSEMESSAGE = 0x800, //WM_USER + 1024
        APP = 0x8000,
    }

}
