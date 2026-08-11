using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
            : base($"{operation} 失败，错误码 {errorCode}：{HansApi.DescribeError(errorCode)}")
        {
            Operation = operation;
            ErrorCode = errorCode;
        }
    }

    internal sealed class HansApi : IDisposable
    {
        private bool _initialized;
        private string _templatePath;

        public HansApi(string dllPath)
        {
            if (string.IsNullOrWhiteSpace(dllPath))
                throw new ArgumentException("DLL 路径不能为空。", nameof(dllPath));

            // CSharpInterface 使用 DllImport("HansAdvInterface.dll")，实际加载路径由系统 DLL 搜索路径决定。
            // 这里把配置中的 DLL 目录加入搜索路径，避免必须把厂家 DLL 复制到程序目录。
            string dllDirectory = Path.GetDirectoryName(Path.GetFullPath(dllPath));
            if (!string.IsNullOrEmpty(dllDirectory))
                SetDllDirectory(dllDirectory);
        }

        public string GetVersionText()
        {
            ushort main = 0;
            ushort dll = 0;
            Check("HS_GetDllVersion", CSharpInterface.HS_GetDllVersion(ref main, ref dll));

            return $"所需主程序 {DecodeVersion(main)}；接口 DLL {DecodeVersion(dll)}（原始值 {main}/{dll}）";
        }

        public void Initialize(string machinePath)
        {
            if (_initialized)
                return;

            Check("HS_InitialMachine", CSharpInterface.HS_InitialMachine(machinePath ?? string.Empty));
            _initialized = true;
        }

        public void LoadTemplate(string templatePath)
        {
            EnsureInitialized();
            Check("HS_LoadMarkFile", CSharpInterface.HS_LoadMarkFile(templatePath));
            _templatePath = templatePath;
        }

        public void SetVariableText(string alias, string value)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("可变文本别名不能为空。", nameof(alias));

            try
            {
                byte[] valueBytes = Encoding.Unicode.GetBytes((value ?? string.Empty) + "\0");
                int rc = CSharpInterface.HS_ChangeTextByNameW(alias, valueBytes);
                if (rc == 0)
                    return;
            }
            catch (EntryPointNotFoundException)
            {
            }

            // 兼容未实现 W 接口的旧版 DLL。当前流水号内容为 ASCII，ANSI 回退不会丢失数据。
            Check("HS_ChangeTextByName", CSharpInterface.HS_ChangeTextByName(alias, value ?? string.Empty));
        }

        public MarkEndStatus MarkAndWait(
            bool redLightPreview,
            bool waitForFootPedal,
            int footPedalTimeoutMs,
            int overallTimeoutMs)
        {
            EnsureInitialized();

            int type = redLightPreview ? 1 : 0;
            Check(
                "HS_Mark",
                CSharpInterface.HS_Mark(
                    type,
                    waitForFootPedal,
                    false,
                    footPedalTimeoutMs,
                    true)
            );

            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                int flag = 0;
                Check("HS_IsMarkEnd", CSharpInterface.HS_IsMarkEnd(ref flag));

                MarkEndStatus status = ToMarkEndStatus(flag);
                if (status != MarkEndStatus.Running)
                    return status;

                if (overallTimeoutMs > 0 && sw.ElapsedMilliseconds > overallTimeoutMs)
                {
                    try
                    {
                        CSharpInterface.HS_MarkStop();
                    }
                    catch
                    {
                        // ignored
                    }

                    throw new TimeoutException("等待打标结束超时，已调用 HS_MarkStop。流水号未确认完成。");
                }

                Thread.Sleep(50);
            }
        }

        public uint? TryGetLastMarkTimeMs()
        {
            int value = 0;
            int rc = CSharpInterface.HS_GetMarkTime(ref value);
            if (rc != 0)
                return null;

            return value < 0 ? (uint?)null : (uint)value;
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            if (!string.IsNullOrWhiteSpace(_templatePath))
            {
                try
                {
                    CSharpInterface.HS_CloseMarkFile(_templatePath, false);
                }
                catch
                {
                    // ignored
                }

                _templatePath = null;
            }

            try
            {
                CSharpInterface.HS_CloseMachine();
            }
            catch
            {
                // ignored
            }

            _initialized = false;
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

        private static MarkEndStatus ToMarkEndStatus(int flag)
        {
            if (flag == (int)MarkEndStatus.Normal)
                return MarkEndStatus.Normal;
            if (flag == (int)MarkEndStatus.Aborted)
                return MarkEndStatus.Aborted;
            if (flag == (int)MarkEndStatus.DeviceError)
                return MarkEndStatus.DeviceError;

            return MarkEndStatus.Running;
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
                case 18: return "命令执行失效";
                case 100: return "未知错误";
                default:
                    try
                    {
                        return CSharpInterface.GetError();
                    }
                    catch
                    {
                        return "未定义错误";
                    }
            }
        }

        private static string DecodeVersion(ushort value)
        {
            int major = (value >> 12) & 0x0F;
            int minor = (value >> 7) & 0x1F;
            int patch = value & 0x7F;
            return "V" + major + "." + minor + "." + patch;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}