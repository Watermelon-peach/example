using System.Collections.Generic;

namespace Saessak.Language
{
    /// <summary>
    /// 스코프(Scope): 변수 이름과 값을 적어 두는 공책이에요.
    /// 함수를 부를 때마다 새 공책을 만들고, 못 찾은 이름은 바깥(부모) 공책에서 찾아요.
    /// </summary>
    public sealed class Scope
    {
        #region Variables

        readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        readonly Scope _parent;

        #endregion

        #region Custom Methods

        public Scope(Scope parent)
        {
            _parent = parent;
        }

        /// <summary>이 공책에 바로 적어요. (함수 매개변수, 함수 정의)</summary>
        public void Define(string name, object value)
        {
            _values[name] = value;
        }

        public bool TryGet(string name, out object value)
        {
            for (Scope scope = this; scope != null; scope = scope._parent)
            {
                if (scope._values.TryGetValue(name, out value))
                    return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// "x = 값" 처리. 바깥 공책에 같은 이름이 있으면 그 값을 바꾸고, 없으면 이 공책에 새로 적어요.
        /// (파이썬과 달리 global 없이도 바깥 변수를 바꿀 수 있어요)
        /// </summary>
        public void Assign(string name, object value)
        {
            for (Scope scope = this; scope != null; scope = scope._parent)
            {
                if (scope._values.ContainsKey(name))
                {
                    scope._values[name] = value;
                    return;
                }
            }
            _values[name] = value;
        }

        #endregion
    }
}
