using System;

namespace Game.Contracts
{
    /// <summary>
    /// HybridCLR 可热更新资源清单的数据结构。
    /// 清单内容由 YooAsset 下发，类型本身保留在主包 AOT 中。
    /// </summary>
    [Serializable]
    public sealed class HybridCLRAssemblyManifest
    {
        public const string ManifestLocation =
            "HybridCLRAssemblyManifest";

        public const string DefaultEntryAssemblyName =
            "Game.HotUpdate";

        public string entryAssemblyName =
            DefaultEntryAssemblyName;
        public string[] aotMetadataDlls = Array.Empty<string>();
        public string[] hotUpdateDlls = Array.Empty<string>();
    }
}
