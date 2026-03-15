# CodeUsageMap Windows 移行 1枚ガイド

## 1. 目的

Mac で開発した `CodeUsageMap` を Windows 環境へ移し、VSIX のビルド、実機確認、検証記録までを最短で進めるための実行ガイドである。

対象:

- Windows 版 Visual Studio 2022 での VSIX 検証
- `MSBuildWorkspace` 経路の確認
- `.NET Framework 4.8` を含む実機検証
- Windows 側の未完了タスク整理

## 2. 最初に開くもの

1. 仕様書: [visual-studio-extension-spec.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/visual-studio-extension-spec.md)
2. 未完了タスク: [open-implementation-tasks.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/open-implementation-tasks.md)
3. 検証カバレッジ: [validation-coverage-matrix.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/validation-coverage-matrix.md)

## 3. Windows 側の主なゴール

- VSIX を restore / build / install できる
- Tool Window を起動し、コマンド、Refresh、Cancel、Follow caret を確認できる
- `MSBuildWorkspace` で solution 解析が通る
- `.NET Framework 4.8` solution を確認できる
- representative sample と UI smoke を再実行できる

## 4. 推奨実行順

1. VSIX を restore / build する
2. Experimental Instance で command と Tool Window を確認する
3. `MSBuildWorkspace` 経路を確認する
4. representative sample で basic smoke を行う
5. `Follow caret`、progress、cancel、graph canvas preview を確認する
6. `.NET Framework 4.8` solution を確認する
7. 検証結果を report に残す

## 5. 実行に使うファイル

### 5.1 Windows 自動検証

- [run_windows_validation.ps1](/Users/funabashiyuuki/programming/CodeUsageMap/scripts/run_windows_validation.ps1)
- [run_windows_ui_smoke.ps1](/Users/funabashiyuuki/programming/CodeUsageMap/scripts/run_windows_ui_smoke.ps1)

### 5.2 representative sample

- [RepresentativeSample.sln](/Users/funabashiyuuki/programming/CodeUsageMap/samples/RepresentativeSample/RepresentativeSample.sln)
- [README.md](/Users/funabashiyuuki/programming/CodeUsageMap/samples/RepresentativeSample/README.md)

### 5.3 `.NET Framework 4.8` 検証

- [windows-net48-validation-checklist.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-net48-validation-checklist.md)
- [windows-net48-validation-report-template.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-net48-validation-report-template.md)

### 5.4 UI 回帰観点

- [windows-ui-regression-strategy.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-ui-regression-strategy.md)

## 6. Windows で確認すべき未完了タスク

優先度高:

- `T-001` Follow caret の実機確認
- `T-002` VSIX restore / build / install / debug
- `T-003` `MSBuildWorkspace` 実動確認
- `T-004` `.NET Framework 4.8` solution 検証
- `T-405` `VisualStudioSymbolContextService` 精度確認
- `T-407` Windows 検証フロー自動化の実運用確認
- `T-408` クロスプラットフォーム検証レポートの Windows 入力確認

詳細は [open-implementation-tasks.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/open-implementation-tasks.md) を参照する。

## 7. 代表的な確認項目

### 7.1 VSIX 起動

- コマンドが表示される
- Tool Window が開く
- root 最小情報が先に表示される
- progress / cancel が見える

### 7.2 graph canvas preview

- root が中央に出る
- inbound が左、outbound が右に出る
- 選択、ダブルクリック、右クリック、zoom、pan、collapse が動く

### 7.3 symbol 解決

- method
- class
- interface
- property
- event
- overload
- generic method

### 7.4 source 正規化

- `ProjectReference`
- same-solution DLL reference
- source に戻せるケース
- source に戻せないケース

### 7.5 `.NET Framework 4.8`

- classic csproj
- `packages.config`
- 条件付きコンパイル
- Framework 固有参照
- project reference
- binary reference

## 8. 検証結果の残し方

1. 自動検証 script の出力を保存する
2. `.NET Framework 4.8` は checklist を埋める
3. report template に結果を書く
4. cross-platform summary を生成する

要約生成:

- [generate_validation_summary.sh](/Users/funabashiyuuki/programming/CodeUsageMap/scripts/generate_validation_summary.sh)

## 9. 現時点の前提

- Mac 側の自動検証は完了している
- Core 解析、shared presentation、representative sample、edge kind 回帰は Mac で確認済み
- Windows 側は script と手順は揃っているが、実機実行は未完了

## 10. 迷ったら見る順番

1. [windows-migration-one-pager.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-migration-one-pager.md)
2. [open-implementation-tasks.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/open-implementation-tasks.md)
3. [windows-net48-validation-checklist.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-net48-validation-checklist.md)
4. [windows-ui-regression-strategy.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-ui-regression-strategy.md)
