# 感应式出入库测试方案

## 1. 测试目标

- 测试主题：感应式出入库功能。
- 测试范围：
  - 感应式服务接口测试。
  - 感应式回调接口测试。
  - 感应式业务流程测试。
- 覆盖系统：
  - 客户端：`WMSApp`
  - 后端：`SmartFactoryWebApi`
  - 外部设备接口：感应料架服务 `InductionRackService`
  - 实时通知：SignalR Hub `/hubs/induction`

## 2. 当前版本与环境基线

- 测试日期基线：`2026-05-13`
- 当前发布版本基线：`1.0.13`
- 客户端后端地址：`http://10.50.77.246:5067`
- 感应料架服务地址：`http://10.50.77.246:8091`
- 客户端当前仓库：`616`
- 后端允许的感应仓库：`616`、`621`
- SignalR Hub 路由：`/hubs/induction`
- 入库回调事件名：`ReceiveDepositCallback`
- 出库回调事件名：`ReceivePickCallback`

## 3. 测试前置条件

### 3.1 环境准备

- `WMSApp` 客户端可正常启动，并可访问 `http://10.50.77.246:5067`。
- `SmartFactoryWebApi` 已启动，且可访问 `api/induction/*` 接口。
- 感应料架服务 `http://10.50.77.246:8091` 可访问。
- 测试机与服务端之间网络正常，无防火墙拦截。
- 测试机能够建立 SignalR 连接到 `/hubs/induction`。

### 3.2 数据准备

- 至少准备 1 个感应料架，且该料架下存在：
  - 可用空库位 `IsEnable='Y'`
  - 已占用库位 `IsEnable='N'`
- 至少准备 2 个测试仓库：
  - `616`：主测试仓库
  - `621`：跨仓校验仓库
- 至少准备以下条码数据：
  - 未入感应架条码：`IsRack='N'`
  - 已入感应架条码：`IsRack='Y'`
  - 数量有效条码：`BarQty > 0`
  - 缺少原库位条码：`BinNo` 为空
- 至少准备以下异常数据：
  - 不属于当前仓库的条码
  - 不存在的条码
  - 不存在的库位
  - 非感应库位

### 3.3 执行顺序

1. 先测环境与联通性。
2. 再测后端服务接口。
3. 再测回调接口与 SignalR 广播。
4. 最后测完整业务流程和异常流程。

## 4. 重点风险

1. 客户端允许仓库列表为 `601/616/621`，但后端感应仓库白名单当前只有 `616/621`。若在客户端切到 `601`，前端不会拦截，但后端会返回“仓库不在感应料架允许列表中”。
2. 回调接口对外响应字段大小写是固定契约，必须严格验证返回字段为 `Success / Message / Data`。
3. 感应式流程依赖 SignalR 广播给页面收尾，若接口成功但 Hub 未连通，用户界面将无法正确完成状态切换。
4. 出库回调存在“非法出库”路径，`DetailsJson` 为空或 `OutStockType != 2` 时，应按非法出库处理，而不是普通失败。

## 5. 核心接口清单

### 5.1 入库接口

| 接口 | 方法 | 说明 |
|---|---|---|
| `/api/induction/entry/validate-shelf` | `POST` | 校验料架、仓库归属和空库位数量 |
| `/api/induction/entry/deposit` | `POST` | 发起感应入库请求 |
| `/api/induction/entry/cancel` | `POST` | 取消待入库请求 |
| `/api/induction/entry/callback` | `POST` | 设备入库回调 |

### 5.2 出库接口

| 接口 | 方法 | 说明 |
|---|---|---|
| `/api/induction/pick/item-suggestions` | `POST` | 料号联想 |
| `/api/induction/pick/query` | `POST` | 查询可出库条码并预览亮灯 |
| `/api/induction/pick/start` | `POST` | 启动感应出库 |
| `/api/induction/pick/cancel` | `POST` | 取消待出库请求 |
| `/api/induction/pick/callback` | `POST` | 设备出库回调 |

### 5.3 灯光接口

| 接口 | 方法 | 说明 |
|---|---|---|
| `/api/induction/light/empty-locations` | `POST` | 点亮料架空库位 |
| `/api/induction/light/off-empty-locations` | `POST` | 熄灭料架空库位 |

## 6. 接口测试用例

### 6.1 入库接口测试

