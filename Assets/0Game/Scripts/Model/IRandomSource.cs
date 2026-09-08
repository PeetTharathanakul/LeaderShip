namespace LeaderShip.Model
{
    /// <summary>
    /// การสุ่มทั้งหมดผ่านอินเทอร์เฟซนี้เท่านั้น (ADR-0007)
    /// เพื่อให้ (1) เล่นซ้ำ bug เดิมได้จาก seed เดียวกัน (2) เทสต์ไม่กระพริบ (3) simulator คุมการสุ่มได้
    /// ห้ามเรียก System.Random หรือ UnityEngine.Random ตรง ๆ ในชั้นโมเดลเด็ดขาด
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>คืนค่าในช่วง [0, 1)</summary>
        double NextDouble();

        /// <summary>คืนค่าจำนวนเต็มในช่วง [minInclusive, maxExclusive)</summary>
        int NextInt(int minInclusive, int maxExclusive);
    }

    /// <summary>
    /// PRNG แบบ xorshift128 ที่ให้ผลเหมือนกันทุกแพลตฟอร์ม
    /// ไม่ใช้ System.Random เพราะสเปกของมันไม่การันตีว่าลำดับจะเหมือนกันข้าม runtime
    /// ซึ่งจะทำให้ seed เดียวกันให้ผลคนละอย่างระหว่าง Editor กับ build
    /// </summary>
    public sealed class SeededRandom : IRandomSource
    {
        uint _x, _y, _z, _w;

        public int Seed { get; }

        public SeededRandom(int seed)
        {
            Seed = seed;

            // splitmix32 เพื่อกระจาย seed ให้ state ทั้ง 4 ตัว — seed 0 กับ 1 ต้องไม่ให้ลำดับที่คล้ายกัน
            uint s = unchecked((uint)seed);
            _x = NextSplit(ref s);
            _y = NextSplit(ref s);
            _z = NextSplit(ref s);
            _w = NextSplit(ref s);

            if ((_x | _y | _z | _w) == 0u) _x = 0x9E3779B9u; // state ทั้งหมดเป็น 0 จะค้างตลอดกาล
        }

        static uint NextSplit(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint z = state;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                return z ^ (z >> 16);
            }
        }

        uint NextUInt()
        {
            unchecked
            {
                uint t = _x ^ (_x << 11);
                _x = _y; _y = _z; _z = _w;
                _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
                return _w;
            }
        }

        public double NextDouble()
        {
            // ใช้ 53 บิตบนเพื่อให้กระจายทั่วช่วง [0,1) อย่างสม่ำเสมอ
            ulong hi = (ulong)(NextUInt() >> 5);
            ulong lo = (ulong)(NextUInt() >> 6);
            return ((hi * 67108864.0) + lo) / 9007199254740992.0;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            long range = (long)maxExclusive - minInclusive;
            return (int)(minInclusive + (long)(NextDouble() * range));
        }
    }
}
