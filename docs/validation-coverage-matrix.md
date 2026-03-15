# CodeUsageMap 検証カバレッジマトリクス

## 1. 目的

本書は、仕様書の主要要件に対して、現在どこまで検証できているかを `Mac 検証済み / Windows 検証待ち / 未整備` の観点で整理する。

対象:

- [visual-studio-extension-spec.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/visual-studio-extension-spec.md) の主要要件
- 現在の probe、sample solution、validation script

## 2. 結論

- Core 解析ロジックと shared presentation 契約については、Mac 環境でかなり広く検証できている
- ただし、今回の要件全体を `Mac 環境だけで網羅的に検証済み` とまでは言えない
- 未充足の中心は `VSIX UI`、`MSBuildWorkspace`、`.NET Framework 4.8`、`Follow caret` 実機挙動、`VisualStudioSymbolContextService` 精度である

## 3. 要件別マトリクス

| 要件 | Mac | Windows | 根拠 |
|---|---|---|---|
| C# symbol から root を生成 | 検証済み | 必須 | representative sample probe で method・class・interface・property・event root を固定確認済み |
| method / class / interface / property / event を root 候補とする | 検証済み | 必須 | representative sample probe で root 種別を固定確認済み |
| solution 全体の参照収集 | 検証済み | 必須 | `run_mac_validation.sh` と CLI smoke |
| implementation / override / 継承収集 | 検証済み | 必須 | representative sample probe と `EdgeKindProbe` で implementation / override の analyzer 実出力を固定確認済み |
| event 購読 / 解除 / 発火収集 | 一部検証 | 必須 | representative sample probe で subscription / unsubscription / raise を固定確認済み。WPF 表示と Windows 実機は未確認 |
| `DirectCall` / `Reference` / `Implements` / `Overrides` / `InstantiatedBy` / event edge 分類 | 検証済み | 必須 | `EdgeKindProbe` で current analyzer の edge kind 全種類を固定確認済み |
| `Depth` 展開 | 一部検証 | 必須 | 既存 CLI smoke と sample で確認済み。全 root 種別横断の固定検証は未整備 |
| `ExcludeTests` / `ExcludeGenerated` | 一部検証 | 必須 | analyzer 側の既存 sample で確認済み。representative sample には未統合 |
| JSON / ViewModel JSON / DGML 出力 | 検証済み | 望ましい | `SerializationProbe`, `SnapshotRegressionProbe` |
| output envelope / partialResult / diagnostics | 検証済み | 望ましい | `SerializationProbe` |
| analyzer 出力と shared presentation 契約の整合 | 検証済み | 望ましい | `PresentationConsistencyProbe` |
| same-solution DLL source 正規化 | 検証済み | 必須 | `MetadataNormalizationProbe` で Mac は確認済み。Windows 実機未確認 |
| DI 解決関係の可視化 | 一部検証 | 必須 | `DiProbe` と representative sample probe で core 側確認済み。VSIX UI 未確認 |
| 影響範囲サマリ | 検証済み | 望ましい | `NodeAssessmentProbe` で shared ロジック確認済み。VSIX UI 未確認 |
| 変更リスク指標 | 検証済み | 望ましい | `NodeAssessmentProbe` で shared ロジック確認済み。VSIX UI 未確認 |
| graph canvas shared layout | 検証済み | 望ましい | `GraphCanvasProbe` |
| graph canvas WPF 表示と操作 | 未検証 | 必須 | zoom / pan / minimap / collapse / reroot / display mode は Windows 実機待ち |
| Tool Window の root 最小表示 500ms | 未検証 | 必須 | Mac では WPF / VSIX 実測不可 |
| progress / cancel | 未検証 | 必須 | 実装済みだが VSIX 実機未確認 |
| `Follow caret` ON / OFF | 未検証 | 必須 | 実装済みだが VSIX 実機未確認 |
| 右クリックメニュー起動 | 未検証 | 必須 | Windows 実機待ち |
| コードジャンプ | 未検証 | 必須 | WPF / VSIX 実機待ち |
| `VisualStudioSymbolContextService` 精度 | 未検証 | 必須 | overload / property / event / attribute / generic method は Windows 実機待ち |
| `MSBuildWorkspace` 経路 | 未検証 | 必須 | `run_windows_validation.ps1` は追加済みだが未実行 |
| `.NET Framework 4.8` solution | 未検証 | 必須 | sample 未作成、Windows 実機待ち |

## 4. 現在の Mac 検証資産

### 4.1 一括実行

- [run_mac_validation.sh](/Users/funabashiyuuki/programming/CodeUsageMap/scripts/run_mac_validation.sh)

### 4.2 Core / 出力 / 正規化

- [CodeUsageMap.SerializationProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.SerializationProbe/Program.cs)
- [CodeUsageMap.SnapshotRegressionProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.SnapshotRegressionProbe/Program.cs)
- [CodeUsageMap.MetadataNormalizationProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.MetadataNormalizationProbe/Program.cs)
- [CodeUsageMap.CacheProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.CacheProbe/Program.cs)

### 4.3 表示モデル / 指標

- [CodeUsageMap.GraphCanvasProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.GraphCanvasProbe/Program.cs)
- [CodeUsageMap.NodeAssessmentProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.NodeAssessmentProbe/Program.cs)
- [CodeUsageMap.PresentationConsistencyProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.PresentationConsistencyProbe/Program.cs)

### 4.4 representative sample

- [RepresentativeSample.sln](/Users/funabashiyuuki/programming/CodeUsageMap/samples/RepresentativeSample/RepresentativeSample.sln)
- [CodeUsageMap.RepresentativeSampleProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.RepresentativeSampleProbe/Program.cs)
- [CodeUsageMap.EdgeKindProbe](/Users/funabashiyuuki/programming/CodeUsageMap/tools/CodeUsageMap.EdgeKindProbe/Program.cs)

## 5. Mac で未網羅の項目

### 5.1 Mac では shared 契約までは確認できるが WPF 実画面までは確認できないもの

- export 結果と actual Tool Window 表示の完全一致
- graph canvas preview の実描画と hit test
- zoom / pan / minimap / context menu の実操作体験

### 5.2 Mac では完結しないもの

- VSIX Tool Window 実表示
- graph canvas WPF 実操作
- `Follow caret`
- progress / cancel の UI 挙動
- `VisualStudioSymbolContextService` 精度
- `MSBuildWorkspace`
- `.NET Framework 4.8`

## 6. 次のアクション

### 6.1 Mac で先に詰める

1. shared presentation と WPF actual UI の差分観点を Windows UI regression に寄せて固定する

### 6.2 Windows で詰める

1. [run_windows_validation.ps1](/Users/funabashiyuuki/programming/CodeUsageMap/scripts/run_windows_validation.ps1) 実行
2. [run_windows_ui_smoke.ps1](/Users/funabashiyuuki/programming/CodeUsageMap/scripts/run_windows_ui_smoke.ps1) 実行
3. [windows-net48-validation-checklist.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-net48-validation-checklist.md) に沿って `.NET Framework 4.8` を確認