| 用例ID | 测试项 | 请求关键数据 | 预期结果 |
|---|---|---|---|
| IE-API-01 | 验证感应料架成功 | `shelfCode=有效感应料架`，`warehouseLocation=616` | `success=true`，返回 `EmptyLocationCount > 0` |
| IE-API-02 | 料架不存在 | `shelfCode=不存在` | `success=false`，提示料架不存在 |
| IE-API-03 | 料架不是感应料架 | `shelfCode=普通料架` | `success=false`，提示不是感应料架 |
| IE-API-04 | 料架不属于当前仓库 | `shelfCode=621仓料架`，`warehouseLocation=616` | `success=false`，提示仓库不匹配 |
| IE-API-05 | 料架无空库位 | 感应料架全部占满 | `success=false`，提示没有可用空库位 |
| IE-API-06 | 发起入库成功 | `barcode=有效未入架条码` | `success=true`，提示“已发送入库请求，等待料架回调” |
| IE-API-07 | 条码不存在 | `barcode=不存在条码` | `success=false`，提示条码不存在 |
| IE-API-08 | 条码已入库 | `barcode=IsRack='Y'` | `success=false`，提示已入库 |
| IE-API-09 | 条码不属于当前仓库 | `barcode=跨仓条码` | `success=false`，提示仓库不匹配 |
| IE-API-10 | 条码缺少原库位 | `barcode.BinNo=null` | `success=false`，提示无法执行感应入库 |
| IE-API-11 | 取消入库成功 | `barcode=待回调条码` | `success=true`，提示已取消入库 |
| IE-API-12 | 取消入库失败 | 外部设备接口返回失败或超时 | `success=false`，提示取消失败原因 |

### 6.2 出库接口测试

| 用例ID | 测试项 | 请求关键数据 | 预期结果 |
|---|---|---|---|
| IP-API-01 | 料号联想成功 | `keyword=有效料号关键字` | `success=true`，返回候选料号列表 |
| IP-API-02 | 联想关键字为空 | `keyword=''` | `success=true`，返回空列表 |
| IP-API-03 | 查询出库成功 | `itemNo=有效料号`，`warehouseLocation=616` | `success=true`，返回可出库条码列表并完成预览亮灯 |
| IP-API-04 | 查询数量不足 | `requiredQty` 大于现有总量 | `success=false`，提示货架物料不满足需求 |
| IP-API-05 | 查询无结果 | 料号不在感应架 | `success=false`，提示无可出库条码 |
| IP-API-06 | 启动出库成功 | `labelIds=有效待拣条码集合` | `success=true`，提示等待拣货回调 |
| IP-API-07 | 启动出库时条码失效 | 标签中包含不在当前感应仓的条码 | `success=false`，提示部分条码失效 |
| IP-API-08 | 启动出库时空列表 | `labelIds=[]` | `success=false`，提示条码列表不能为空 |
| IP-API-09 | 取消出库成功 | `labelIds=待拣条码集合` | `success=true`，提示已取消出库 |
| IP-API-10 | 取消出库失败 | 外部设备接口返回失败或超时 | `success=false`，提示取消失败原因 |

### 6.3 灯光接口测试

| 用例ID | 测试项 | 请求关键数据 | 预期结果 |
|---|---|---|---|
| IL-API-01 | 点亮空库位成功 | `shelfCode=有效感应料架`，`color=2` | `success=true`，返回“亮灯成功” |
| IL-API-02 | 熄灭空库位成功 | `shelfCode=有效感应料架`，`color=0` | `success=true`，返回“熄灯成功” |
| IL-API-03 | 外部设备超时 | 模拟设备服务不可达 | `success=false`，返回超时或失败提示 |

## 7. 回调接口测试

### 7.1 入库回调测试

| 用例ID | 测试项 | 请求关键数据 | 预期结果 |
|---|---|---|---|
| IE-CB-01 | 正常入库回调 | `LabelId=有效条码`，`Location=有效空库位`，`DetailsJson` 完整 | HTTP 200；响应字段为 `Success/Message/Data`；数据库完成库存迁移；Hub 广播成功 |
| IE-CB-02 | `DetailsJson` 为空 | `DetailsJson=null` | HTTP 200；`Success=false`；消息提示缺少 `DetailsJson`；Hub 广播失败结果 |
| IE-CB-03 | `DetailsJson` 非法 JSON | `DetailsJson='abc'` | HTTP 200；`Success=false`；提示解析失败 |
| IE-CB-04 | 条码不存在 | `LabelId=不存在条码` | HTTP 200；`Success=false`；提示条码不存在 |
| IE-CB-05 | 原库位与目标库位相同 | `SourceBinNo == Location` | HTTP 200；`Success=false`；提示不能重复上架 |
| IE-CB-06 | 目标库位不存在 | `Location=不存在库位` | HTTP 200；`Success=false`；提示库位不存在 |
| IE-CB-07 | 目标库位非感应库位 | `Location=普通库位` | HTTP 200；`Success=false`；提示不是感应料架库位 |
| IE-CB-08 | 目标库位已占用 | `targetShelf.IsEnable='N'` | HTTP 200；`Success=false`；提示已被占用 |
| IE-CB-09 | 原库位库存不足 | `StockQty < BarQty` | HTTP 200；`Success=false`；提示事务失败 |

