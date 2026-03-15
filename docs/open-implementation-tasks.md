# CodeUsageMap 未実装タスク一覧

## 1. この文書の目的

本書は、現行実装に対して未実装の項目だけを整理したタスク一覧である。

対象:

- 仕様書 [visual-studio-extension-spec.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/visual-studio-extension-spec.md) で要求しているが、未実装のもの
- Windows 実機確認が未完了のもの
- PoC から実用段階へ進むために不足しているもの

対象外:

- すでに実装済みの機能
- 過去の検討メモ
- 将来構想だけで優先度未定の抽象的アイデア

## 2. 優先度高

### T-001 Follow caret を VSIX に実装する

現状:

- 実装済み
- Windows 実機未確認

やること:

- Windows 実機で `Follow caret` ON/OFF を確認する
- debounce 挙動を確認する
- `Refresh`、明示 reroot、コードジャンプ後の追従を確認する

完了条件:

- OFF では自動追従しない
- ON では caret 移動後に root が追従する
- ダブルクリックのコードジャンプ後、ON のときだけ結果的に reroot される

依存:

- VSIX 実機確認環境

### T-002 Windows 実機で VSIX を restore / build / install / debug する

現状:

- Mac 側では未検証
- VSIX scaffold は存在するが実機確認未完了

やること:

- Windows で `CodeUsageMap.Vsix.csproj` を restore
- Experimental Instance 起動
- コンテキストメニュー表示確認
- Tool Window 起動確認
- command 実行、refresh、cancel 確認

完了条件:

- VSIX がビルドできる
- Visual Studio 上で command が出る
- Tool Window が起動する

依存:

- Windows + Visual Studio 2022

### T-003 Windows 実機で `MSBuildWorkspace` 経路を確認する

現状:

- Mac では `adhoc` 中心に確認済み
- `msbuild` 既定経路の Windows 実動は未確認

やること:

- Windows で solution load を確認
- symbol resolve を確認
- reference / implementation / event collect を確認
- `ProjectReference` と classic csproj を確認

完了条件:

- `MSBuildWorkspace` で解析が通る
- root symbol 解決と参照収集が Windows 実機で安定する

依存:

- T-002

### T-004 `.NET Framework 4.8` solution を Windows 実機で検証する

現状:

- 仕様には含めたが、実機検証未完了

やること:

- classic csproj
- `packages.config`
- 条件付きコンパイル
- Framework 固有参照
- project reference と binary reference

完了条件:

- チェックリスト [windows-net48-validation-checklist.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-net48-validation-checklist.md) を埋められる
- 報告テンプレート [windows-net48-validation-report-template.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-net48-validation-report-template.md) に結果を残せる

依存:

- T-002
- T-003

## 3. UI 実装タスク

## 4. 解析・フィルタ拡張タスク

## 5. DI / 影響範囲タスク

## 6. 品質・検証タスク

### T-405 `VisualStudioSymbolContextService` の精度検証を行う

現状:

- Windows 実機未確認

やること:

- overload
- property
- event
- attribute
- generic method

完了条件:

- 誤解決パターンが記録される

### T-407 Windows 検証フローを自動化する

現状:

- `scripts/run_windows_validation.ps1` を追加済み
- 代表 sample solution と `MSBuildWorkspace` smoke の自動実行土台はある
- Windows 実機で未確認

やること:

- Windows で script を実行して補正する
- VSIX build 成功を確認する
- representative sample smoke を確認する
- `.NET Framework 4.8` sample solution を接続する
- 必要なら CI ジョブ化する

完了条件:

- Windows 上で検証手順を半自動または自動で再現できる
- 少なくとも build、restore、basic smoke の成否が機械的に分かる

### T-408 クロスプラットフォーム検証レポートを自動生成する

現状:

- `scripts/generate_validation_summary.sh` を追加済み
- Mac report の要約は自動生成できる
- Windows report 入力での実運用は未確認

やること:

- Windows report を入力したときの summary を確認する
- 必要なら PowerShell 版 summary も追加する
- commit / worktree 情報の出力方針を確定する

完了条件:

- 1 回の検証結果を共有しやすいレポートにまとめられる
- Windows 実機確認の抜け漏れを減らせる

## 7. 将来拡張タスク

### T-501 graph 保存と再表示

### T-502 循環参照ハイライト

### T-503 ホットパス強調

### T-504 C++ 解析アダプタ

### T-505 mixed solution 可視化

### T-506 差分レビュー支援

## 8. 実行順の推奨

1. T-001 Follow caret
2. T-002 Windows VSIX 実機確認
3. T-003 `MSBuildWorkspace` 実機確認
4. T-004 `.NET Framework 4.8` 実機確認
5. T-407 Windows 検証フロー自動化
6. T-408 クロスプラットフォーム検証レポート自動化
7. T-202 以降のフィルタ拡張
8. T-201 以降のフィルタ拡張
9. T-301 以降の DI / 影響範囲
