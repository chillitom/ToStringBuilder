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
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class TestToStringBuilder
    {
        public class A
        {
            public string Prop { get; set; }
            public string Field;
        }

        public class B
        {
            public byte Byte = 1;
            public sbyte SByte = 1;
            public char Char = 'c';
            public short Short = 1;
            public ushort UShort = 1;
            public int Int = 1;
            public uint UInt = 1;
            public long Long = 1;
            public ulong ULong = 1;
            public float Float = 1.1f;
            public double Double = 1.1;
            public decimal Decimal = 1.1M;
        }

        public class C
        {
            public int Int { get; set; }
        }
        
        public class D
        {
            public int PublicProperty { get; set; }
            private int PrivateProperty { get; set; }
            public int PublicField;
            private int _privateField;
        }

        public class E
        {
            public int PublicField = 0;
            private int _privateField = 1;

            private static readonly ToStringBuilder<E> ToStringBuilder = new ToStringBuilder<E>()
                .Include(e => e.PublicField).Include(e => e._privateField).Compile();

            public override string ToString()
            {
                return ToStringBuilder.Stringify(this);
            }
        }

        public class F
        {
            public E ChildObject = new E();
        }

        public class G
        {
            public G()
            {
                DateTime = DateTime.Parse("2010-01-01 01:01:01");
            }
            public DateTime DateTime { get; set; }
        }

        [Test]
        public void BasicFormatting()
        {
            var target = new A { Prop = "A Property Value", Field = "A Field Value" };

            var builder = new ToStringBuilder<A>()
                .Include(a => a.Field)
                .Include(a => a.Prop)
                .Compile();
            
            var result = builder.Stringify(target);
            Assert.That(result, Is.EqualTo("A{Field:\"A Field Value\",Prop:\"A Property Value\"}"));
        }

        [Test]
        public void NotQuotingStrings()
        {
            var target = new A { Prop = "A Property Value", Field = "A Field Value" };

            var builder = new ToStringBuilder<A>()
                .QuoteStrings(false)
                .Include(a => a.Field)
                .Include(a => a.Prop)
                .Compile();
            
            var result = builder.Stringify(target);
            Assert.That(result, Is.EqualTo("A{Field:A Field Value,Prop:A Property Value}"));
        }

        [Test]
        public void MultiLineFormatting()
        {
            var target = new A { Prop = "A Property Value", Field = "A Field Value" };

            var builder = new ToStringBuilder<A>()
                .MultiLine(true)
                .Include(a => a.Field)
                .Include(a => a.Prop)
                .Compile();

            var result = builder.Stringify(target);
            Assert.That(result, Is.EqualTo("A\r\n{\r\n  Field:\"A Field Value\",\r\n  Prop:\"A Property Value\"\r\n}"));
        }

        [Test]
        public void Primitives()
        {
            var target = new B();

            var builder = new ToStringBuilder<B>()
                .Include(b => b.Byte)
                .Include(b => b.SByte)
                .Include(b => b.Char)
                .Include(b => b.Short)
                .Include(b => b.UShort)
                .Include(b => b.Int)
                .Include(b => b.UInt)
                .Include(b => b.Long)
                .Include(b => b.ULong)
                .Include(b => b.Float)
                .Include(b => b.Double)
                .Include(b => b.Decimal)
                .Compile();

            var result = builder.Stringify(target);
            Assert.That(result, Is.EqualTo("B{" +
                "Byte:1," + 
                "SByte:1," +
                "Char:'c'," +
                "Short:1," +
                "UShort:1," +
                "Int:1," +
                "UInt:1," +
                "Long:1," +
                "ULong:1," +
                "Float:1.1," +
                "Double:1.1," +
                "Decimal:1.1" +
            "}"));
        }

        [Test]
        public void BuilderReuse()
        {
            var target = new C();

            var builder = new ToStringBuilder<C>()
                .Include(c => c.Int)
                .Compile();

            for (int i = 0; i < 10; i++)
            {
                target.Int = i;
                var result = builder.Stringify(target);
                Assert.That(result, Is.EqualTo("C{Int:" + i + "}"));
            }
        }

        [Test]
        public void BuilderConcurrentUse()
        {
            var builder = new ToStringBuilder<C>()
                .Include(c => c.Int)
                .Compile();

            Action task = () =>
                            {
                                var target = new C();
                                for (int i = 0; i < 10000; i++)
                                {
                                    target.Int = i;
                                    var result = builder.Stringify(target);
                                    Assert.That(result, Is.EqualTo("C{Int:" + i + "}"));
                                }
                            };

            var taskA = Task.Factory.StartNew(task);
            var taskB = Task.Factory.StartNew(task);
            var taskC = Task.Factory.StartNew(task);
            Task.WaitAll(taskA, taskB, taskC);
        }

        [Test]
        public void OrderAlphabetically()
        {
            var target = new B();

            var builder = new ToStringBuilder<B>()
                .OrderAlphabetically(true)
                .Include(b => b.Short)
                .Include(b => b.Byte)
                .Include(b => b.SByte)
                .Include(b => b.Char)
                
                .Compile();

            var result = builder.Stringify(target);
            Assert.That(result, Is.EqualTo("B{" +                
                "Byte:1," +
                "Char:'c'," +
                "SByte:1," +
                "Short:1" +
            "}"));
        }

        [Test]
        public void IncludeAllPublic()
        {
            var target = new D();

            var builder = new ToStringBuilder<D>()
                .IncludeAllPublic()
                .Compile();

            var result = builder.Stringify(target);
            Assert.That(result, Is.EqualTo("D{" +                
                "PublicField:0," +
                "PublicProperty:0" +
            "}"));
        }

        [Test]
        public void PrivateFields()
        {
            E e = new E();
            Assert.That(e.ToString(), Is.EqualTo("E{PublicField:0,_privateField:1}"));
        }

        [Test]
        public void ChildObjects()
        {
            var target= new F();
            var builder = new ToStringBuilder<F>()
                .Include(f => f.ChildObject)
                .Compile();

            var result = builder.Stringify(target);
            Assert.That(result, Is.EqualTo("F{ChildObject:E{PublicField:0,_privateField:1}}"));
        }

        [Test]
        public void NonPrimitiveValueTypes()
        {
            var target = new G();
            var builder = new ToStringBuilder<G>()
                .IncludeAllPublic()
                .Compile();

            var result = builder.Stringify(target);

            Assert.That(result, Contains.Substring("01/01/2010 01:01:01"));
        }


        [Test, ExpectedException(ExpectedMessage = "ToStringBuilder not compiled")]
        public void NonCompiledBuilderThrowsOnStringify()
        {
            var target = new A();
            var builder = new ToStringBuilder<A>()
                .Include(a => a.Prop);
            builder.Stringify(target);
        }
    }
}