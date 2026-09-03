namespace Game.Battle.Core
{
    public struct BattleHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong value;

        /// <summary>
        /// 向调用方提供值。
        /// </summary>
        public ulong Value => value == 0 ? OffsetBasis : value;

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        public void Add(bool item) => Add(item ? 1 : 0);

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        public void Add(int item) => Add(unchecked((uint)item));

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        public void Add(uint item)
        {
            EnsureInitialized();
            AddByte((byte)item);
            AddByte((byte)(item >> 8));
            AddByte((byte)(item >> 16));
            AddByte((byte)(item >> 24));
        }

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        public void Add(long item) => Add(unchecked((ulong)item));

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        public void Add(ulong item)
        {
            EnsureInitialized();
            for (int shift = 0; shift < 64; shift += 8)
            {
                AddByte((byte)(item >> shift));
            }
        }

        /// <summary>
        /// 确保Initialized。
        /// </summary>
        private void EnsureInitialized()
        {
            if (value == 0)
            {
                value = OffsetBasis;
            }
        }

        /// <summary>
        /// 添加Byte。
        /// </summary>
        private void AddByte(byte item)
        {
            value ^= item;
            value *= Prime;
        }
    }
}
