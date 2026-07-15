using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HansLaserDateSerialDemo
{
    [DataContract]
    internal sealed class AppConfiguration
    {
        public const bool DefaultUseFootPedal = false;
        public const int DefaultFootPedalTimeoutMs = 10 * 60 * 1000;

        [DataMember(IsRequired = true, Order = 1)]
        public string DllPath { get; set; }

        [DataMember(IsRequired = true, Order = 2)]
        public string MachinePath { get; set; }

        [DataMember(IsRequired = true, Order = 3)]
        public string TemplatePath { get; set; }

        [DataMember(IsRequired = true, Order = 4)]
        public string VariableTextAlias { get; set; }

        [DataMember(IsRequired = false, Order = 5)]
        public bool UseFootPedal { get; set; }

        [DataMember(IsRequired = false, Order = 6)]
        public int FootPedalTimeoutMs { get; set; }

        public static AppConfiguration Load(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException("找不到外部配置文件：" + path, path);

            return LoadFromJson(File.ReadAllText(path, Encoding.UTF8), path);
        }

        public static AppConfiguration LoadFromJson(string json, string sourceName)
        {
            if (json == null)
                throw new ArgumentNullException("json");

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppConfiguration));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json.TrimStart('\uFEFF'))))
            {
                AppConfiguration configuration = (AppConfiguration)serializer.ReadObject(stream);
                if (configuration == null)
                    throw new InvalidDataException(sourceName + " 配置内容为空。");

                configuration.ApplyDefaults();
                configuration.ValidateRequired(sourceName);
                return configuration;
            }
        }

        public static void Save(string fileName, AppConfiguration configuration)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            File.WriteAllText(path, configuration.ToJson(), new UTF8Encoding(false));
        }

        public string ToJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            AppendJsonLine(builder, "DllPath", DllPath, true);
            AppendJsonLine(builder, "MachinePath", MachinePath, true);
            AppendJsonLine(builder, "TemplatePath", TemplatePath, true);
            AppendJsonLine(builder, "VariableTextAlias", VariableTextAlias, true);
            builder.Append("  \"UseFootPedal\": ").Append(UseFootPedal ? "true" : "false").AppendLine(",");
            builder.Append("  \"FootPedalTimeoutMs\": ").Append(FootPedalTimeoutMs).AppendLine();
            builder.AppendLine("}");
            return builder.ToString();
        }

        public void ValidateFiles()
        {
            if (!File.Exists(DllPath))
                throw new FileNotFoundException("找不到接口 DLL，请修改 config.json 中的 DllPath。", DllPath);
            if (!File.Exists(TemplatePath))
                throw new FileNotFoundException("找不到打标模板，请修改 config.json 中的 TemplatePath。", TemplatePath);

            if (!string.IsNullOrWhiteSpace(MachinePath) && !Directory.Exists(MachinePath))
                throw new DirectoryNotFoundException("MachinePath 不存在：" + MachinePath);
        }

        private void ApplyDefaults()
        {
            if (FootPedalTimeoutMs <= 0)
                FootPedalTimeoutMs = DefaultFootPedalTimeoutMs;
        }

        private void ValidateRequired(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(DllPath))
                throw new InvalidDataException(sourceName + " 缺少 DllPath。");
            if (string.IsNullOrWhiteSpace(MachinePath))
                throw new InvalidDataException(sourceName + " 缺少 MachinePath。");
            if (string.IsNullOrWhiteSpace(TemplatePath))
                throw new InvalidDataException(sourceName + " 缺少 TemplatePath。");
            if (string.IsNullOrWhiteSpace(VariableTextAlias))
                throw new InvalidDataException(sourceName + " 缺少 VariableTextAlias。");
            if (FootPedalTimeoutMs < 1000)
                throw new InvalidDataException(sourceName + " 的 FootPedalTimeoutMs 至少应为 1000。");
        }

        private static void AppendJsonLine(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(Escape(value)).Append("\"");
            if (comma)
                builder.Append(",");
            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            if (value == null)
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