### 7.2 出库回调测试

| 用例ID | 测试项 | 请求关键数据 | 预期结果 |
|---|---|---|---|
| IP-CB-01 | 正常出库回调 | `DetailsJson.OutStockType=2`，仓库正确 | HTTP 200；`Success=true`；库位释放；条码 `IsRack='N'`；`BinNo=null`；Hub 广播成功 |
| IP-CB-02 | `DetailsJson` 为空 | `DetailsJson=null` | HTTP 200；按非法出库处理；响应 `Success=false`；Hub 消息 `IsIllegal=true` |
| IP-CB-03 | `OutStockType != 2` | `OutStockType=1` | HTTP 200；按非法出库处理；释放库位；不清空条码库位 |
| IP-CB-04 | `DetailsJson` 非法 JSON | `DetailsJson='abc'` | HTTP 200；`Success=false`；提示解析失败 |
| IP-CB-05 | 回调库位与条码当前库位不一致 | `bar.BinNo != Location` | HTTP 200；`Success=false`；提示库位不一致 |
| IP-CB-06 | 正常出库但库位不是占用状态 | `IsEnable != 'N'` | HTTP 200；`Success=false`；提示无法确认正常出库 |
| IP-CB-07 | 回调条码不存在 | `LabelId=不存在条码` | HTTP 200；`Success=false`；提示条码不存在 |

### 7.3 回调响应契约测试

所有回调接口都需要额外验证以下内容：

- HTTP 状态码固定为 `200`。
- 响应 JSON 顶层字段必须是：
  - `Success`
  - `Message`
  - `Data`
- 不允许输出小写字段：
  - `success`
  - `message`
  - `data`

### 7.4 推荐回调报文样例

入库回调：

```json
{
  "labelId": "BAR-IND-001",
  "location": "R616A0101",
  "detailsJson": "{\"warehouseLocation\":\"616\",\"barGuid\":\"GUID-001\",\"sourceBinNo\":\"TEMP-001\",\"operationTime\":\"2026/05/13 09:30:00\"}"
}
```

出库回调：

```json
{
  "labelId": "BAR-IND-002",
  "location": "R616A0102",
  "detailsJson": "{\"warehouseLocation\":\"616\",\"outStockType\":2,\"operationTime\":\"2026/05/13 09:35:00\"}"
}
```

## 8. SignalR 测试点

| 用例ID | 测试项 | 预期结果 |
|---|---|---|
| HUB-01 | 客户端打开感应入库页 | 自动连接 `/hubs/induction` |
| HUB-02 | 客户端打开感应出库页 | 自动连接 `/hubs/induction` |
| HUB-03 | 入库回调成功后广播 | 客户端收到 `ReceiveDepositCallback`，页面状态从等待中恢复 |
| HUB-04 | 出库回调成功后广播 | 客户端收到 `ReceivePickCallback`，条码状态更新为成功 |
| HUB-05 | 非法出库广播 | 客户端收到 `ReceivePickCallback`，且 `IsIllegal=true` |
| HUB-06 | 页面关闭后再回调 | 页面不再继续处理旧回调 |
| HUB-07 | SignalR 断线重连 | 连接断开后自动重连，恢复后可继续接收回调 |

## 9. 业务流程测试

### 9.1 感应入库主流程

1. 打开感应入库页面。
2. 确认页面已连接 SignalR。
3. 输入有效感应料架号并验证。
4. 确认页面显示空库位数量，空库位已亮灯。
5. 扫描有效未入架条码。
6. 确认页面进入“等待料架响应”状态。
7. 触发设备入库回调。
8. 确认页面收到成功提示，条码加入已入库列表。
9. 确认库位被占用，条码 `IsRack='Y'`，`BinNo` 更新为目标库位。

### 9.2 感应入库取消流程

1. 完成料架验证。
2. 扫描条码并发起入库。
3. 在回调前点击取消。
4. 确认页面状态恢复为空闲。
5. 确认外部设备对应标签已移除。
6. 再次扫描同一条码时可以重新发起请求。

### 9.3 感应出库主流程

