using System;
using System.Collections.Generic;

namespace HansLaserDateSerialDemo
{
    internal static class CodeGenerator
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

        public static string Build(DateTime date, int serial)
        {
            char yearCode;
            if (!YearCodes.TryGetValue(date.Year, out yearCode))
            {
                throw new ArgumentOutOfRangeException(
                    "date",
                    "年份不在编码表范围内（2025-2035）：" + date.Year);
            }

            if (serial < 1 || serial > 9999)
            {
                throw new ArgumentOutOfRangeException(
                    "serial",
                    "流水号必须在 0001-9999 范围内。");
            }

            char monthCode = MonthDayCodes[date.Month - 1];
            char dayCode = MonthDayCodes[date.Day - 1];

            return string.Concat(
                yearCode,
                monthCode,
                dayCode,
                serial.ToString("0000"));
        }
    }
}
