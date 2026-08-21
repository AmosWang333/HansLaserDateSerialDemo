using System;
using System.Collections.Generic;

namespace HansLaserDateSerialDemo
{
    public sealed class CodeGeneratorTypes
    {
        public const string Normal = "Normal";
        public const string EcoFlow = "EcoFlow";
    }

    internal interface ICodeGenerator
    {
        ICodeGenerator Init(string pattern);
        string Build(DateTime date, int serial);
    }

    internal static class CodeGeneratorFactory
    {
        public static ICodeGenerator Create(string generatorType, string pattern)
        {
            ICodeGenerator generator;
            if (string.Equals(generatorType, CodeGeneratorTypes.Normal, StringComparison.OrdinalIgnoreCase))
                generator = new NormalCodeGenerator();
            else if (string.Equals(generatorType, CodeGeneratorTypes.EcoFlow, StringComparison.OrdinalIgnoreCase))
                generator = new EcoFlowCodeGenerator();
            else
                throw new ArgumentException("不支持的编号生成器：" + generatorType, nameof(generatorType));

            return generator.Init(pattern ?? string.Empty);
        }
    }

    internal class NormalCodeGenerator : ICodeGenerator
    {
        private string _pattern = "";

        public ICodeGenerator Init(string pattern)
        {
            _pattern = pattern.Replace("JJJ", "{1:000}");
            return this;
        }

        public string Build(DateTime date, int serial)
        {
            var dayOfYear = date.DayOfYear;
            return string.Format(date.ToString(_pattern), serial, dayOfYear);
        }
    }

    internal class EcoFlowCodeGenerator : ICodeGenerator
    {
        // 1..31 => 1..9, A..H, J..N, P..X（跳过 I、O）
        private const string MonthDayCodes = "123456789ABCDEFGHJKLMNPQRSTUVWX";

        private static readonly Dictionary<int, char> YearCodes =
            new Dictionary<int, char>
            {
                { 2025, 'H' },
                { 2026, 'J' },
                { 2027, 'K' },
                { 2028, 'L' },
                { 2029, 'M' },
                { 2030, 'N' },
                { 2031, 'P' },
                { 2032, 'Q' },
                { 2033, 'R' },
                { 2034, 'S' },
                { 2035, 'T' }
            };

        private string _staticStr = "";

        public ICodeGenerator Init(string pattern)
        {
            _staticStr = pattern;
            return this;
        }

        public string Build(DateTime date, int serial)
        {
            char yearCode;
            if (!YearCodes.TryGetValue(date.Year, out yearCode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(date),
                    "年份不在编码表范围内（2025-2035）：" + date.Year);
            }

            if (serial < 1 || serial > 9999)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(serial),
                    "流水号必须在 0001-9999 范围内。");
            }

            char monthCode = MonthDayCodes[date.Month - 1];
            char dayCode = MonthDayCodes[date.Day - 1];

            return string.Concat(
                _staticStr,
                yearCode,
                monthCode,
                dayCode,
                serial.ToString("0000"));
        }
    }
}