using System;
using System.IO;
using System.Text;

namespace HansLaserDateSerialDemo
{
    internal static class AuditLog
    {
        private static readonly object Gate = new object();

        public static void Append(string path, string action, string code, string detail)
        {
            lock (Gate)
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                bool newFile = !File.Exists(path);
                using (StreamWriter writer = new StreamWriter(path, true, new UTF8Encoding(false)))
                {
                    if (newFile)
                        writer.WriteLine("time,action,code,detail");

                    writer.WriteLine(
                        $"{Csv(DateTime.Now.ToString("o"))},{Csv(action)},{Csv(code)},{Csv(detail)}");
                }
            }
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}