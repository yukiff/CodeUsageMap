# MixedDependencySample

単一 solution 内で複数 project の fan-in / fan-out を確認する representative sample。

含む観点:

- 5 project 構成
- App -> Core / Infrastructure / Abstractions の複数 outbound
- Tests -> App / Core の複数 inbound
- interface 実装
- event 購読 / 解除
- DI registration
- 複数 implementation
