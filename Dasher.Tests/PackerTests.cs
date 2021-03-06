﻿#region License
//
// Dasher
//
// Copyright 2015-2017 Drew Noakes
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
// More information about this project is available at:
//
//    https://github.com/drewnoakes/dasher
//
#endregion

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Dasher.Tests
{
    public sealed class PackerTests
    {
        private readonly MemoryStream _stream;
        private readonly Packer _packer;
        private readonly MsgPack.Unpacker _unpacker;

        public PackerTests()
        {
            _stream = new MemoryStream();
            _packer = new Packer(_stream);
            _unpacker = MsgPack.Unpacker.Create(_stream);
        }

        [Fact]
        public void PacksByte()
        {
            for (var i = (int)byte.MinValue; i <= byte.MaxValue; i++)
            {
                _stream.Position = 0;

                _packer.Pack((byte)i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadByte(out byte result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksSByte()
        {
            for (var i = (int)sbyte.MinValue; i <= sbyte.MaxValue; i++)
            {
                _stream.Position = 0;

                _packer.Pack((sbyte)i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadSByte(out sbyte result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksInt16()
        {
            for (var i = (int)short.MinValue; i <= short.MaxValue; i++)
            {
                _stream.Position = 0;

                _packer.Pack((short)i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadInt16(out short result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksUInt16()
        {
            for (var i = (int)ushort.MinValue; i <= ushort.MaxValue; i++)
            {
                _stream.Position = 0;

                _packer.Pack((ushort)i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadUInt16(out ushort result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksInt32()
        {
            var inputs = Enumerable.Range(-60000, 60000*2).Concat(new[] {int.MinValue, int.MaxValue, int.MinValue + 1, int.MaxValue - 1});

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadInt32(out int result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksUInt32()
        {
            var inputs = Enumerable.Range(0, 60000 * 2)
                .Select(i => (uint)i)
                .Concat(new[] { (uint)int.MaxValue, (uint)int.MaxValue - 1, uint.MaxValue, uint.MaxValue - 1 });

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadUInt32(out uint result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksInt64()
        {
            var inputs = Enumerable.Range(-60000, 60000*2)
                .Select(i => (long)i)
                .Concat(new[] {int.MinValue, int.MaxValue, int.MinValue + 1, int.MaxValue - 1, int.MaxValue + 1L, int.MinValue - 1L, long.MinValue, long.MaxValue, long.MinValue + 1, long.MaxValue - 1});

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadInt64(out long result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksUInt64()
        {
            var inputs = Enumerable.Range(-60000, 60000 * 2)
                .Select(i => (ulong)i)
                .Concat(new ulong[] { int.MaxValue, int.MaxValue - 1, int.MaxValue + 1L, ulong.MinValue, ulong.MaxValue, ulong.MinValue + 1, ulong.MaxValue - 1 });

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadUInt64(out ulong result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksSingle()
        {
            var inputs = new[] {float.MinValue, float.MaxValue, 0.0f, 1.0f, -1.0f, 0.1f, -0.1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.Epsilon};

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadSingle(out float result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksDouble()
        {
            var inputs = new[] {double.MinValue, double.MaxValue, 0.0f, 1.0f, -1.0f, 0.1f, -0.1f, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.Epsilon};

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadDouble(out double result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksString()
        {
            var inputs = new[] {"Hello", "", Environment.NewLine, null, "\0"};

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadString(out string result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksStringWithEncoding()
        {
            var inputs = new[] {"Hello", "", Environment.NewLine, null, "\0", new string('A', 0xFF), new string('A', 0x100), new string('A', 0x10000) };

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i, Encoding.UTF8);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadString(out string result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksBool()
        {
            var inputs = new[] {true, false};

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadBoolean(out bool result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksBytes()
        {
            var inputs = new[] {null, new byte[0], new byte[0xFF], new byte[0xFFFF], new byte[0x10000], new byte[] {1,2,3}};

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadBinary(out byte[] result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksByteArraySegment()
        {
            var inputs = new[]
            {
                new ArraySegment<byte>(new byte[0]),
                new ArraySegment<byte>(new byte[0xFF]),
                new ArraySegment<byte>(new byte[0xFFFF]),
                new ArraySegment<byte>(new byte[0x10000]),
                new ArraySegment<byte>(new byte[0x10000], 100, 100),
                new ArraySegment<byte>(new byte[0x10000], 100, 0),
                new ArraySegment<byte>(new byte[] {1, 2, 3}),
                new ArraySegment<byte>(new byte[] {1, 2, 3}, 1, 2),
                new ArraySegment<byte>(new byte[] {1, 2, 3}, 1, 1),
                new ArraySegment<byte>(new byte[] {1, 2, 3}, 1, 0),
                new ArraySegment<byte>(new byte[] {1, 2, 3}, 2, 1),
            };

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.Pack(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadBinary(out byte[] result));
                Assert.Equal(i.ToArray(), result);
            }
        }

        [Fact]
        public void PacksArrayHeader()
        {
            var inputs = new uint[] {0, 1, 255, 256, ushort.MaxValue, ushort.MaxValue + 1, int.MaxValue};

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.PackArrayHeader(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadArrayLength(out long result));
                Assert.Equal(i, result);
            }
        }

        [Fact]
        public void PacksMapHeader()
        {
            var inputs = new uint[] {0, 1, 255, 256, ushort.MaxValue, ushort.MaxValue + 1, int.MaxValue};

            foreach (var i in inputs)
            {
                _stream.Position = 0;

                _packer.PackMapHeader(i);
                _packer.Flush();

                _stream.Position = 0;

                Assert.True(_unpacker.ReadMapLength(out long result));
                Assert.Equal(i, result);
            }
        }
    }
}