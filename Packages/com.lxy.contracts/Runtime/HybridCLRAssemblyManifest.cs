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
        /// <summary>
        /// 公开的清单位置数据。
        /// </summary>
        public const string ManifestLocation =
            "HybridCLRAssemblyManifest";

        /// <summary>
        /// 公开的aotMetadataDlls数据。
        /// </summary>
        public string[] aotMetadataDlls = Array.Empty<string>();
        /// <summary>
        /// 公开的热更新UpdateDlls数据。
        /// </summary>
        public string[] hotUpdateDlls = Array.Empty<string>();
    }
}
