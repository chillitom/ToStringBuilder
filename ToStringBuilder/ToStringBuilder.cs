// Copyright (c) 2011, Tom Rathbone.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//
//    * Redistributions of source code must retain the above copyright
//      notice, this list of conditions and the following disclaimer.
//
//    * Redistributions in binary form must reproduce the above
//      copyright notice, this list of conditions and the following disclaimer
//      in the documentation and/or other materials provided with the
//      distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

namespace Chillitom
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    public class ToStringBuilder<T> where T : class 
    {
        private static readonly string TypeName = typeof(T).Name;

        private static readonly ParameterExpression SbArgExpression = Expression.Parameter(typeof(StringBuilder), "sb");
        private static readonly ParameterExpression TargetArgExpression = Expression.Parameter(typeof(T), "target");
        private static readonly MethodInfo CharAppendMethodInfo = typeof(StringBuilder).GetMethod("Append", new[] { typeof(char) });
        private static readonly MethodInfo StringAppendMethodInfo = typeof(StringBuilder).GetMethod("Append", new[] { typeof(string) });
        private static readonly MethodInfo GenericStringJoinMethod = typeof(string).GetGenericMethod("Join", new[] { typeof(string), typeof(IEnumerable<>) });

        private readonly List<MemberInfo> _members = new List<MemberInfo>();
        private readonly List<Expression> _appendExpressions = new List<Expression>();
        private Action<StringBuilder, T> _action;
        private bool _orderAlphabetically;
        private bool _quoteStrings = true;
        private bool _multiLine;

        public ToStringBuilder()
        {
        }

        public ToStringBuilder(bool quoteStrings = true, bool multiLine = false, bool orderAlphabetically = false)
        {
            _quoteStrings = quoteStrings;
            _orderAlphabetically = orderAlphabetically;
            _multiLine = multiLine;
        }

        public ToStringBuilder<T> QuoteStrings(bool quoteStrings)
        {
            _quoteStrings = quoteStrings;
            return this;
        }

        public ToStringBuilder<T> MultiLine(bool multiLine)
        {
            _multiLine = multiLine;
            return this;
        }

        public ToStringBuilder<T> OrderAlphabetically(bool orderAlphabetically)
        {
            _orderAlphabetically = orderAlphabetically;
            return this;
        }

        public ToStringBuilder<T> Include<TResult>(Expression<Func<T, TResult>> expression)
        {
            if(expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            MemberExpression memberExpression = expression.Body as MemberExpression;
            if(memberExpression != null && 
                (memberExpression.Member.MemberType == MemberTypes.Field
                    || memberExpression.Member.MemberType == MemberTypes.Property))
            {
                _members.Add(memberExpression.Member);
            }
            else
            {
                throw new ArgumentException("non-member expression passed to ToStringBuilder", "expression");
            }
            return this;
        }

        public ToStringBuilder<T> IncludeAllPublic()
        {
            foreach (var member in typeof(T).FindMembers(MemberTypes.Field | MemberTypes.Property, BindingFlags.Public | BindingFlags.Instance, null, null))
            {
                _members.Add(member);
            }
            return this;
        }

        public ToStringBuilder<T> Compile()
        {
            _appendExpressions.Clear();
            if(_orderAlphabetically)
            {
                StringComparer comparer = StringComparer.OrdinalIgnoreCase;
                _members.Sort((a,b) => comparer.Compare(a.Name, b.Name));
            }

            AppendTypeName();
            AppendStartOfMembers();

            bool first = true;
            foreach(MemberInfo me in _members)
            {
                if (!first)
                {
                    AppendMemberSeperator();
                }
                else
                {
                    first = false;
                }

                AppendMemberName(me);
                AppendMember(me);
            }

            AppendEndOfMembers();
            
            BlockExpression block = Expression.Block(_appendExpressions);
            _action = Expression.Lambda<Action<StringBuilder,T>>(block, SbArgExpression, TargetArgExpression).Compile();
            
            return this;
        }

        public string Stringify(T target)
        {
            if(target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (_action != null)
            {
                StringBuilder sb = new StringBuilder();
                _action.Invoke(sb, target);
                return sb.ToString();
            }
            throw new Exception("ToStringBuilder not compiled");
        }

        private void AppendMember(MemberInfo memberInfo)
        {
            AppendQuotesIfRequiredForType(memberInfo);

            Type type = GetMemberType(memberInfo);
            var memberAppendMethod = typeof(StringBuilder).GetMethod("Append", new[] { type });
            Expression getMemberValue = Expression.MakeMemberAccess(TargetArgExpression, memberInfo);

            if (type.IsValueType)
            {
                Type appendArgType = memberAppendMethod.GetParameters()[0].ParameterType;
                if (type != appendArgType)
                {
                    getMemberValue = Expression.TypeAs(getMemberValue, typeof(object));
                }
                _appendExpressions.Add(Expression.Call(SbArgExpression, memberAppendMethod, getMemberValue));
            }
            else if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                // emit the equivalent of string.Join(", ", args), where args is IEnumerable<T>
                AppendStartOfMembers();

                var genericStringJoinMethod = GenericStringJoinMethod.MakeGenericMethod(new[] { type.GetGenericArguments()[0] });
                
                getMemberValue = Expression.Call(genericStringJoinMethod, Expression.Constant(", "), getMemberValue);
                
                _appendExpressions.Add(Expression.Call(SbArgExpression, memberAppendMethod, getMemberValue));
                
                AppendEndOfMembers();
            }
            else
            {
                _appendExpressions.Add(Expression.Call(SbArgExpression, memberAppendMethod, getMemberValue));
            }
            
            AppendQuotesIfRequiredForType(memberInfo);
        }

        private void AppendQuotesIfRequiredForType(MemberInfo memberInfo)
        {
            if (_quoteStrings)
            {
                if (typeof(string).IsAssignableFrom(GetMemberType(memberInfo)))
                {
                    AppendChar('"');
                }
                else if (typeof(char).IsAssignableFrom(GetMemberType(memberInfo)))
                {
                    AppendChar('\'');
                }
            }
        }

        private void AppendMemberName(MemberInfo memberInfo)
        {
            if(_multiLine)
            {
                AppendString(Environment.NewLine + "  " + memberInfo.Name + ":");
            }
            else
            {
                AppendString(memberInfo.Name + ":");
            }
        }

        private void AppendStartOfMembers()
        {
            AppendChar('{');
        }

        private void AppendMemberSeperator()
        {
            AppendChar(',');
        }

        private void AppendEndOfMembers()
        {
            if (_multiLine)
            {
                AppendString(Environment.NewLine + "}");
            }
            else
            {
                AppendChar('}');
            }
        }

        private void AppendTypeName()
        {
            if (_multiLine)
            {
                AppendString(TypeName + Environment.NewLine);
            }
            else
            {
                AppendString(TypeName);
            }
        }

        private void AppendChar(char c)
        {
            _appendExpressions.Add(Expression.Call(SbArgExpression, CharAppendMethodInfo, Expression.Constant(c)));
        }

        private void AppendString(string s)
        {
            _appendExpressions.Add(Expression.Call(SbArgExpression, StringAppendMethodInfo, Expression.Constant(s)));
        }

        private static Type GetMemberType(MemberInfo memberInfo)
        {
            if(memberInfo is FieldInfo)
            {
                return ((FieldInfo) memberInfo).FieldType;
            }
            if(memberInfo is PropertyInfo)
            {
                return ((PropertyInfo)memberInfo).PropertyType;
            }
            throw new Exception("illegal state, expecting property or field");
        }
    }
    
    // code to find a generic method on a Type, sourced from : http://stackoverflow.com/a/4036187/115734
    static class ToStringHelper
    {

        private class SimpleTypeComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type x, Type y)
            {
                return x.Assembly == y.Assembly &&
                    x.Namespace == y.Namespace &&
                    x.Name == y.Name;
            }

            public int GetHashCode(Type obj)
            {
                throw new NotImplementedException();
            }
        }

        public static MethodInfo GetGenericMethod(this Type type, string name, Type[] parameterTypes)
        {
            var methods = type.GetMethods();
            foreach (var method in methods.Where(m => m.Name == name))
            {
                var methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

                if (methodParameterTypes.SequenceEqual(parameterTypes, new SimpleTypeComparer()))
                {
                    return method;
                }
            }

            return null;
        }
    }
}
