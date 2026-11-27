using System;
using System.Collections.Generic;
using System.Globalization;

namespace Saessak.Language
{
    /// <summary>
    /// 새싹에 기본으로 들어 있는 함수들 (출력, 길이, 범위 …).
    /// 게임 전용 함수(이동, 수확 …)는 여기가 아니라 게임 쪽에서 Interpreter.DefineFunction으로 넣어요.
    /// </summary>
    public static class Builtins
    {
        #region Variables

        const int MaxRangeSize = 100000;

        static readonly Random Random = new Random();

        #endregion

        #region Custom Methods

        public static void Register(Interpreter interpreter)
        {
            interpreter.DefineFunction("출력", 0, -1, args =>
            {
                var parts = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                    parts[i] = SaessakValues.ToDisplay(args[i]);
                interpreter.Print(string.Join(" ", parts));
                return null;
            });

            interpreter.DefineFunction("길이", 1, 1, args => args[0] switch
            {
                List<object> list => (double)list.Count,
                string text => (double)text.Length,
                _ => throw new SaessakError($"길이 함수에는 목록이나 문자를 넣어야 해요. (지금: {SaessakValues.TypeName(args[0])})"),
            });

            interpreter.DefineFunction("범위", 1, 3, Range);

            interpreter.DefineFunction("추가", 2, 2, args =>
            {
                SaessakValues.ExpectList(args[0], "추가").Add(args[1]);
                return null;
            });

            interpreter.DefineFunction("꺼내기", 1, 1, args =>
            {
                List<object> list = SaessakValues.ExpectList(args[0], "꺼내기");
                if (list.Count == 0)
                    throw new SaessakError("빈 목록에서는 꺼낼 수 없어요.");
                object last = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                return last;
            });

            interpreter.DefineFunction("정수", 1, 1, args => ToInteger(args[0]));

            interpreter.DefineFunction("문자", 1, 1, args => SaessakValues.ToDisplay(args[0]));

            interpreter.DefineFunction("절댓값", 1, 1, args => Math.Abs(SaessakValues.ExpectNumber(args[0], "절댓값")));

            interpreter.DefineFunction("최소", 1, -1, args => Extreme(args, "최소", pickSmaller: true));
            interpreter.DefineFunction("최대", 1, -1, args => Extreme(args, "최대", pickSmaller: false));

            interpreter.DefineFunction("무작위", 2, 2, args =>
            {
                int min = SaessakValues.ExpectInteger(args[0], "무작위");
                int max = SaessakValues.ExpectInteger(args[1], "무작위");
                if (min > max)
                    throw new SaessakError("무작위 함수의 시작 값이 끝 값보다 커요.");
                return (double)Random.Next(min, max + 1);
            });
        }

        static object Range(object[] args)
        {
            int start = 0;
            int end;
            int step = 1;

            if (args.Length == 1)
            {
                end = SaessakValues.ExpectInteger(args[0], "범위");
            }
            else
            {
                start = SaessakValues.ExpectInteger(args[0], "범위");
                end = SaessakValues.ExpectInteger(args[1], "범위");
                if (args.Length == 3)
                    step = SaessakValues.ExpectInteger(args[2], "범위");
            }

            if (step == 0)
                throw new SaessakError("범위 함수의 간격은 0이 될 수 없어요.");

            long count = step > 0
                ? Math.Max(0, ((long)end - start + step - 1) / step)
                : Math.Max(0, ((long)start - end - step - 1) / -step);
            if (count > MaxRangeSize)
                throw new SaessakError($"범위가 너무 커요. (최대 {MaxRangeSize}개까지 만들 수 있어요)");

            var list = new List<object>((int)count);
            for (long value = start; step > 0 ? value < end : value > end; value += step)
                list.Add((double)value);
            return list;
        }

        static object ToInteger(object value)
        {
            switch (value)
            {
                case double d:
                    return Math.Truncate(d);
                case bool b:
                    return b ? 1.0 : 0.0;
                case string text when double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed):
                    return Math.Truncate(parsed);
                case string text:
                    throw new SaessakError($"숫자로 바꿀 수 없는 문자예요: \"{text}\"");
                default:
                    throw new SaessakError($"정수 함수에는 숫자나 문자를 넣어야 해요. (지금: {SaessakValues.TypeName(value)})");
            }
        }

        static object Extreme(object[] args, string name, bool pickSmaller)
        {
            // 최소([3, 1, 2]) 처럼 목록 하나를 넣어도 되고, 최소(3, 1, 2) 처럼 여러 개를 넣어도 돼요.
            IList<object> values = args.Length == 1 && args[0] is List<object> list ? list : (IList<object>)args;
            if (values.Count == 0)
                throw new SaessakError($"{name} 함수에 빈 목록을 넣을 수 없어요.");

            double best = SaessakValues.ExpectNumber(values[0], name);
            for (int i = 1; i < values.Count; i++)
            {
                double value = SaessakValues.ExpectNumber(values[i], name);
                if (pickSmaller ? value < best : value > best)
                    best = value;
            }
            return best;
        }

        #endregion
    }
}
