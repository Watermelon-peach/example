using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Saessak.Language
{
    /// <summary>
    /// 새싹의 값들을 다루는 도우미 모음.
    /// 새싹 값은 C#에서 이렇게 표현돼요:
    ///   숫자 → double, 문자 → string, 참거짓 → bool, 없음 → null,
    ///   목록 → List&lt;object&gt;, 함수 → UserFunction / NativeFunction
    /// </summary>
    public static class SaessakValues
    {
        #region Custom Methods

        /// <summary>조건으로 쓸 때 참인지. 거짓·없음·0·빈 문자·빈 목록만 거짓이에요.</summary>
        public static bool IsTruthy(object value) => value switch
        {
            null => false,
            bool b => b,
            double d => d != 0,
            string s => s.Length > 0,
            List<object> list => list.Count > 0,
            _ => true,
        };

        public static bool IsFunction(object value) => value is UserFunction || value is NativeFunction;

        public static string TypeName(object value) => value switch
        {
            null => "없음",
            bool _ => "참거짓",
            double _ => "숫자",
            string _ => "문자",
            List<object> _ => "목록",
            UserFunction _ => "함수",
            NativeFunction _ => "함수",
            _ => value.GetType().Name,
        };

        /// <summary>출력할 때 보이는 모양. 3.0은 "3"으로, 참은 "참"으로 보여 줘요.</summary>
        public static string ToDisplay(object value) => ToDisplay(value, false);

        static string ToDisplay(object value, bool insideList)
        {
            switch (value)
            {
                case null:
                    return "없음";
                case bool b:
                    return b ? "참" : "거짓";
                case double d:
                    return FormatNumber(d);
                case string s:
                    return insideList ? $"\"{s}\"" : s;
                case List<object> list:
                {
                    var builder = new StringBuilder("[");
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0)
                            builder.Append(", ");
                        builder.Append(ToDisplay(list[i], true));
                    }
                    return builder.Append(']').ToString();
                }
                case UserFunction f:
                    return $"<함수 {f.Name}>";
                case NativeFunction f:
                    return $"<함수 {f.Name}>";
                default:
                    return value.ToString();
            }
        }

        static string FormatNumber(double d)
        {
            if (double.IsNaN(d))
                return "숫자아님";
            if (double.IsInfinity(d))
                return d > 0 ? "무한" : "-무한";
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15)
                return ((long)d).ToString(CultureInfo.InvariantCulture);
            // G15: 0.1 + 0.2 같은 계산의 아주 작은 오차는 숨겨서 0.3으로 보여 줘요.
            return d.ToString("G15", CultureInfo.InvariantCulture);
        }

        public static bool AreEqual(object a, object b)
        {
            if (a == null || b == null)
                return a == null && b == null;

            if (a is List<object> listA && b is List<object> listB)
            {
                if (listA.Count != listB.Count)
                    return false;
                for (int i = 0; i < listA.Count; i++)
                {
                    if (!AreEqual(listA[i], listB[i]))
                        return false;
                }
                return true;
            }

            return a.Equals(b);
        }

        /// <summary>
        /// 게임(C#) 쪽에서 넘겨준 값을 새싹 값으로 바꿔요. int·float 같은 숫자는 모두 double로 맞춰요.
        /// </summary>
        public static object FromHost(object value) => value switch
        {
            int i => (double)i,
            long l => (double)l,
            float f => (double)f,
            char c => c.ToString(),
            _ => value,
        };

        // ───────── 기본 함수들이 값 종류를 검사할 때 쓰는 도우미 ─────────

        public static double ExpectNumber(object value, string function)
        {
            if (value is double d)
                return d;
            throw new SaessakError($"{function} 함수에는 숫자를 넣어야 해요. (지금: {TypeName(value)})");
        }

        public static int ExpectInteger(object value, string function)
        {
            double d = ExpectNumber(value, function);
            if (d != Math.Floor(d) || d > int.MaxValue || d < int.MinValue)
                throw new SaessakError($"{function} 함수에는 정수를 넣어야 해요. (지금: {ToDisplay(value)})");
            return (int)d;
        }

        public static List<object> ExpectList(object value, string function)
        {
            if (value is List<object> list)
                return list;
            throw new SaessakError($"{function} 함수에는 목록을 넣어야 해요. (지금: {TypeName(value)})");
        }

        #endregion
    }
}
