# CodeUsageMap Windows / .NET Framework 4.8 検証結果テンプレート

## 1. 基本情報

- 実施日:
- 担当者:
- Windows version:
- Visual Studio version:
- VSIX project revision:
- 検証対象 solution:
- solution 種別:
  - SDK-style / classic csproj / mixed
- `.NET Framework 4.8` project 数:
- `packages.config` project 有無:
- 条件付きコンパイル有無:

## 2. 環境情報

### 2.1 Visual Studio / SDK

- `Microsoft.VisualStudio.SDK` version:
- `Microsoft.VSSDK.BuildTools` version:
- `.NET Framework 4.8 Developer Pack`:
- `MSBuildWorkspace` load path:

### 2.2 検証対象 solution の特徴

- project 数:
- C# project 数:
- class library 数:
- executable project 数:
- `ProjectReference` 数:
- event 使用箇所:
- interface / override 使用箇所:

## 3. VSIX build / install

### 3.1 restore

- 結果: 成功 / 失敗
- 補足:

### 3.2 build

- 結果: 成功 / 失敗
- warning:
- error:

### 3.3 Experimental Instance

- 起動: 成功 / 失敗
- VSIX load: 成功 / 失敗
- command 表示: 成功 / 失敗

## 4. `MSBuildWorkspace` / `.NET Framework 4.8` 読み込み結果

### 4.1 solution load

- 結果: 成功 / 失敗
- 所見:

### 4.2 classic csproj

- 結果: 成功 / 失敗
- 所見:

### 4.3 `packages.config`

- 結果: 成功 / 失敗 / 対象なし
- 所見:

### 4.4 条件付きコンパイル

- 結果: 成功 / 失敗 / 対象なし
- active configuration:
- 所見:

## 5. 機能確認

### 5.1 symbol resolve

| 観点 | 結果 | 補足 |
| --- | --- | --- |
| method |  |  |
| property |  |  |
| event |  |  |
| interface method |  |  |
| override method |  |  |
| attribute usage |  |  |
| generic method |  |  |
| overload |  |  |

### 5.2 解析結果

| 観点 | 結果 | 補足 |
| --- | --- | --- |
| source references |  |  |
| implementations |  |  |
| overrides |  |  |
| event subscription |  |  |
| event unsubscription |  |  |
| event raise |  |  |
| `Depth 1/2/3` |  |  |
| `ExcludeTests` |  |  |
| `ExcludeGenerated` |  |  |

### 5.3 Tool Window

| 観点 | 結果 | 補足 |
| --- | --- | --- |
| root 表示 |  |  |
| incoming 表示 |  |  |
| outgoing 表示 |  |  |
| related 表示 |  |  |
| details 表示 |  |  |
| refresh |  |  |
| cancel |  |  |
| status 表示 |  |  |
| code jump |  |  |

## 6. 既知の問題 / 発見事項

1.
2.
3.

## 7. 収集ログ / 証跡

- build log:
- VSIX install log:
- 例外メッセージ:
- スクリーンショット:
- 出力 JSON / DGML:

## 8. 総合判定

- 判定: 合格 / 条件付き合格 / 不合格
- 判定理由:

## 9. 次アクション

1.
2.
3.
