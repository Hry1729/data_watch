# 验证记录

这里保留按日期归档的阶段性测试、联调与故障分析。它们反映**当时的代码、环境和结论**；当前设计、API 与部署方式仍以 `01`～`04` 目录的权威文档及现行代码为准。原始日志和运行产物留在 `outputs/`、`evidence/`，不要把旧报告的性能数字直接当作当前生产容量。

## 2026 年 9 月

完整文件位于 [2026-09](2026-09/)；以下按用途选读。

| 主题 | 记录 |
| --- | --- |
| 最新运行状态 | [2026-09-29 运行状态与性能改动](2026-09/2026-09-29-运行状态与性能改动.md) |
| 测试准入与验收 | [测试准入复核](2026-09/test-readiness-review-20260926.md)、[隔离测试 run2](2026-09/acceptance-run2-20260926.md)、[两轮性能复核](2026-09/performance-verification-recheck-20260926.md)、[第二轮补充](2026-09/performance-verification-recheck-20260926-round2.md) |
| 性能诊断与整改 | [初次优化交付](2026-09/performance-optimization-delivery-20260924.md)、[验证续报](2026-09/performance-optimization-validation-20260924.md)、[诊断](2026-09/performance-diagnosis-20260925.md)、[修复报告](2026-09/performance-fix-report-20260925.md)、[更正与整改](2026-09/performance-review-remediation-20260925.md)、[三包交付](2026-09/performance-three-package-delivery-20260925.md)、[定向诊断](2026-09/directed-diagnosis-20260927.md) |
| 联调与恢复 | [联合测量](../joint-measurement-20260927.md)、[阶梯联调](../joint-ladder-20260927.md)、[有界回放设计](2026-09/bounded-inflight-replay-design-20260927.md)、[中心运行时修复](2026-09/central-runtime-fix-20260927.md)、[中心 Redis 恢复](2026-09/central-restore-20260928.md)、[停机排空修复](2026-09/central-shutdown-drain-fix-20260928.md) |
| 其他交付与故障 | [后端 Docker 验证](2026-09/docker-backend-validation-20260924.md)、[驱动生命周期交付](2026-09/driver-lifecycle-delivery.md)、[可靠性评审交付](2026-09/reliability-review-delivery.md) |
| 小型 JSON 证据 | `2026-09/collector-*-profile-20260924.json`、`2026-09/receipt-v2-fault-validation-20260924.json`、`2026-09/tdengine-fault-validation-20260924.json` |

## 2026 年 10 月

完整文件位于 [2026-10](2026-10/)。

| 主题 | 记录 |
| --- | --- |
| 审计整改与核验 | [2026-10-06 整改提交与核验记录](2026-10/2026-10-06-整改提交与核验记录.md) — B1–B50 / D1–D39 / S1–S6 的逐项提交对应、独立只读复核、回归门禁证据与现场剩余事项；主清单见 [`docs/Data_Watch_综合审查与Agent整改执行清单_6845e80.md`](../Data_Watch_综合审查与Agent整改执行清单_6845e80.md) |

联合测量与阶梯联调两篇仍留在 `docs/` 根部，因为 `evidence/` 下的 `SHA256SUMS.txt` 使用其原始相对路径和内容哈希；挪动或改写会使现存证据无法按清单复核。`docs/tdengine-schema-20260929.json` 也是本机数据库恢复时的原始 schema 导出，保留原路径。`outputs/` 内各批次原始结果与 `evidence/` 下的签名清单也保持原位。
