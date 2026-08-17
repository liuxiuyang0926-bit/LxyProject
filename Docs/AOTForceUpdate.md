# AOT 整包强更

`gameConfig.json` 的 `data.version` 仅是 YooAsset Package Version，不能判断 APK 中的 AOT 代码是否兼容。当 `Game.Main`、`Game.Resource`、`Game.Scene`、`Game.Contracts` 等 AOT 程序集变更时，必须构建新 APK，并在远程配置中提高最低客户端版本。

## 服务器配置

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "version": "112",
    "downloadUrl": "https://cdn.example.com/game",
    "minimumAppVersion": "0.2.0",
    "minimumAndroidVersionCode": 2,
    "appDownloadUrl": "https://download.example.com/game.apk",
    "forceUpdateMessage": "本次更新包含客户端必要升级，请安装新版本。"
  }
}
```

- `minimumAppVersion` 与 `Application.version` 比较，格式为 1 到 4 段数字，例如 `0.2`、`1.3.5`。
- `minimumAndroidVersionCode` 与 APK `versionCode` 比较；Android 正式发布建议始终填写。
- 任一版本条件不满足就会停止 YooAsset/HybridCLR 启动，且不允许降级到内置资源。
- `appDownloadUrl` 在触发强更时必填，只接受 HTTP/HTTPS；可以是 APK 地址或跳转到应用商店的 HTTPS 页面。
- 旧服务端配置不含这些字段时保持原有行为，方便分阶段上线。

## 发布顺序

1. 提高 Unity `PlayerSettings.bundleVersion` 和 Android `Bundle Version Code`。
2. 重新生成 HybridCLR，构建 YooAsset，再构建并上传新 APK。
3. 验证新 APK 及 `appDownloadUrl` 可用。
4. 最后提高 `minimumAppVersion` / `minimumAndroidVersionCode` 并切换 `gameConfig.json`。

不要在新 APK 可下载之前提高最低版本，否则旧客户端会被阻断却无法完成升级。

## 编辑器验证

在 Start 场景 `[GameMain]` 的 `YooAssetLauncher` 上勾选“模拟客户端整包强更”即可验证强更 UI。该开关在 Player 中不生效。
