using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace HansLaserDateSerialDemo
{
    internal enum MarkEndStatus
    {
        Running = 0,
        Normal = 1,
        Aborted = 2,
        DeviceError = 3
    }

    internal sealed class HansApiException : Exception
    {
        public int ErrorCode { get; private set; }
        public string Operation { get; private set; }

        public HansApiException(string operation, int errorCode)
            : base(operation + " 失败，错误码 " + errorCode + "：" + HansApi.DescribeError(errorCode))
        {
            Operation = operation;
            ErrorCode = errorCode;
        }
    }

    internal sealed class HansApi : IDisposable
    {
        private IntPtr _module;
        private bool _initialized;

        private readonly GetDllVersionDelegate _getDllVersion;
        private readonly InitialMachineDelegate _initialMachine;
        private readonly CloseMachineDelegate _closeMachine;
        private readonly LoadMarkFileDelegate _loadMarkFile;
        private readonly ChangeTextByNameWDelegate _changeTextW;
        private readonly ChangeTextByNameADelegate _changeTextA;
        private readonly MarkDelegate _mark;
        private readonly IsMarkEndDelegate _isMarkEnd;
        private readonly MarkStopDelegate _markStop;
        private readonly GetMarkTimeDelegate _getMarkTime;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDllVersionDelegate(out ushort mainVersion, out ushort dllVersion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int InitialMachineDelegate(IntPtr path);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CloseMachineDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int LoadMarkFileDelegate(IntPtr fileName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ChangeTextByNameWDelegate(IntPtr textNameAnsi, IntPtr textValueUnicode);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ChangeTextByNameADelegate(IntPtr textNameAnsi, IntPtr textValueAnsi);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MarkDelegate(
            int type,
            int waitTouch,
            int waitEnd,
            int overTimeMs,
            int markAll);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int IsMarkEndDelegate(out int flag);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MarkStopDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetMarkTimeDelegate(out uint markTimeMs);

        public HansApi(string dllPath)
        {
            if (string.IsNullOrWhiteSpace(dllPath))
                throw new ArgumentException("DLL 路径不能为空。", "dllPath");

            _module = LoadLibrary(dllPath);
            if (_module == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "无法加载 " + dllPath + "。请检查路径、依赖 DLL 和 x86/x64 位数。");
            }

            try
            {
                _getDllVersion = GetRequired<GetDllVersionDelegate>("HS_GetDllVersion", 8);
                _initialMachine = GetRequired<InitialMachineDelegate>("HS_InitialMachine", 4);
                _closeMachine = GetRequired<CloseMachineDelegate>("HS_CloseMachine", 0);
                _loadMarkFile = GetRequired<LoadMarkFileDelegate>("HS_LoadMarkFile", 4);

                // 文档提供 Unicode 版本；若某套旧 DLL 没导出 W 版本，则退回 ANSI 版本。
                _changeTextW = TryGet<ChangeTextByNameWDelegate>("HS_ChangeTextByNameW", 8);
                _changeTextA = TryGet<ChangeTextByNameADelegate>("HS_ChangeTextByName", 8);
                if (_changeTextW == null && _changeTextA == null)
                    throw new MissingMethodException("DLL 中未找到 HS_ChangeTextByNameW/HS_ChangeTextByName。");

                _mark = GetRequired<MarkDelegate>("HS_Mark", 20);
                _isMarkEnd = GetRequired<IsMarkEndDelegate>("HS_IsMarkEnd", 4);
                _markStop = GetRequired<MarkStopDelegate>("HS_MarkStop", 0);
                _getMarkTime = TryGet<GetMarkTimeDelegate>("HS_GetMarkTime", 4);
            }
            catch
            {
                FreeLibrary(_module);
                _module = IntPtr.Zero;
                throw;
            }
        }

        public string GetVersionText()
        {
            ushort main;
            ushort dll;
            Check("HS_GetDllVersion", _getDllVersion(out main, out dll));

            return "所需主程序 " + DecodeVersion(main) +
                   "；接口 DLL " + DecodeVersion(dll) +
                   "（原始值 " + main + "/" + dll + "）";
        }

        public void Initialize(string machinePath)
        {
            if (_initialized)
                return;

            IntPtr p = IntPtr.Zero;
            try
            {
                if (!string.IsNullOrWhiteSpace(machinePath))
                    p = Marshal.StringToHGlobalAnsi(machinePath);

                Check("HS_InitialMachine", _initialMachine(p));
                _initialized = true;
            }
            finally
            {
                if (p != IntPtr.Zero)
                    Marshal.FreeHGlobal(p);
            }
        }

        public void LoadTemplate(string templatePath)
        {
            EnsureInitialized();
            IntPtr p = Marshal.StringToHGlobalAnsi(templatePath);
            try
            {
                Check("HS_LoadMarkFile", _loadMarkFile(p));
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        public void SetVariableText(string alias, string value)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("可变文本别名不能为空。", "alias");

            IntPtr pAlias = Marshal.StringToHGlobalAnsi(alias);
            try
            {
                if (_changeTextW != null)
                {
                    IntPtr pValueW = Marshal.StringToHGlobalUni(value);
                    try
                    {
                        Check("HS_ChangeTextByNameW", _changeTextW(pAlias, pValueW));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pValueW);
                    }
                }
                else
                {
                    // 当前编号只包含 ASCII 字符，ANSI 回退不会丢失内容。
                    IntPtr pValueA = Marshal.StringToHGlobalAnsi(value);
                    try
                    {
                        Check("HS_ChangeTextByName", _changeTextA(pAlias, pValueA));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pValueA);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pAlias);
            }
        }

        public MarkEndStatus MarkAndWait(
            bool redLightPreview,
            bool waitForFootPedal,
            int footPedalTimeoutMs,
            int overallTimeoutMs)
        {
            EnsureInitialized();

            int type = redLightPreview ? 1 : 0;
            int rc = _mark(
                type,
                waitForFootPedal ? 1 : 0,
                0, // 非阻塞；由 HS_IsMarkEnd 判断实际结束状态
                footPedalTimeoutMs,
                1); // 全部打标
            Check("HS_Mark", rc);

            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                int flag;
                Check("HS_IsMarkEnd", _isMarkEnd(out flag));

                if (flag == (int)MarkEndStatus.Normal)
                    return MarkEndStatus.Normal;
                if (flag == (int)MarkEndStatus.Aborted)
                    return MarkEndStatus.Aborted;
                if (flag == (int)MarkEndStatus.DeviceError)
                    return MarkEndStatus.DeviceError;

                if (overallTimeoutMs > 0 && sw.ElapsedMilliseconds > overallTimeoutMs)
                {
                    try { _markStop(); } catch { }
                    throw new TimeoutException("等待打标结束超时，已调用 HS_MarkStop。流水号未确认完成。");
                }

                Thread.Sleep(50);
            }
        }

        public uint? TryGetLastMarkTimeMs()
        {
            if (_getMarkTime == null)
                return null;

            uint value;
            int rc = _getMarkTime(out value);
            if (rc != 0)
                return null;
            return value;
        }

        public void Dispose()
        {
            if (_initialized)
            {
                try { _closeMachine(); } catch { }
                _initialized = false;
            }

            if (_module != IntPtr.Zero)
            {
                FreeLibrary(_module);
                _module = IntPtr.Zero;
            }
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("控制系统尚未初始化。");
        }

        private static void Check(string operation, int rc)
        {
            if (rc != 0)
                throw new HansApiException(operation, rc);
        }

        public static string DescribeError(int code)
        {
            switch (code)
            {
                case 0: return "成功";
                case 1: return "另一个程序在运行；请关闭标准打标软件或校正软件";
                case 2: return "路径不正确";
                case 3: return "初始化失败";
                case 4: return "未初始化";
                case 5: return "设备报警";
                case 6: return "命令超时";
                case 7: return "无法读取文件";
                case 8: return "指定字体不存在";
                case 9: return "指定层号不存在";
                case 10: return "未找到指定对象/可变文本别名";
                case 11: return "参数非法";
                case 12: return "当前状态不允许执行此操作";
                case 13: return "分配内存失败";
                case 14: return "打标范围超限";
                case 15: return "缓冲区不足";
                case 16: return "空指针";
                case 17: return "未找到指定文档";
                case 100: return "未知错误";
                default: return "未定义错误";
            }
        }

        private static string DecodeVersion(ushort value)
        {
            int major = (value >> 12) & 0x0F;
            int minor = (value >> 7) & 0x1F;
            int patch = value & 0x7F;
            return "V" + major + "." + minor + "." + patch;
        }

        private T GetRequired<T>(string name, int stackBytes) where T : class
        {
            T value = TryGet<T>(name, stackBytes);
            if (value == null)
                throw new MissingMethodException("DLL 中未找到导出函数：" + name);
            return value;
        }

        private T TryGet<T>(string name, int stackBytes) where T : class
        {
            IntPtr proc = GetProcAddress(_module, name);

            // 某些 32 位 stdcall DLL 使用 _函数名@参数字节数 的导出名称。
            if (proc == IntPtr.Zero && IntPtr.Size == 4)
                proc = GetProcAddress(_module, "_" + name + "@" + stackBytes);

            if (proc == IntPtr.Zero)
                return null;

            return (T)(object)Marshal.GetDelegateForFunctionPointer(proc, typeof(T));
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);
    }
}