1. 打开感应出库页面。
2. 确认页面已连接 SignalR。
3. 输入有效料号，可选输入需求数量。
4. 执行查询，确认返回待拣条码列表。
5. 确认对应条码已完成预览亮灯。
6. 点击开始拣货。
7. 确认页面进入“拣货中”状态。
8. 触发设备正常出库回调。
9. 确认列表条码状态逐条更新为成功。
10. 全部条码完成后，页面恢复空闲。

### 9.4 感应出库非法出库流程

1. 先查询并启动拣货。
2. 使用空 `DetailsJson` 或 `OutStockType != 2` 触发回调。
3. 确认页面提示“非法出库”。
4. 确认条码状态标记为异常。
5. 确认库位被释放。
6. 确认条码仍保留原库位，不被当作正常出库处理。

### 9.5 页面关闭流程

1. 入库页面已验证料架时关闭页面。
2. 确认页面触发空库位熄灯。
3. 出库页面处于待拣或拣货中时关闭页面。
4. 确认客户端调用取消出库，释放待拣标签。
5. 再次打开页面时不应残留旧状态和旧提示。

## 10. 数据验证点

### 10.1 入库成功后

- `WMS_BAR_DETAIL`
  - `BinNo` 更新为目标库位
  - `WarehouseNo` 仍为目标仓库
  - `IsRack='Y'`
  - `InstockDate` 已更新
- `WMS_ITEM_STOCK`
  - 原库位库存扣减
  - 目标库位库存新增或更新
- `WMS_SHELF_DETAIL`
  - 目标库位 `IsEnable='N'`

### 10.2 出库成功后

- `WMS_BAR_DETAIL`
  - `IsRack='N'`
  - `BinNo=NULL`
- `WMS_SHELF_DETAIL`
  - 原库位 `IsEnable='Y'`

### 10.3 非法出库后

- `WMS_SHELF_DETAIL`
  - 回调库位仍会释放为 `IsEnable='Y'`
- `WMS_BAR_DETAIL`
  - 条码不应被当作正常出库清空库位

## 11. 建议执行清单

### 11.1 第一轮冒烟

- `IE-API-01`
- `IE-API-06`
- `IE-CB-01`
- `IP-API-03`
- `IP-API-06`
- `IP-CB-01`
- `HUB-03`
- `HUB-04`

### 11.2 第二轮异常回归

- `IE-API-08`
- `IE-CB-02`
- `IE-CB-08`
- `IP-API-04`
- `IP-CB-02`
- `IP-CB-03`
- `HUB-05`

## 12. 测试记录模板

| 用例ID | 执行人 | 执行时间 | 实际结果 | 结论 | 备注 |
|---|---|---|---|---|---|
| IE-API-01 |  |  |  | 通过/失败 |  |

## 13. 快速接口测试命令

### 13.1 验证料架

```powershell
$body = @{
  shelfCode = "R616A"
  warehouseLocation = "616"
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://10.50.77.246:5067/api/induction/entry/validate-shelf" `
  -ContentType "application/json" `
  -Body $body
```

### 13.2 发起入库

```powershell
$body = @{
  barcode = "BAR-IND-001"
  shelfCode = "R616A"
  warehouseLocation = "616"
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://10.50.77.246:5067/api/induction/entry/deposit" `
  -ContentType "application/json" `
  -Body $body
```

### 13.3 入库回调

```powershell
$detailsJson = '{"warehouseLocation":"616","barGuid":"GUID-001","sourceBinNo":"TEMP-001","operationTime":"2026/05/13 09:30:00"}'
$body = @{
  labelId = "BAR-IND-001"
  location = "R616A0101"
  detailsJson = $detailsJson
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://10.50.77.246:5067/api/induction/entry/callback" `
  -ContentType "application/json" `
  -Body $body
```

### 13.4 查询出库条码

```powershell
$body = @{
  itemNo = "ITEM-001"
  requiredQty = 10
  warehouseLocation = "616"
  color = 6
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://10.50.77.246:5067/api/induction/pick/query" `
  -ContentType "application/json" `
  -Body $body
```

### 13.5 启动出库

```powershell
$body = @{
  labelIds = @("BAR-IND-002","BAR-IND-003")
  warehouseLocation = "616"
  color = 6
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://10.50.77.246:5067/api/induction/pick/start" `
  -ContentType "application/json" `
  -Body $body
```

### 13.6 出库回调

```powershell
$detailsJson = '{"warehouseLocation":"616","outStockType":2,"operationTime":"2026/05/13 09:35:00"}'
$body = @{
  labelId = "BAR-IND-002"
  location = "R616A0102"
  detailsJson = $detailsJson
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://10.50.77.246:5067/api/induction/pick/callback" `
  -ContentType "application/json" `
  -Body $body
```
