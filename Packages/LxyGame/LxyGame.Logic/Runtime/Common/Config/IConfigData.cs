using Luban;

namespace config
{
    public interface IConfigData
    {
        void Initialize(ByteBuf buffer);
    }
}
