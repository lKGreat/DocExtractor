# SecurityPlatform 产品化重构契约基线（v1）

> 本文档是下一阶段产品化重构的唯一契约基线之一。若与历史文档冲突，以本文与主计划为准。

## 1. 文档目标与范围

### 1.1 目标
- 在不改变既有安全规范、文档流程、等保约束的前提下，定义可落地的新一代平台契约。
- 统一“平台控制面 + 应用工作台 + 运行交付面 + 治理层”的对象模型、路由、API、安全与兼容规则。
- 为 12 Sprint 实施、测试与验收提供可执行合同。

### 1.2 范围
- **包含**：元模型、前端 IA、后端 `api/v1` 契约、请求头安全要求、弃用策略、测试断言。
- **不包含**：具体技术实现细节（ORM 语句、前端组件实现、部署脚本）。

---

## 2. 统一领域元模型（Canonical Domain Model）

所有新增模块必须使用显式强类型 DTO，不允许复用旧 DTO。

## 2.1 AppManifest（应用定义）
```json
{
  "id": "guid",
  "appKey": "string(unique)",
  "name": "string",
  "description": "string",
  "ownerTenantId": "string",
  "status": "Draft|Published|Archived",
  "currentReleaseId": "guid|null",
  "createdAt": "datetime",
  "updatedAt": "datetime",
  "version": "rowVersion"
}
```

## 2.2 AppRelease（应用发布）
```json
{
  "id": "guid",
  "appId": "guid",
  "releaseNo": "string(semver-like)",
  "sourceManifestVersion": "string",
  "runtimeSnapshotRef": "string",
  "isRollbackPoint": "bool",
  "impactSummary": "string",
  "createdBy": "string",
  "createdAt": "datetime"
}
```

## 2.3 RuntimeRoute（运行态路由）
```json
{
  "id": "guid",
  "appId": "guid",
  "appKey": "string",
  "pageKey": "string",
  "routePath": "/r/:appKey/:pageKey",
  "releaseId": "guid",
  "isEnabled": "bool",
  "authPolicyId": "guid",
  "updatedAt": "datetime"
}
```

## 2.4 PackageArtifact（迁移包）
```json
{
  "id": "guid",
  "artifactType": "Structure|SeedData|FullClone",
  "manifestVersion": "string",
  "checksum": "sha256",
  "sourceTenantId": "string",
  "createdAt": "datetime",
  "createdBy": "string"
}
```

## 2.5 LicenseGrant（离线许可）
```json
{
  "id": "guid",
  "licenseNo": "string(unique)",
  "tenantId": "string",
  "productEdition": "Community|Enterprise|Private",
  "capacity": {
    "maxUsers": 0,
    "maxApps": 0,
    "toolQuota": 0
  },
  "validFrom": "date",
  "validTo": "date",
  "signature": "string",
  "status": "Requested|Issued|Imported|Expired|Revoked"
}
```

## 2.6 ToolAuthorizationPolicy（工具授权策略）
```json
{
  "id": "guid",
  "tenantId": "string",
  "toolCode": "string",
  "policyMode": "Allow|Deny|Conditional",
  "approvalRequired": "bool",
  "rateLimitPerMinute": 0,
  "dailyQuota": 0,
  "effectiveFrom": "datetime",
  "effectiveTo": "datetime|null",
  "updatedBy": "string"
}
```

## 2.7 FlowDefinition（流程定义）
- 审批流（Approval）与工作流（Workflow）统一挂载到 `FlowDefinition`：
  - `flowType`: `Approval|Workflow`
  - `bindingTargetType`: `AppManifest|Release|RuntimeTask|ToolPolicy`
  - `bindingTargetId`: `guid`
  - `stateModel`: 显式状态机定义（禁止隐式状态跳转）

---

## 3. 前端信息架构与路由契约

## 3.1 固定三层入口
1. 平台控制面：`/console`
2. 应用工作台：`/apps/:appId/*`
3. 运行交付面：`/r/:appKey/:pageKey`

## 3.2 兼容入口
- `/settings/*` 继续保留，但标记 `Deprecated`，仅做兼容跳转与只读展示。

## 3.3 路由行为约束
- 任一 `appId`、`appKey`、`pageKey` 不合法时返回统一错误页 + 事件审计。
- 运行态路由必须绑定可用 `releaseId`；无发布态时禁止访问业务页。

---

## 4. 后端 API 契约（统一前缀：`/api/v1`）

