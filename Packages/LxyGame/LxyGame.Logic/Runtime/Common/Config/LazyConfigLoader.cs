using Luban;

namespace config
{
    public sealed class LazyConfigLoader<T> where T : class, IConfigData, new()
    {
        private T data;

        public T Data
        {
            get
            {
                LoadData();
                return data;
            }
        }

        public void Reset()
        {
            data = default;
            buffer = null;
        }

        private ByteBuf buffer;

        public void Preload()
        {
            if (data != null || buffer != null)
                return;

            buffer = loader(name);
        }

        private void LoadData()
        {
            if (data != null)
                return;

            var loadedData = new T();
            buffer ??= loader(name);
            loadedData.Initialize(buffer);

            // Publish before resolving references so cyclic table references do not
            // recursively deserialize the same table.
            data = loadedData;
            if (loadedData is IConfigDataDeserializer deserializer)
            {
                deserializer.OnDeserialize();
            }

            buffer = null;
        }

        private readonly System.Func<string, ByteBuf> loader;
        private readonly string name;

        public LazyConfigLoader(System.Func<string, ByteBuf> loader, string name)
        {
            this.loader = loader ?? throw new System.ArgumentNullException(nameof(loader));
            this.name = string.IsNullOrWhiteSpace(name)
                ? throw new System.ArgumentException("Config name cannot be empty.", nameof(name))
                : name;
        }
    }
}
