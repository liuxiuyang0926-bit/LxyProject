/// <summary>
/// YooAsset 资源交付层标签。
/// Collector、首包复制规则和运行时下载器必须共同使用这组值。
/// </summary>
public static class YooAssetContentTags
{
    /// <summary>随 APK 安装，同时也参与后续版本校验。</summary>
    public const string Builtin = "Builtin";

    /// <summary>进入 Login 前必须准备完成的资源。</summary>
    public const string Mandatory = "Mandatory";

}