> 说明：以下为契约最小集合（P0/P1），新增端点需同步追加到本文与 `.http` 契约用例。

## 4.1 平台面
- `GET /api/v1/platform/overview`
- `GET /api/v1/platform/resources`
- `GET /api/v1/platform/releases`

## 4.2 应用面
- `GET /api/v1/app-manifests`
- `POST /api/v1/app-manifests`
- `GET /api/v1/app-manifests/{id}`
- `PATCH /api/v1/app-manifests/{id}`
- `POST /api/v1/app-manifests/{id}/workspace/pages`
- `POST /api/v1/app-manifests/{id}/workspace/forms`
- `POST /api/v1/app-manifests/{id}/workspace/flows`
- `GET /api/v1/app-manifests/{id}/workspace/permissions`

## 4.3 运行面
- `GET /api/v1/runtime/apps/{appKey}/pages/{pageKey}`
- `GET /api/v1/runtime/tasks`
- `POST /api/v1/runtime/tasks/{taskId}/claim`
- `POST /api/v1/runtime/tasks/{taskId}/complete`
- `POST /api/v1/runtime/tasks/{taskId}/reject`

## 4.4 治理面
- `POST /api/v1/packages/export`
- `POST /api/v1/packages/import`
- `POST /api/v1/licenses/offline-request`
- `POST /api/v1/licenses/import`
- `POST /api/v1/licenses/validate`
- `GET /api/v1/tools/authorization-policies`
- `POST /api/v1/tools/authorization-policies`
- `POST /api/v1/tools/authorization-policies/simulate`
- `GET /api/v1/tools/authorization-policies/audit`

---

## 5. 写接口安全与一致性契约（强制）

## 5.1 强制请求头
所有写接口（`POST|PUT|PATCH|DELETE`）必须同时携带：
- `Idempotency-Key: <uuid>`
- `X-CSRF-TOKEN: <token>`

任一缺失时：
- 返回 `400`（缺参）或 `403`（CSRF 校验失败）
- 审计事件类型：`Security.ValidationRejected`

## 5.2 幂等语义
- 幂等键作用域：`tenantId + actorId + method + routeTemplate`
- 同键同载荷：返回首个成功结果（`200/201` 重放）
- 同键不同载荷：返回 `409 IDEMPOTENCY_CONFLICT`

## 5.3 跨租户访问
- 任何租户上下文不一致访问直接 `403 CROSS_TENANT_DENIED`
- 审计日志必须写入租户、主体、目标资源、拒绝原因。

## 5.4 敏感字段处理
- 审计持久化必须脱敏（示例：token、secret、证书串、手机号、邮箱前缀）。
- 日志/审计中禁止明文存储 License 私钥材料与签名原文。

---

## 6. 错误模型契约

统一错误响应：
```json
{
  "code": "UPPER_SNAKE_CASE",
  "message": "human readable",
  "traceId": "string",
  "details": {}
}
```

常见错误码：
- `IDEMPOTENCY_CONFLICT`
- `CROSS_TENANT_DENIED`
- `CSRF_TOKEN_INVALID`
- `RESOURCE_NOT_FOUND`
- `VALIDATION_FAILED`
- `POLICY_SIMULATION_DENIED`
- `LICENSE_SIGNATURE_INVALID`

---

## 7. 兼容与弃用策略

1. 旧接口保留 6 个月弃用窗口（仅安全修复，不新增功能）。
2. 新接口禁止复用旧 DTO，必须使用新命名空间和显式验证器。
3. 所有 Deprecated 接口需返回响应头：
   - `Deprecation: true`
   - `Sunset: <rfc1123 date>`
   - `Link: <migration doc>`

---

## 8. 测试契约（必须落地）

## 8.1 契约测试（.http）
每个新增/变更端点至少覆盖：
1. 成功路径
2. 鉴权失败
3. 幂等冲突
4. 跨租户拒绝

## 8.2 集成测试
覆盖链路：
- 平台 -> 应用 -> 运行态 -> 审批/工作流 -> 状态回写 -> 审计
- 包导入导出
- License 离线导入校验
- Tools 策略模拟与审计

## 8.3 前端 e2e + GUI 手动验收
- 三层入口切换
- 发布与回滚
- 运行态访问与任务处理
- 异常提示可见、可追踪

---

## 9. 版本控制

- 当前版本：`v1.0-baseline`
- 生效范围：12 Sprint 产品化重构
- 变更规则：任何对象字段、路由、错误码变更必须先更新本文，再进实现。
