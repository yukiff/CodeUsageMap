# CodeUsageMap 追加候補タスク

## 1. この文書の目的

本書は、現行実装を踏まえて追加で検討する価値がある候補タスクを整理した backlog である。

対象:

- 現時点で必須ではないが、品質や実用性を高める余地がある項目
- Mac 環境でも先行して整備できる補助タスク
- Windows 実機確認後に詰めるべき拡張タスク

対象外:

- 既存の正式な未実装タスク
- 仕様で明示済みの将来構想だけの抽象論

## 2. 品質・回帰候補

### C-103 export と actual Tool Window 表示の整合確認を追加する

現状:

- JSON / ViewModel JSON / DGML export はある
- shared presentation 契約までは `PresentationConsistencyProbe` で確認済み
- actual Tool Window 表示と export 結果の整合性確認は手動前提

やること:

- root
- incoming / outgoing / related
- diagnostics
- impact / risk summary の元になる relation 数

が export と UI で矛盾しないことを確認するテスト手順を追加する

完了条件:

- export 結果と actual Tool Window 表示の比較観点が文書化または自動化される

依存:

- Windows 実機確認があると望ましい

## 3. Windows 実機補助候補

### C-201 same-solution DLL 正規化の Windows 実機ケースを固定する

現状:

- Mac では probe で確認済み
- Windows 実機での source 正規化ケースが未固定

やること:

- same-solution の source project
- 同一 assembly を参照する binary reference project
- source に戻せるケース
- source に戻せないケース

を含む representative solution を用意する

完了条件:

- Windows 実機で binary reference 正規化の成否を安定して再現できる

依存:

- T-003

## 4. サンプル・検証資産候補

### C-303 複数プロジェクト mixed dependency sample solution を追加する

現状:

- `RepresentativeSample.sln` は 3 project の直線的な依存が中心
- cross-project、DI、event、override は見ているが、fan-in / fan-out の強い依存は薄い

やること:

- 4 project 以上の sample solution を追加する
- 複数の app / core / adapter / test project を含める
- 1 root から複数 project へ outbound するケース
- 複数 project から 1 root へ inbound するケース

完了条件:

- 単一 solution 内の複雑依存を representative asset で再現できる

依存:

- なし

### C-304 same-solution binary reference を actual solution で固定する

現状:

- same-solution DLL 正規化は `MetadataNormalizationProbe` の synthetic workspace で確認済み
- actual `.sln` に source project と binary-reference project を同居させた固定資産はない

やること:

- source project を含む
- 別 project が DLL reference で同一 assembly を参照する
- source に戻せるケースと戻せないケースを同じ solution で持つ

完了条件:

- Mac / Windows の両方で同じ asset を使って same-solution DLL 正規化を繰り返し検証できる

依存:

- C-201 と重なるため統合してもよい

### C-305 複数実装 / DI 曖昧性 sample を追加する

現状:

- `RepresentativeSample.sln` の `IWorkflow` は実質 1 実装
- 複数実装時の ranking、impact、density control の効き方は薄くしか見えていない

やること:

- 同一 interface に対する複数 implementation を追加する
- generic registration と `typeof(...)` registration を混在させる
- consumer 側に project をまたぐ複数利用箇所を追加する

完了条件:

- DI と implementation 展開が node 数増加時にも妥当か確認できる

依存:

- なし

### C-306 symbol 解決の曖昧性 sample を追加する

現状:

- 曖昧解決機構はある
- 単一 solution の複数 project で同名 type / overload / generic method を含む固定資産は薄い

やること:

- 同名 class を別 project / namespace に配置する
- overload を複数用意する
- generic method / extension method を含める

完了条件:

- `symbolResolution` 候補表示と `--symbol-index` の動作を representative asset で確認できる

依存:

- なし

### C-307 external package / metadata dependency sample を追加する

現状:

- external metadata の可視化と filter はある
- actual solution で NuGet package / framework extension method を使う固定 sample はない

やること:

- package reference を 1 つ持つ project を追加する
- framework / package の extension method 呼び出しを含める
- `Hide external` / `Hide System.*` / `Hide NuGet` の前提データを固定する

完了条件:

- external metadata 系 filter を representative asset で確認できる

依存:

- Windows 実機があると望ましいが、Core 側は Mac でも確認可能

### C-302 representative `.NET Framework 4.8` sample solution をリポジトリ内に固定する

現状:

- Windows 用チェックリストはある
- 実際に毎回同じ構成で確認する sample solution が未固定

やること:

- classic csproj
- `packages.config`
- project reference
- binary reference
- conditional compilation

を含む最小 sample solution を用意する

完了条件:

- `.NET Framework 4.8` 検証を同じ資産で繰り返せる

依存:

- T-004

## 5. 優先順位の提案

1. C-303 複数プロジェクト mixed dependency sample solution
2. C-304 actual solution での same-solution DLL 正規化固定
3. C-305 複数実装 / DI 曖昧性 sample
4. C-306 symbol 解決の曖昧性 sample
5. C-307 external package / metadata dependency sample
6. C-201 same-solution DLL 正規化の Windows ケース固定
7. C-302 `.NET Framework 4.8` representative sample solution
8. C-103 export と actual Tool Window 表示の整合確認
