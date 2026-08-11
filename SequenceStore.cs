using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HansLaserDateSerialDemo
{
    internal sealed class Reservation
    {
        public DateTime Date { get; set; }
        public int Serial { get; set; }
        public string Code { get; set; }
        public bool WasAlreadyPending { get; set; }
    }

    internal sealed class SequenceStore
    {
        private readonly ICodeGenerator _codeGenerator;

        private sealed class State
        {
            public DateTime Date;
            public int NextSerial;
            public int? PendingSerial;
            public string PendingCode;
        }

        private readonly string _path;
        private readonly object _gate = new object();

        public SequenceStore(string path, ICodeGenerator codeGenerator)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("状态文件路径不能为空。", nameof(path));
            if (codeGenerator == null)
                throw new ArgumentNullException(nameof(codeGenerator));

            _path = path;
            _codeGenerator = codeGenerator;
        }

        // 先持久化“占用”编号，再返回给打标流程。
        // 即使断电，也不会静默地把同一编号分配给下一件。
        public Reservation GetOrReserve(DateTime now)
        {
            lock (_gate)
            {
                State state = LoadOrCreate(now.Date);

                if (!string.IsNullOrEmpty(state.PendingCode))
                {
                    return new Reservation
                    {
                        Date = state.Date,
                        Serial = state.PendingSerial.Value,
                        Code = state.PendingCode,
                        WasAlreadyPending = true
                    };
                }

                if (state.Date != now.Date)
                {
                    state.Date = now.Date;
                    state.NextSerial = 1;
                }

                if (state.NextSerial < 1 || state.NextSerial > 9999)
                    throw new InvalidOperationException("当天流水号已经达到 9999，禁止回卷到 0001。 ");

                int serial = state.NextSerial;
                string code = _codeGenerator.Build(state.Date, serial);

                state.PendingSerial = serial;
                state.PendingCode = code;
                state.NextSerial = serial + 1;
                SaveAtomic(state);

                return new Reservation
                {
                    Date = state.Date,
                    Serial = serial,
                    Code = code,
                    WasAlreadyPending = false
                };
            }
        }

        public void Complete(string code)
        {
            ResolvePending(code);
        }

        // “跳过/确认已打”都会清除 pending；NextSerial 已在占用时前移。
        public void SkipOrConfirmAlreadyMarked(string code)
        {
            ResolvePending(code);
        }

        private void ResolvePending(string code)
        {
            lock (_gate)
            {
                State state = LoadRequired();
                if (string.IsNullOrEmpty(state.PendingCode))
                    throw new InvalidOperationException("当前没有待确认编号。");
                if (!string.Equals(state.PendingCode, code, StringComparison.Ordinal))
                    throw new InvalidOperationException($"待确认编号不一致。状态文件={state.PendingCode}，程序={code}");

                state.PendingSerial = null;
                state.PendingCode = null;
                SaveAtomic(state);
            }
        }

        private State LoadOrCreate(DateTime today)
        {
            if (!File.Exists(_path))
            {
                State state = new State
                {
                    Date = today,
                    NextSerial = 1,
                    PendingSerial = null,
                    PendingCode = null
                };
                SaveAtomic(state);
                return state;
            }

            return LoadRequired();
        }

        private State LoadRequired()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadAllLines(_path, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int pos = line.IndexOf('=');
                if (pos <= 0)
                    continue;

                values[line.Substring(0, pos).Trim()] = line.Substring(pos + 1).Trim();
            }

            DateTime date;
            int next;
            if (!values.ContainsKey("DATE") ||
                !DateTime.TryParseExact(values["DATE"], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
                throw new InvalidDataException($"状态文件 DATE 无效：{_path}");

            if (!values.ContainsKey("NEXT") || !int.TryParse(values["NEXT"], out next) || next < 1 || next > 10000)
                throw new InvalidDataException($"状态文件 NEXT 无效：{_path}");

            int? pendingSerial = null;
            string pendingCode = null;

            int parsedPending;
            if (values.ContainsKey("PENDING_SERIAL") &&
                int.TryParse(values["PENDING_SERIAL"], out parsedPending) &&
                parsedPending > 0)
            {
                pendingSerial = parsedPending;
            }

            if (values.ContainsKey("PENDING_CODE") && values["PENDING_CODE"].Length > 0)
                pendingCode = values["PENDING_CODE"];

            if (pendingSerial.HasValue != !string.IsNullOrEmpty(pendingCode))
                throw new InvalidDataException("状态文件的 PENDING_SERIAL/PENDING_CODE 不一致。");

            return new State
            {
                Date = date.Date,
                NextSerial = next,
                PendingSerial = pendingSerial,
                PendingCode = pendingCode
            };
        }

        private void SaveAtomic(State state)
        {
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temp = $"{_path}.tmp";
            string backup = $"{_path}.bak";

            string[] lines =
            {
                "VERSION=1",
                $"DATE={state.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
                $"NEXT={state.NextSerial.ToString(CultureInfo.InvariantCulture)}",
                $"PENDING_SERIAL={(state.PendingSerial.HasValue ? state.PendingSerial.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}",
                $"PENDING_CODE={(state.PendingCode ?? string.Empty)}"
            };

            if (File.Exists(temp))
                File.Delete(temp);

            using (FileStream fs = new FileStream(
                       temp,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            using (StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                foreach (string line in lines)
                    writer.WriteLine(line);
                writer.Flush();
                fs.Flush(true);
            }

            if (File.Exists(_path))
            {
                if (File.Exists(backup))
                    File.Delete(backup);
                File.Replace(temp, _path, backup, true);
                if (File.Exists(backup))
                    File.Delete(backup);
            }
            else
            {
                File.Move(temp, _path);
            }
        }
    }
}