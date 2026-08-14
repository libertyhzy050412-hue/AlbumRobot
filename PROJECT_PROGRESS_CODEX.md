---
project: AlbumRobot
document_role: canonical-source-of-truth + codex-development-handoff
document_version: 5.0
updated_at: 2026-08-15
project_stage: implementation-ready
last_completed_grill_question: 219
next_step: phase_0_probe_then_vertical_slice
implementation_started: false
primary_target: V1
canonical_domain: album.rocknrollliberty.dpdns.org
---

# AlbumRobot — Codex 正式开发 Source of Truth

> 本文由完整产品 / 架构 Grill 讨论整理而来，用于从 ChatGPT 无损转移到 Codex 正式开发。
>
> **Q1～Q219 已完成。产品讨论收口。接下来默认直接实现。**

# 0. Codex 正式开发接管协议（最高优先级）

> **本文件是 AlbumRobot 当前唯一 source of truth。**
> Codex / 后续开发代理必须优先遵守本节、Canonical Constraints、Q1～Q219 的 LOCKED 决策和开发里程碑。

## 0.1 接管方式

1. 完整阅读本文；
2. 不重新讨论 Q1～Q219；
3. 默认直接做实现，不继续细粒度 Grill；
4. 实现级决策由 Codex 自行做出，并在代码 / ADR / README 中记录理由；
5. 只有以下情况必须向用户确认：
   - 改变产品边界；
   - 改变部署方式 / 成本约束；
   - 改变隐私边界；
   - 引入会产生费用的服务；
   - 不可逆地改变核心数据模型；
   - 真实技术探针证明某个 LOCKED 产品目标无法实现，需要产品层降级；
6. 外部平台事实不得凭记忆假设，必须先验证；
7. 不允许为了“未来扩展性”把 V1 做成复杂微服务 / 通用平台；
8. 优先交付真实可运行的纵向切片，再扩功能和视觉精修。

## 0.2 Canonical Constraints

### 产品
- V1 服务当前一个 QQ 群所有群友；
- 主产品是 **Mobile-first PWA**；
- V1 不发布 Android APK / 原生 App；
- 默认入口是专辑库；一级页面仅 **专辑 / 动态 / 统计**；
- 不做个人账号、收藏、待听、评分、评论、个人主页、歌单、音乐社区；
- V1 只识别网易云音乐**专辑分享卡片**；
- 不扫描普通聊天文本做 AI 推专识别；
- Album 是全局实体；Share / Member / Stats 按 Group 隔离。

### 数据采集
```text
Windows QQ
   ↓
QCE
   ↓
AlbumRobot Sync
   ↓
Cloudflare /api/sync/batch
   ↓
D1
   ↓
AlbumRobot PWA
```
- QQ Bot：**SUPERSEDED，不得实现为 V1 主链路**；
- QCE Direct API：主路径；QCE JSON：备用 / 故障降级；
- 完整 QQ 群聊仅本机处理；
- 普通聊天文本、无关图片、聊天上下文、失败 Raw Payload 不得上传 Cloudflare；
- Parse Error 原始结构只可本地短期保存；
- 自动识别和人工修复最终统一进入 Pending Queue，再走统一 Batch Sync。

### 云端
```text
Cloudflare Free
├─ Workers Static Assets
├─ Worker API
└─ D1
```
- Tencent CloudBase：**SUPERSEDED**；
- 不租 VPS；长期 0 元为硬约束；
- 不主动启用会产生按量账单的 Paid 服务；
- 免费额度不足时宁可限流 / 暂时失败；
- GitHub 只作为源码仓库 / CI 来源；
- PWA 与 API 尽量同源。

### 域名
当前正式候选入口：
```text
https://album.rocknrollliberty.dpdns.org
```
用户已将 `rocknrollliberty.dpdns.org` 连接至 Cloudflare。
大陆网络可达性仍属 **UNVERIFIED**：先按现方案部署，实测不稳定再迁移，不提前推翻 Cloudflare。

### 技术栈
```text
PWA  → React + Vite + TypeScript
Sync → C# / .NET Windows Desktop + SQLite
Cloud → Cloudflare Worker + D1 + Static Assets
```
Worker Router、React 状态管理、Motion library、WPF / WinUI 等属于实现级选择，由 Codex 自行决定并记录理由。

### 视觉与 Motion
- Apple Music 设计语言；
- Light / Dark，默认跟随系统；
- 不做品牌 Splash / Logo 演出；
- Motion 是一级质量要求；
- 移动端 Bottom Sheet + Shared Element 是核心验收项；
- Motion 必须自然、连续、跟手、可中断；
- Reduced Motion 必须合理降级；
- 桌面端保留 Navigation Rail + Floating Detail Sheet，但不提前过度打磨桌面细节。

### 权限
普通用户：群共享密码；Session 长期有效；修改群共享密码 → 所有普通 Session 失效；Session 失效 → 离线业务缓存同时清除。

Admin：独立管理员密码；Admin Authorization 与当前 Admin Mode 分离；设备授权长期有效直到主动注销；每次冷启动默认普通模式；修改管理员密码 → 所有 Admin Authorization 立即失效。

## 0.3 旧架构覆盖关系

| 旧方案 | 当前状态 | 正式替代 |
|---|---|---|
| QQ Bot 采集 | SUPERSEDED | AlbumRobot Sync + QCE |
| Tencent CloudBase | SUPERSEDED | Cloudflare Free |
| 一次性 Import CLI | SUPERSEDED | AlbumRobot Sync 首次全量 + 后续增量 |
| Web Import Job / Parse Error 页 | SUPERSEDED | Parse Error 只留 Sync 本地 |
| 品牌 Splash / Logo 启动演出 | SUPERSEDED | 直接进入 App Shell |
| 继续细粒度 Grill | SUPERSEDED | Q219 后默认直接开发 |

## 0.4 Grill 结束规则

Q219 后，常规 Grill 正式结束。Codex 默认：**自行决断实现细节 → 记录理由 → 继续开发。**

不要再为 batch size、SQLite migration、Worker Router、React 状态库、Query 缓存库、Motion 库、WPF / WinUI、日志文件大小、重叠扫描具体条数、Cache Storage、API DTO 命名、测试框架、CI 细节逐项询问用户，除非触发 0.1 中的重大边界变化。

# A. Grill Me 决策登记（Q1～Q180）

下面用于防止“只有总结、没有决策轨迹”造成的新对话漂移。**历史编号 Q7 未单独使用（Q6 与 Q8 之间直接记录了部署方向变更），不是缺失决策。**

## Q1 — 产品第一版面向谁 / 做多重
**状态：LOCKED**

用户明确要求：

> 第一版尽量轻量化，供所有群友使用，也可以不是 App 形式，怎么方便怎么来。

归一化决策：

- V1 是群共享工具；
- 优先低使用门槛；
- 原生 App 不是目标。

## Q2 — Bot 与产品主体如何分工
**状态：SUPERSEDED by Q164（历史决策保留）**

用户：

> 机器人负责收集信息类的功能，其他主要功能都交给网页。

决策：

- 旧方案：Bot = 采集层，Web = 浏览主体；
- 最新方案：Bot 取消，采集层改为本地 AlbumRobot Sync + QCE；Web 仍是群友浏览与主要交互主体。

## Q3 — 什么算 V1 的专辑推荐来源
**状态：LOCKED（由真实群聊使用方式直接限定）**

用户提供截图并说明：

> 群里推荐专辑都是网易云直接分享的卡片形式。

决策：

- V1 只识别网易云专辑分享卡片；
- 不做自然语言推荐识别；
- 不需要 LLM 扫描所有群聊。

## Q4 — 同一专辑被多次分享时如何建模
**用户选择：C**
**状态：LOCKED**

- 首页 / 动态保留每次 Share；
- 专辑库按 Album 去重；
- Share 与 Album 分离。

## Q5 — 网页访问认证
**用户选择：B，并进一步要求避免每次输入**
**状态：LOCKED**

- 一个群共享访问密码；
- 同一设备首次验证后长期保持会话；
- 不做个人注册。

**后续已解决：** Q206 锁定为长期有效，直到群共享密码变更、浏览器数据清除或服务端撤销。

## Q6 — 成员展示复杂度
**用户选择：A**
**状态：LOCKED**

- 只显示分享者昵称；
- V1 不做成员主页。

## 部署方向变更 — 不想租服务器
**状态：SUPERSEDED by Q163（“不租 VPS / 零成本”意图仍 LOCKED）**

用户明确表示：

> 懒得租服务器。

随后在候选中选择：

> 1（Tencent CloudBase）

因此当时采用 CloudBase；**最新 Q163 已改为 Cloudflare Free 全栈**。持续有效的是“不租 VPS、零成本优先”。

## Q8 — 首页 / 专辑与动态关系
**用户选择：D**
**状态：LOCKED**

- 默认页 = 专辑库 / 专辑墙；
- Share Feed 单独放“动态”页。

## 成员稳定身份补充
**状态：LOCKED（用户意图） + UNVERIFIED（具体 QQ 字段）**

用户原始要求：

> 后端存群成员信息时应该以 QQ 号为准，防止成员改名。

真正必须保留的需求语义是：

> **成员身份必须依赖稳定 ID，不能依赖昵称。**

旧 Bot 阶段曾把实现基线写为 `member_openid`；当前已改用 QCE，具体稳定成员字段必须以 QCE 真实样本验证。

**注意：**
- 不要在后续对话中把“真实 QQ 号一定可取”当事实；
- 实施前必须用 QCE Direct API / JSON 真实样本验证稳定成员字段及其作用域。

## Q9 — 改名后历史记录显示哪个昵称
**用户选择：A**
**状态：LOCKED**

- 所有页面展示当前昵称；
- 不保留“分享当时昵称”快照。

## Q10 — 点击专辑后的详情
**用户选择：C**
**状态：LOCKED**

- 不做独立详情页；
- 使用轻量 Bottom Sheet；
- 不堆详细音乐元数据。

## Q11 — Album Grid 的文字密度
**用户选择：C**
**状态：LOCKED**

- 封面是绝对视觉主体；
- 专辑名 + 艺术家弱化显示。

## Q12 — 专辑库排序
**用户选择：C**
**状态：LOCKED**

支持：

- 最近分享（默认）；
- 首次收录；
- 分享最多。

## Q13 — 搜索范围
**用户选择：C**
**状态：LOCKED**

匹配：

- 专辑名；
- 艺术家；
- 分享者昵称。

最终结果仍然展示专辑。

**未锁定：**
- 拼音；
- typo tolerance；
- 高级模糊搜索；
- 专门搜索引擎。

## Q14 — 管理权限
**用户选择：C**
**状态：LOCKED**

- 普通群密码 = 浏览；
- 独立管理员密码 = 修改 / 删除 / 合并 / 导入等管理操作。

## Q15 — 是否导入历史 QQ 记录
**用户选择：B**
**状态：LOCKED**

- V1 上线就考虑历史回填；
- 不能只记录 Bot 加入后的未来数据。

## Q16 — 历史导入工具形态
**用户选择：B**
**状态：SUPERSEDED by Q164～Q180（历史决策保留）**

- 旧方案使用本地 CLI；
- 最新方案由 AlbumRobot Sync 统一承担首次历史全量与后续增量；
- “完整 QQ 历史不在网页 / 云端解析”这一隐私边界继续有效。

## Q17 — CLI 如何写入 CloudBase
**用户选择：C**
**状态：SUPERSEDED by Q163 / Q171（历史决策保留）**

- 旧方案：CLI 调用管理员 Import API；
- 最新方案：AlbumRobot Sync 调用 Cloudflare `/api/sync/batch`；客户端仍不直接连接数据库。

## Q18 — 同专辑不同版本
**用户选择：C**
**状态：LOCKED**

- 默认按网易云 album ID 分开；
- 管理员可以人工合并。

## Q19 — “分享最多”如何统计
**用户选择：B**
**状态：LOCKED**

- 按不同分享者人数；
- 不是总 Share 次数。

即概念上：

`COUNT(DISTINCT member_id)`

## Q20 — 同一个人反复分享同一专辑时动态怎么显示
**用户选择：D**
**状态：LOCKED**

- Share 全保留；
- 非第一次时显示“再次分享了”。

## Q21 — 专辑页时间筛选
**用户选择：B**
**状态：LOCKED**

- 全部；
- 本月；
- 今年。

不做完整自定义日期筛选。

## Q22 — 多 QQ 群
**用户选择：B**
**状态：LOCKED**

- V1 产品只服务当前一个群；
- 数据结构从第一天保留 group ID；
- 不在 UI 中做多群选择。

**后续已解决：** Q219 锁定 Album 为全局实体，Member / Share / Stats 按 Group 隔离。

## Q23 — 是否补 MusicBrainz / 年份 / 流派等元数据
**用户选择：D**
**状态：LOCKED**

- V1 不补全；
- 只使用网易云能稳定获得的数据；
- 架构允许未来扩展 metadata。

## Q24 — 收藏 / 待听
**用户选择：A**
**状态：LOCKED**

- V1 完全不做。

## Q25 — 统计页内容
**用户选择：B，但明确覆盖原方案**
**状态：LOCKED**

用户要求：

> 统计页只做用户统计，统计成员推专数量排行，按周 / 月 / 年度区分。

并随后补充：

> 同时显示本周 / 本月 / 本年群里共计分享了多少张专辑。

因此不要恢复“热门专辑榜”“艺术家榜”“复杂图表”。

## Q26 — 成员排行里重复专辑怎么算
**用户选择：B**
**状态：LOCKED**

同一成员、同一统计周期内，同一 album 多次 Share 只算一张不同专辑。

概念上：

`GROUP BY member_id + COUNT(DISTINCT album_id)`

## Q27 — 群内总分享专辑数怎么算
**用户选择：A**
**状态：LOCKED**

按该周期内不同 Album 数：

`COUNT(DISTINCT album_id)`

不是把所有成员排行数字相加。

## Q28 — 周 / 月 / 年的周期定义
**用户选择：A**
**状态：LOCKED**

使用自然周期：

- 本周：自然周；
- 本月：自然月；
- 本年：自然年。

**当前基线：** UTC+8。  
**说明：** UTC+8 是当时设计基线，不是用户单独选择的一个选项；实施时仍应配置成明确业务时区而非依赖服务器系统时区。

## Q29 — QQ 撤回
**用户选择：B**
**状态：LOCKED**

- 网站继续保留；
- AlbumRobot 是档案馆，不是 QQ 消息镜像。

## Q30 — 历史成员无法与当前稳定 ID 匹配
**用户回答：按建议**
**采用建议：B + 管理员合并**
**状态：LOCKED**

- 无法匹配时建立 Legacy Member；
- 不阻塞历史导入；
- 管理员后续可合并成员。

## Q31 — 历史 Share 的时间精度
**用户选择：B**
**状态：LOCKED**

- 历史导入只要求日期；
- 不要求具体时分；
- 实时数据仍可保留完整时间。

## Q32 — 动态页组织方式
**用户选择：B**
**状态：LOCKED**

- 按日期分组；
- 历史 date-only Share 不伪造时分。

## Q33 — 一级导航
**用户选择：B**
**状态：LOCKED**

三个一级 Tab：

- 专辑；
- 动态；
- 统计。

搜索放在专辑页顶部。

## Q34 — 视觉方向
**用户没有选择预设 A/B/C/D，而是明确覆盖为 Apple Music**
**状态：LOCKED**

> 整体做成 Apple Music 风格。

因此后续不要再转回杂志、RYM、复古数据库、实体唱片柜等视觉方向。

## Q35 — 浅 / 深色
**用户选择：C**
**状态：LOCKED**

- 浅色 + 深色；
- 默认跟随系统。

## 动效一级约束
**状态：LOCKED**

用户明确追加：

> 不止视觉效果，动效也要按照苹果的设计风格来，做到一样的丝滑动效。

这不是“动画多一点”，而是产品质量标准：

- 连续；
- 跟手；
- 自然；
- 有空间关系；
- 可中断；
- 性能稳定；
- 不能有普通网页 Modal / 生硬刷新感。

## Q36 — Large Title
**用户选择：A**
**状态：LOCKED**

三个一级页都使用：

`Large Title → Compact Title`

并随滚动自然过渡。

## Bot 与 Web 的通信方式
**状态：SUPERSEDED by Q164**

旧方案已作废。当前为：AlbumRobot Sync → Cloudflare Worker / D1；PWA → 同源 Cloudflare API / D1 读取。

## 产品传播方式
**状态：BASELINE（已进入总体架构）**

当前载体：

> Mobile-first PWA Web App

传播：

- QQ 群公告链接；
- Bot 可提供 `/专辑库` 之类入口；
- 高频用户可以添加到主屏幕。

V1 不做 APK / App Store。

## 实时推送
**状态：BASELINE，不是单独 Grill-confirmed**

当前 V1 基线是不做 WebSocket/SSE 实时推送到已打开页面。

刷新时机可以是：

- 初次打开；
- 回到前台；
- 下拉刷新；
- 必要的 Tab 回访刷新。

如果未来用户明确要求“分享后网页即时出现”，再改。

## Q37 — 启动体验（本轮修订）
**原选择：C；后续被用户“只是自用工具，不需要品牌”明确覆盖**
**状态：LOCKED / SUPERSEDED**

最新决策：

- 不做独立 Splash；
- 不做 Logo / 品牌符号的启动表演；
- 冷启动直接呈现 App Shell，Large Title / Tab Bar / 内容以连续、无硬切的方式就位；
- V1 不投入专门品牌建设，重点放在视觉、Motion 与工具体验完成度。

> 本条以最新决策为准，旧的“品牌元素 → 首页”方案作废。

## Q38 — Bottom Sheet 的动效等级
**用户选择：D**
**状态：LOCKED**

采用：

- 完整手势 Bottom Sheet；
- Shared Element Transition；
- 封面从 Grid 连续变化为 Sheet 大封面；
- 关闭时尽量回到原元素；
- 支持拖拽、阈值关闭、未达阈值回弹。

这是 V1 的重点验收项。

## Q39 — Album Grid 加载
**用户选择：C**
**状态：LOCKED**

- 无限滚动；
- 提前预取；
- 图片懒加载；
- 页面状态与滚动位置保持；
- 切 Tab / 开关 Sheet 后不丢位置。

## Q40 — 搜索交互
**用户选择：B**
**状态：LOCKED**

Apple 式 Search Mode：

- 搜索框扩展；
- Large Title 收缩；
- 取消按钮进入；
- 筛选排序让位；
- 退出后恢复原页面状态。

## Q41 — Bottom Tab Bar
**用户选择：D**
**状态：LOCKED**

- Apple 式半透明 Material；
- 根据滚动轻微响应；
- 不完全隐藏；
- Safe Area 正确；
- 深浅模式适配。

## Q42 — 一级 Tab 之间的页面转场
**用户选择：D**
**状态：LOCKED**

- 轻微横向空间转场；
- Tab Bar 稳定；
- 每个 Tab 独立保存自己的滚动、筛选和状态。

---


## Q43 — 动态页单条 Share 的核心布局
**用户选择：B**
**状态：LOCKED**

采用 Apple Music 式紧凑媒体行：

- 左侧约 56～64px 方形专辑封面；
- 右侧展示专辑信息；
- 不使用厚重卡片背景；
- 整行可点击并打开统一专辑 Bottom Sheet。

## Q44 — 动态页日期分组标题
**用户选择：B**
**状态：LOCKED**

- 日期标题使用 Sticky Header；
- 当前日期轻量吸附在 Compact Title 下方；
- 进入下一日期分组后由新日期自然顶替旧日期；
- 避免传统网页式硬吸顶条。

## Q45 — 动态行的信息层级
**用户选择：C**
**状态：LOCKED**

专辑优先，分享者弱化到次级信息层：

```text
[封面]  Loveless
        My Bloody Valentine
        A 分享了 · 14:32
```

重复分享：

```text
A 再次分享了 · 14:32
```

动态页仍回答“谁分享了什么”，但不做成社交 Feed。

## Q46 — 动态页筛选
**用户选择：A**
**状态：LOCKED**

- 动态页不提供常驻内容筛选；
- 动态页保持为纯粹的群内专辑时间线；
- 专辑 / 艺术家 / 分享者查询继续交给专辑页搜索。

## Q47 — 动态历史加载
**用户选择：A，并追加时间跳转能力**
**状态：LOCKED**

默认：

- 无限滚动；
- Cursor Pagination；
- 自动提前预取更早 Share；
- 图片懒加载；
- 切 Tab 后保持已加载数据与当前位置。

同时必须提供“按时间快速定位”的能力。

**性能 BASELINE：**
- 禁止 DOM 随历史 Share 数量无限增长；
- 数据规模上升后使用 Windowing / Virtualization；
- 实际虚拟化库与阈值实施时再定。

## Q48 — 历史时间选择器
**用户选择：C**
**状态：LOCKED**

采用 Apple 式分级日期选择：

```text
年份
2026 >
2025
2024
```

进入年份后选择月份。

使用轻量 Bottom Sheet，不做传统日期范围表单。

## Q49 — 选择月份后的语义
**用户选择：A**
**状态：LOCKED**

- 月份选择只负责快速跳转定位；
- 不进入“筛选模式”；
- 定位到目标月份后，时间线仍然连续；
- 可以继续向上 / 向下浏览相邻月份。

## Q50 — 刷新后发现新 Share
**用户选择：B**
**状态：LOCKED**

V1 不做 WebSocket / SSE 实时推送。

只有在已有刷新触发发生后（重新打开、回前台、Tab 回访轻量刷新、下拉刷新等）才检测新 Share。

如果用户当前不在顶部：

- 不改变当前阅读位置；
- 显示轻量“↑ X 条新动态”提示；
- 用户点击后才平滑回到最新位置。

## Q51 — 动态行分隔
**用户选择：B**
**状态：LOCKED**

- 使用低对比度、缩进式细分隔线；
- 分隔线从文字区域开始，不穿过左侧封面；
- 不使用独立卡片。

## Q52 — 历史时间入口
**用户选择：B**
**状态：LOCKED**

- 动态页右上角使用轻量 Calendar / History 图标；
- Large Title 与 Compact Title 状态下都保持在导航右侧；
- 点击后打开 Q48 的年份 → 月份选择 Sheet；
- 因为只是导航，不显示“当前筛选月份”。

## Q53 — 从动态页打开专辑 Sheet
**用户选择：B**
**状态：LOCKED**

复用与专辑页同一个 Bottom Sheet，但保留当前 Share 上下文：

- 从专辑页进入：显示最近分享者 / 不同分享者人数；
- 从动态页进入：优先显示当前这次 Share 的分享者与时间；
- date-only 历史数据只显示日期，不伪造时分；
- 其他内容和 Shared Element 逻辑共用。

## Q54 — 动态页下拉刷新
**用户选择：B**
**状态：LOCKED**

采用 Apple 式跟手下拉刷新：

- 内容随手势位移；
- 越往下阻尼越明显；
- 达阈值时给轻量反馈；
- 松手后自然 Spring 回弹；
- 刷新后遵循 Q50 的“新动态提示”规则；
- 不加入品牌 Logo / 品牌 Refresh 动画。

## Q55 — 动态页加载更早记录的 Loading
**用户选择：D**
**状态：LOCKED**

- 目标是依靠提前预取做到“无感加载”；
- 正常情况下不展示 Skeleton / 明显 Spinner；
- 网络较慢、预取未赶上时，才退化显示极轻量 Loading Row。

## Q56 — 回到最新
**用户选择：A**
**状态：LOCKED**

- 当用户距离顶部足够远时，显示临时“↑ 回到最新”按钮；
- 尤其适用于从日历跳转到较旧月份后；
- 按钮位于 Bottom Tab Bar 上方，不遮挡内容；
- 回到顶部后自动消失。

## Q57 — date-only 历史 Share 的同日顺序
**用户选择：A**
**状态：LOCKED**

- 尽可能保留 QQ 导出文件中的原始出现顺序；
- 建议历史 Share 增加 `source_order`；
- 动态页同一天内按该稳定顺序展示；
- 如果导出源连原始顺序都无法可靠提供，则使用稳定导入顺序；
- 不伪造具体时间。

---

## Q58 — 统计页总体视觉结构
**用户选择：B**
**状态：LOCKED**

采用“大数字总结 + 极简成员排行”：

```text
统计

[ 本周 | 本月 | 本年 ]

        42
    张不同专辑
   本周群内分享

成员排行
1  A  17
2  B  14
...
```

- 不做 Dashboard 卡片；
- 数字、文字、留白形成主要层级；
- 与专辑页（封面）和动态页（时间线）形成不同视觉重点。

## Q59 — 周 / 月 / 年切换控件
**用户选择：A**
**状态：LOCKED**

- 顶部使用 iOS 风格 Segmented Control；
- 选中背景平滑移动；
- 不整页刷新；
- 大数字与排行榜做连续状态过渡。

## Q60 — 成员排行榜视觉
**用户选择：C**
**状态：LOCKED**

- Top 3 轻度强调；
- 通过字号、字重、间距或数字层级实现；
- 不使用奖牌、皇冠、Emoji、领奖台、头像框等游戏化元素；
- 其余成员使用普通极简列表。

## Q61 — 成员头像
**用户选择：A**
**状态：LOCKED**

- 排行榜完全不显示成员头像；
- 不新增 QQ 头像获取、缓存、失效等依赖。

## Q62 — 点击排行榜成员
**用户选择：B**
**状态：LOCKED**

点击成员：

```text
统计
↓
专辑 Tab
↓
进入 Search Mode
↓
展示该成员分享过的专辑
```

不新增成员详情页或成员 Bottom Sheet。

## Q63 — 成员跳转的统计周期
**用户选择：A**
**状态：LOCKED**

- 从统计页点击成员时继承当前周期；
- 例如“本月 A 17 张” → 专辑页展示 A 本月对应的专辑集合；
- 本周 / 本月 / 本年都遵循相同语义。

## Q64 — 成员跳转后的专辑页状态
**用户选择：A**
**状态：LOCKED**

- 直接进入专辑页现有 Search Mode；
- 搜索框自动填入成员昵称；
- 同时携带 Q63 的当前统计周期约束；
- 不新增特殊“成员结果页”。

> 普通专辑页的常驻时间筛选仍维持“全部 / 本月 / 今年”；统计页跳转携带的“本周”等周期属于跳转上下文，不因此扩大全局筛选项。

## Q65 — 统计大数字是否可点击
**用户选择：A**
**状态：LOCKED**

- 顶部“X 张不同专辑”只用于展示；
- 不承担导航；
- 不因为统计页大数字而给普通专辑页新增“本周”常驻筛选。

## Q66 — 排行榜展示人数
**用户选择：A**
**状态：LOCKED**

- 当前周期内所有有分享行为的成员全部展示；
- 不做 Top 10 折叠；
- 不增加“查看全部”。

## Q67 — 切换统计周期的排行过渡
**用户选择：C**
**状态：LOCKED**

采用连续 Reorder Motion：

- 同一成员保留同一行并移动到新排名；
- 数字同步更新；
- 新进入成员淡入；
- 离开成员淡出；
- Top 3 强调跟随成员移动；
- 动画可中断，快速连续切换时追随最新状态；
- 不排队播放。

## Q68 — 排名涨跌
**用户选择：A**
**状态：LOCKED**

- 不显示 ↑ / ↓ 排名变化；
- 不显示相对上周 / 上月 / 去年的数量变化；
- 统计页只表达当前周期绝对数据。

## Q69 — 统计页空状态
**用户选择：A**
**状态：LOCKED**

即使当前周期为 0，也保持原结构：

```text
0
张不同专辑

成员排行
本周还没有专辑分享
```

- 不加插图；
- 不加“去分享”按钮；
- 不自动跳其他周期。

---

## Q70 — Motion Token 层级
**用户选择：B**
**状态：LOCKED**

采用三层：

- `Micro`：按钮、图标、Segment 等小反馈；
- `UI`：搜索、Sticky Header、列表状态变化；
- `Spatial`：Bottom Sheet、Shared Element、Tab 空间转场。

Spatial 优先使用 Spring / 手势物理，不简单写死 duration。

## Q71 — Spring 分档
**用户选择：B**
**状态：LOCKED**

两档：

- `Responsive Spring`：小控件、Segment、轻量回弹，快、紧、直接；
- `Soft Spring`：Bottom Sheet、Shared Element、大范围空间移动，更柔和、有重量感。

## Q72 — 非 Spring easing
**用户选择：B**
**状态：LOCKED**

统一两套：

- `Enter`：进入 / 展开 / 出现，快速响应、末端柔和；
- `Exit`：退出 / 收起 / 消失，更短、更利落。

有明确空间关系的位置变化优先使用 Spring。

## Q73 — prefers-reduced-motion
**用户选择：B**
**状态：LOCKED**

Reduced Motion 下：

保留：
- 按钮按压；
- Segment 状态变化；
- 极轻 opacity；
- 基本下拉刷新跟手反馈。

弱化 / 移除：
- Shared Element 大范围位移；
- Tab 横向空间转场；
- 明显列表飞移；
- Bottom Sheet 强弹性与大位移。

## Q74 — 整体动画节奏
**用户选择：B**
**状态：LOCKED**

基调：

- 偏快；
- 用户操作发生后立即响应；
- 末端留自然收稳余韵；
- `Micro` 约 100～160ms；
- `UI` 约 180～260ms；
- `Spatial` 主要由 Spring 和距离 / 速度决定。

这些是设计区间，不是最终实现参数。

## Q75 — Spring Overshoot
**用户选择：B**
**状态：LOCKED**

- 允许非常轻微的 Overshoot；
- 必须低存在感；
- 目标是“停得自然”，而不是让用户明显看到“弹簧动画”；
- 禁止夸张 Q 弹。

## Q76 — 手势结束状态判定
**用户选择：B**
**状态：LOCKED**

使用：

`position + velocity + direction`

共同判断继续还是取消。

原则：

> 用户意图优先于单一绝对距离阈值。

不同组件具体阈值可不同。

## Q77 — Spatial Motion 可中断
**用户选择：B**
**状态：LOCKED**

- 主要 Spatial Motion 必须可中断、可重新接管；
- Sheet 回弹途中可重新按住；
- 从当前视觉状态继续，而不是等待旧动画结束；
- 快速连续操作始终追随最新意图；
- 不为了播完动画锁 UI。

## Q78 — Bottom Sheet 内部滚动与拖动关闭冲突
**用户选择：B，并明确修订**
**状态：LOCKED**

采用 **Gesture Ownership Lock**：

- 一次 `pointer down → pointer up` 内，一旦判定为 Internal Scroll 或 Sheet Drag，所有权保持不变；
- 如果本轮被判定为内部滚动，即使滚到顶部 / 底部，也不在同一次按下中把手势转交给 Sheet；
- 到边界后继续拖时使用阻尼 / Rubber-band；
- 松手后回弹；
- 下一次新手势再重新判定所有权。

该规则优先于常见的“滚到顶部后同一手势无缝转交父 Sheet”。

## Q79 — Gesture Owner 初始判定
**用户选择：B**
**状态：LOCKED**

- `pointer down` 后先观察最初很短一段移动；
- 根据当前滚动位置 + 初始主要方向确定 Gesture Owner；
- 内部在顶部且明显向下 → 可判定 Sheet Drag；
- 内部可滚且纵向滚动 → Internal Scroll；
- 明显横向移动不触发 Sheet Drag；
- 轻点保持为 Tap；
- 一旦确定，按 Q78 锁定到 `pointer up`。

## Q80 — Bottom Sheet 停靠档位
**用户选择：A**
**状态：LOCKED**

只有：

```text
Closed ↔ Expanded
```

- 不做 Medium / Large 多档停靠；
- 展开高度可以响应设备尺寸；
- 交互状态只有一个展开态。

## Q81 — Bottom Sheet 展开高度
**用户选择：B**
**状态：LOCKED**

- Expanded 约占屏幕 70%～80%；
- 根据设备尺寸与 Safe Area 小幅自适应；
- 背景页面仍应可见；
- 不做接近全屏的独立详情页感。

## Q82 — Sheet 打开后的背景
**用户选择：B**
**状态：LOCKED**

- 背景轻微变暗；
- 加极弱 Blur 建立层级；
- 不缩放背景页面；
- Blur 必须可性能降级；
- 低性能设备 / 不支持时退化为半透明遮罩；
- 拖动 Sheet 时遮罩与 Blur 跟随进度连续变化。

## Q83 — Shared Element 的对象
**用户选择：A**
**状态：LOCKED**

- 只有专辑封面参与 Shared Element；
- 专辑名、艺术家、分享信息使用轻量 Enter；
- 不做文字跨字号 / 跨布局几何变形。

原则：

> 封面承担空间连续性，文字承担信息连续性。

## Q84 — Shared Element 源位置
**用户选择：A**
**状态：LOCKED**

- 封面从 Grid / 动态行被“拿起”后，源位置保留空白占位；
- 原列表不重排；
- 关闭时封面可准确回到该位置。

## Q85 — 关闭时源元素不可用
**用户选择：B**
**状态：LOCKED**

- 如果原始封面仍可见且位置可靠 → Shared Element Return；
- 如果不可见、被虚拟化卸载或定位失效 → 自动降级为普通 Sheet Exit；
- 绝不为了完成动画强行滚回源位置或改变用户当前页面状态。

## Q86 — Bottom Sheet 关闭方式
**用户选择：B**
**状态：LOCKED**

支持：

1. 向下拖 Sheet；
2. 点击 Sheet 外背景遮罩；
3. Android 返回键 / 浏览器系统返回。

三种方式统一进入同一关闭状态机：

```text
请求关闭
↓
源元素可用？
├─ 是 → Shared Element Return
└─ 否 → 普通 Exit
```

打开 Sheet 后按一次系统返回必须优先关闭 Sheet，而不是直接离开 AlbumRobot。



## Q87 — Admin Mode 入口
**用户选择：B**
**状态：LOCKED**

- 普通界面通过全局 `···` 菜单进入“管理员模式”；
- 不在主界面长期放醒目的管理按钮；
- 第一次进入需要管理员认证，之后按 Q103 / Q111 处理设备授权。

## Q88 — Admin Mode 总体形态
**用户选择：D**
**状态：LOCKED**

采用混合模式：

- 原有“专辑 / 动态 / 统计”三页结构不变；
- 内容相关管理在原上下文就地完成；
- 全局任务从 `··· → 管理工具` 进入；
- 不做传统 Admin Dashboard。

## Q89 — 专辑局部管理入口
**用户选择：C**
**状态：LOCKED**

- Album Grid 本身不出现管理按钮；
- 点击专辑仍正常打开 Album Detail Sheet；
- Admin Mode 下只在 Sheet 的 `···` 菜单显示编辑 / 合并 / 删除；
- V1 不做长按快捷管理菜单。

## Q90 — 删除整张 Album 的语义
**用户选择：C**
**状态：LOCKED**

- 删除 Album = 删除 Album 实体 + 该 Album 的全部关联 Share；
- 必须明确二次确认，并显示会删除多少条历史 Share；
- 删除单条 Share 是独立操作；
- V1 不引入回收站 / 软删除体系。

## Q91 — 删除单条 Share 的入口
**用户选择：B**
**状态：LOCKED**

- 只有从动态页某条 Share 打开的 Album Sheet 才有“删除这次分享”；
- 从 Album Grid 打开的 Sheet 因没有特定 Share Context，不显示此操作；
- 动态列表不增加 `···` / 左滑删除。

## Q92 — 删除 Share 的确认
**用户选择：D**
**状态：LOCKED**

- 使用轻量 Action Sheet 二次确认；
- 文案明确“只删除这一次分享，不影响专辑及其他分享”。

## Q93 — 可编辑 Album 字段
**用户选择：B**
**状态：LOCKED**

允许编辑：

- `title`；
- `artist`；
- `cover_url`；
- `netease_url`。

`netease_album_id` 为只读身份字段；身份关系问题走 Merge，不直接编辑 ID。

## Q94 — Album 编辑形态
**用户选择：A**
**状态：LOCKED**

- 当前 Album Detail Sheet 原位从查看态连续切换到编辑态；
- 不叠第二层 Sheet，不跳独立编辑页；
- 保存 / 取消后回到同一 Album 的查看态；
- 编辑态按系统返回优先退出编辑态，而不是直接关闭整个 Sheet。

## Q95 — Album Merge 目标选择
**用户选择：B**
**状态：LOCKED**

- 当前 Album 作为来源 A；
- 进入搜索式目标选择器，按专辑名 / 艺术家搜索现有 Album B；
- A 的 Share 全部迁移到 B；
- A 删除，B 保留；
- 系统不自动猜“哪些版本应该合并”。

## Q96 — Album Merge 最终确认
**用户选择：B**
**状态：LOCKED**

- 同时展示来源 / 目标封面、名称与各自 Share 数量；
- 明确说明来源 Share 会迁入目标；
- 目标 Album 的 title / artist / cover / URL 保留；
- 不做字段级自动融合。

## Q97 — Member Merge 流程
**用户选择：B**
**状态：LOCKED**

统一为：来源 Member → 搜索目标 Member → 查看影响范围 → 确认合并。

## Q98 — Member Merge 字段保留规则
**用户选择：B**
**状态：LOCKED**

- 展示信息以目标 Member 为主；
- 来源 Share 全部迁移到目标；
- 若只有一边存在经过验证的稳定 QQ 身份字段，该稳定身份必须保留；
- 若两边都存在不同稳定身份，禁止自动覆盖并阻止合并，要求人工处理。

## Q99 — 成员管理列表组织
**用户选择：B**
**状态：LOCKED**

- 顶部优先展示 Legacy Member / 待处理历史成员；
- 下方展示全部成员；
- 搜索覆盖全部 Member。

## Q100 — 历史导入在网页中的职责
**用户选择：D（历史决策）**
**状态：SUPERSEDED by Q163～Q180**

当时锁定：网页只查看历史导入任务 / 结果，真正解析与导入由本地工具发起，不在网页上传完整聊天记录。

最新架构中，本地工具已统一为 AlbumRobot Sync；“完整聊天不上传网页 / 云端”这一边界继续有效，但旧 Import Job 页面不再自动视为 V1 必做。

## Q101 — 导入失败 / 跳过记录的网页信息密度
**用户选择：B（历史决策）**
**状态：SUPERSEDED by Q177**

当时锁定：网页可保留结构化错误摘要，但不保存完整 QQ 原始消息正文。

最新 Q177 进一步收紧：解析失败项只保留在 AlbumRobot Sync 本地，不上传 Cloudflare，因此旧网页错误清单方案作废。

## Q102 — 历史导入记录保留策略
**用户选择：B（历史决策）**
**状态：SUPERSEDED by Q163～Q180**

当时锁定：Import Job 默认长期保留，管理员可删除记录且不影响已入库 Album / Share / Member。

由于当前不再把旧 Import Job UI 作为 V1 主线，该保留策略不再是当前必做需求；若未来引入云端 Batch Audit，应重新 Grill。

## Q103 — 管理员授权与 Admin Mode 分离
**用户选择：D**
**状态：LOCKED**

- 管理员认证授权与“当前是否处于 Admin Mode”分离；
- 设备已有有效管理员授权时，进入 Admin Mode 无需再次输密码；
- 重新打开 AlbumRobot 时仍默认普通浏览；
- 提供“退出管理员模式”和“注销管理员授权”两个不同动作。

## Q104 — 未保存编辑保护
**用户选择：C**
**状态：LOCKED**

- 仅当表单实际 dirty 时才拦截离开；
- 未修改则直接退出；
- dirty 时提供“继续编辑 / 放弃更改”；
- 关闭 Sheet、系统返回、切 Tab、退出 Admin Mode 等若会销毁 dirty edit，统一先走该检查。

## Q105 — 批量管理
**用户选择：A**
**状态：LOCKED**

V1 不做 Album / Share / Member 多选或批量管理；保持单对象维护模型。

## Q106 — Admin Mode 状态标识
**用户选择：B**
**状态：LOCKED**

- 导航右侧持续显示轻量“管理中”；
- 不使用警告横幅，不改变整个页面配色；
- 点击“管理中”可快速退出 Admin Mode。

## Q107 — 管理成功反馈
**用户选择：B**
**状态：LOCKED**

- 普通字段编辑：依靠界面状态变化反馈，不额外 Toast；
- 删除 / 合并等影响不易直接感知的操作：显示短暂轻量 Toast。

## Q108 — 管理失败反馈
**用户选择：B**
**状态：LOCKED**

- 临时可重试失败（网络 / 超时）：Toast / 行内提示，保留当前状态并允许重试；
- 业务冲突（身份冲突、目标不存在、约束冲突）：明确 Dialog / Sheet 解释原因，系统不自动猜、不自动修。

## Q109 — Admin 审计日志
**用户选择：B**
**状态：LOCKED**

- 后端从 V1 起保留轻量结构化 `AdminAction`；
- 记录 action_type / target_type / target_id / summary / 必要 metadata / created_at；
- V1 不专门做“操作历史”页面；
- 不保存密码、完整请求 Payload、完整 QQ 聊天内容。

## Q110 — 管理操作 Undo
**用户选择：A**
**状态：LOCKED**

V1 不支持 Undo / Event Sourcing / 恢复快照；通过明确确认与审计降低误操作风险。

## Q111 — 管理员授权有效期
**用户选择：A**
**状态：LOCKED**

- 设备管理员授权永久有效，直到主动“注销管理员授权”；
- 但每次冷启动仍默认普通浏览模式。

## Q112 — 管理工具首页
**用户选择：A**
**状态：LOCKED**

`管理工具` 是极简导航 Sheet，不做 Dashboard。当前稳定入口至少包含成员管理；旧“历史导入”入口已被 Sync 架构覆盖；Q211 已锁定管理工具提供轻量只读“同步状态”入口。

## Q113 — 管理二级页形态
**用户选择：B**
**状态：PARTIALLY LOCKED / UPDATED**

- `管理工具` 本身保持轻量 Sheet；
- 真正需要浏览较多内容的管理任务使用 App 内 Push 二级页面；
- 成员管理明确适用；
- 旧“历史导入页面”部分已被 Q163～Q180 覆盖，不再自动视为当前必做页面。

## Q114 — 在管理二级页退出 Admin Mode
**用户选择：B**
**状态：LOCKED**

- 退出 Admin Mode 时关闭整个管理层级；
- 恢复到进入管理工具之前的普通页面及 Tab / 滚动 / 搜索 / 筛选 / 排序状态；
- dirty edit 仍先执行 Q104。

## Q115 — Album 管理菜单风险分组
**用户选择：B**
**状态：LOCKED**

菜单：

- 编辑专辑；
- 合并专辑；
- 分隔线；
- 删除整张专辑（destructive）。

## Q116 — Member Merge 来源删除提示
**用户选择：B**
**状态：LOCKED**

最终确认必须显式说明：来源 Member 的 Share 将迁移、来源成员记录会被移除、目标展示信息保留、稳定身份不会因合并丢失。

## Q117 — 封面编辑
**用户选择：B**
**状态：LOCKED**

- `cover_url` 输入 + 实时封面预览；
- 加载失败显示错误但保留输入；
- V1 不做本地图片上传 / Cloudflare 文件管理；
- 不强制封面来自网易云，但保存前至少验证 URL 格式与实际可加载。

## Q118 — `netease_url` 与 Album 身份一致性
**用户选择：B**
**状态：LOCKED**

- 能可靠从链接解析 album ID 时，必须与当前 `netease_album_id` 一致；
- 不一致则禁止保存并提示走 Merge；
- 无法可靠解析时不猜身份，只做 URL 校验。

## Q119 — Album 字段必填
**用户选择：C**
**状态：LOCKED**

- `title` 必填；
- `artist` 必填；
- `cover_url` 可空；
- `netease_url` 可空；
- 缺封面使用克制占位；缺网易云链接则隐藏“在网易云打开”。

## Q120 — 保存后已挂载 Album 视图同步
**用户选择：A**
**状态：LOCKED**

- Album 编辑保存成功后，当前已挂载的 Grid / 动态 / Sheet 同一 Album 立即同步 title / artist / cover / URL；
- 不整页刷新，不改滚动；
- 修改封面后，关闭 Sheet 时可用新封面 Shared Element Return 到已同步的新封面源位置。

## Q121 — 封面验证中点击保存
**用户选择：C**
**状态：LOCKED**

- 用户可随时点保存；
- 若封面仍在验证，保存事务等待验证结果；
- 验证成功才整体提交；失败则不提交并保留编辑态全部输入。

## Q122 — 恢复原始值功能
**用户选择：A**
**状态：LOCKED**

V1 不额外提供逐字段 / 全局“还原”按钮；使用保存、取消、dirty 检测、放弃更改确认即可。

## Q123 — title / artist 空白规范
**用户选择：B**
**状态：LOCKED**

只自动 Trim 首尾空白；不改内部连续空格、标点、大小写、全半角等正式命名格式。

## Q124 — `netease_url` 规范化
**用户选择：B**
**状态：LOCKED**

- 能可靠识别网易云 album ID 时，统一规范成标准专辑 URL；
- 无法可靠识别时不擅自重写；
- 继续遵守 Q118 的身份一致性校验。

## Q125 — `netease_url` 平台限制
**用户选择：B**
**状态：LOCKED**

`netease_url` 要么为空，要么是受支持的网易云专辑链接；V1 不允许其他平台 / 通用外部链接。

## Q126 — 删除 Album 后落点
**用户选择：A**
**状态：LOCKED**

- 删除成功后对应 Sheet 关闭；
- 底层 Grid 直接呈现真实状态；
- 删除最后一张 Album 后自然进入专辑页空状态；
- 不自动切 Tab / 管理页。

## Q127 — 删除后的 Grid Motion
**用户选择：B**
**状态：LOCKED**

- 被删除项轻微 opacity + scale 退出；
- 后续 Album 通过 Reorder Motion 平滑补位；
- 可中断；Reduced Motion 下弱化为轻量淡出 + 直接重排。

## Q128 — 切换排序的 Reorder Motion
**用户选择：B**
**状态：LOCKED**

- 仅当前 viewport 内仍挂载 / 可见的 Album 做 Reorder Motion；
- 屏幕外 / 虚拟化内容直接采用新顺序；
- 不为了动画扩大 DOM；
- 快速切换追随最新状态。

## Q129 — 排序变化后的滚动位置
**用户选择：B**
**状态：LOCKED**

切换排序视为新浏览语义，回到新排序顶部；不为不同排序单独记忆滚动位置。

## Q130 — 时间筛选变化后的滚动位置
**用户选择：B**
**状态：LOCKED**

与排序一致：时间筛选改变结果集合后回到顶部。

## Q131 — 普通 Search Mode 退出
**用户选择：B**
**状态：LOCKED**

从专辑页主动进入搜索时，退出后完整恢复进入搜索前的筛选、排序与滚动位置。

## Q132 — 统计页成员 Drill-down 的取消
**用户选择：B**
**状态：LOCKED**

统计页点击成员 → 专辑 Search Mode（带成员 + 当前周期）；点击“取消”返回原统计页并恢复周期 / 位置，而不是留在普通专辑页。

## Q133 — 统计成员 Drill-down 的系统返回
**用户选择：A**
**状态：LOCKED**

系统返回与“取消”语义一致；若上层还有 Album Sheet，则返回优先逐层关闭最上层状态。

## Q134 — Drill-down 中主动切其他 Tab
**用户选择：A**
**状态：LOCKED**

统计页发起的成员 Search Mode 是临时跨 Tab 上下文；主动切到其他一级 Tab 即视为结束，之后再进专辑 Tab 恢复专辑自身原状态。

## Q135 — 桌面导航形态
**用户选择：D**
**状态：LOCKED**

- 手机 / 窄屏 / 部分平板：Bottom Tab Bar；
- 真正宽屏：左侧轻量 Navigation Rail；
- breakpoint 由实现阶段实际布局测试决定，不现在锁死；
- Rail 只放专辑 / 动态 / 统计，不扩张为后台 Sidebar。

## Q136 — 桌面主内容最大宽度
**用户选择：C**
**状态：LOCKED**

响应式最大宽度：先利用空间，达到舒适尺度后主要增加外侧留白，不无限增加列数 / 拉长内容。

## Q137 — 桌面 Album Grid 密度
**用户选择：B**
**状态：LOCKED**

中等封面 + 中等密度，视觉上约 5～7 列量级；具体列数 / 卡片宽度实现时响应式确定。

## Q138 — 桌面动态页
**用户选择：B**
**状态：LOCKED**

仍为单列时间线，限制阅读宽度；不双列、不加右侧辅助栏，超宽空间允许留白。

## Q139 — 桌面统计页
**用户选择：B**
**状态：LOCKED**

宽屏使用左右两栏：左侧大数字总结，右侧成员排行；顶部 Segmented Control 统一控制周期；不新增图表 / Dashboard 指标。

## Q140 — 桌面 Large Title
**用户选择：B**
**状态：LOCKED**

桌面仍保留 Large Title → Compact Title，但字号 / 位移 / 收缩幅度比移动端更克制。

## Q141 — 桌面专辑工具行
**用户选择：C**
**状态：LOCKED**

真正宽屏尽量把搜索 + 时间筛选 + 排序放同一行；宽度不足自动退回两行；移动端保持现有布局。

## Q142 — 桌面 Album Detail
**用户选择：C**
**状态：LOCKED**

宽屏不再使用横跨屏幕的 Bottom Sheet，而改为居中 / 偏右的 Floating Detail Sheet；仍复用同一个 Album Detail 组件与状态机。

## Q143 — Floating Sheet 落点
**用户选择：C**
**状态：LOCKED**

来源封面位置 + 当前可用空间 + 统一桌面布局区域共同决定中央 / 偏右落点；不固定死居中，也不贴着源做 Popover。

## Q144 — Floating Sheet 尺寸
**用户选择：B**
**状态：LOCKED**

响应式 min / max 尺寸；达到舒适尺度后停止放大；内容超过最大高度时内部滚动；查看态与编辑态共享同一尺寸框架。

## Q145 — 桌面 Sheet 内布局
**用户选择：D**
**状态：LOCKED**

- 查看态：封面左、信息右的横向布局；
- Admin 编辑态：同一 Sheet 内连续重排为较适合表单的纵向布局；
- 不创建新 Sheet / 页面。

## Q146 — Floating Sheet 拖动
**用户选择：B**
**状态：LOCKED**

桌面 Floating Sheet 不支持自由拖动，位置由布局系统稳定控制。

## Q147 — Sheet 打开时点击背景其他 Album
**用户选择：C**
**状态：LOCKED**

- 背景不能滚动，搜索 / 筛选 / Tab 等不可交互；
- 当前可见 Album 封面仍可点击，用来切换详情；
- Sheet 容器保持稳定，不重复完整 Shared Element 开启动画；
- 移动端不引入这套行为。

## Q148 — 桌面详情内 Album 切换 Motion
**用户选择：B**
**状态：LOCKED**

打开 / 关闭详情使用 Spatial + Shared Element；详情已打开后的 Album 切换只用 UI Motion：封面 Crossfade + 文字轻量 Enter / Exit，容器保持稳定。

## Q149 — 快速连续切换 Album
**用户选择：C**
**状态：LOCKED**

允许连续点击，动画可中断并追随最后一次选择；中间状态不排队；异步数据 / 图片响应不得覆盖最新目标。

## Q150 — 再点当前 Album
**用户选择：C**
**状态：LOCKED**

只给 Micro 级按压反馈，不关闭、不重播、不刷新。

## Q151 — Sheet 打开时背景 hover
**用户选择：D**
**状态：LOCKED**

背景 Album 不保留 hover，只在实际 pointer down / click 时给轻量 Press；仍保持可点击语义。

## Q152 — 当前 Album selected 状态
**用户选择：D**
**状态：LOCKED**

背景当前 Album 使用非常克制的 selected 标记，不加明显描边 / 勾选 / 强强调色，可通过轻微字重 / opacity / 内层高光表达。

## Q153 — 桌面遮罩强度
**用户选择：C**
**状态：LOCKED**

桌面仍保持明确模态层级，但 Dim / Blur 比移动端稍弱，使背景 Album 仍清晰可辨并可选择；Sheet 仍为绝对焦点。

## Q154 — 桌面滚轮归属
**用户选择：B**
**状态：LOCKED**

Floating Sheet 打开时背景滚动完全冻结；滚轮 / 触控板纵向滚动只交给详情层处理，不穿透到底层 Grid。

**用户随后明确：桌面端暂时不继续讨论这么细。** 因此桌面端精确像素、breakpoint、Dim / Blur 数值、滚轮边缘行为等均留实现阶段调试，不再 Grill。

## Q155 — PWA 主动安装提示
**用户选择：B**
**状态：LOCKED**

首次进入不立刻提示；用户已正常使用一段时间后只轻量提示一次；拒绝后不反复打扰。

## Q156 — 手动安装入口
**用户选择：B**
**状态：LOCKED**

拒绝主动提示后，`···` 菜单长期保留“添加到主屏幕”；当前环境不支持安装则不显示。

## Q157 — 从网易云返回 AlbumRobot
**用户选择：C**
**状态：LOCKED**

尽可能恢复离开前页面浏览上下文（Tab / 滚动 / 筛选 / 排序 / Search 等），但不恢复外跳前的 Album Detail Sheet。

## Q158 — 冷启动状态
**用户选择：A**
**状态：LOCKED**

真正冷启动统一从：

- 专辑 Tab；
- 时间筛选“全部”；
- 排序“最近分享”；
- 顶部位置。

不恢复上次 Tab / 滚动 / Search / 历史月份 / Sheet / Admin Mode / 上次筛选排序。普通访问授权与设备管理员授权仍可保持。

## Q159 — App 回前台刷新
**用户选择：A**
**状态：LOCKED**

每次从后台恢复到前台执行轻量静默数据检查；有新数据时按各页面规则呈现，但不强行改变当前阅读位置。

## Q160 — 回前台时 Detail Sheet 数据
**用户选择：A**
**状态：LOCKED**

若 Album Detail Sheet 仍打开，则可变详情数据也一起静默刷新；原位 UI Motion 更新，不关闭 Sheet、不重播 Shared Element。

## Q161 — 回前台时 Admin 编辑态
**用户选择：A**
**状态：LOCKED**

- 当前表单输入绝不被远端刷新覆盖；
- 只静默检查远端 Album 是否变化；
- 适合通过 `updated_at` / `version` 做乐观并发控制。

## Q162 — 发现远端版本冲突时保存
**用户选择：B**
**状态：LOCKED**

- 阻止直接覆盖；
- 完整保留管理员当前输入；
- 允许查看最新数据后自行决定下一步；
- V1 不做自动字段合并，也不提供强制覆盖。

## Q163 — 云端平台：Cloudflare 全栈免费方案
**用户选择：A**
**状态：LOCKED / SUPERSEDES CLOUDBASE**

当前正式基线：

```text
GitHub Repository
      ↓ 部署
Cloudflare Free
├─ Workers Static Assets → AlbumRobot PWA
├─ Worker API            → REST / Sync / Auth / Admin
└─ D1                    → SQL 数据库
```

硬约束：

- CloudBase 主方案作废；
- 目标长期 0 元运行；
- 保持 Cloudflare Free Plan，不主动升级 Paid；
- 免费额度用尽时宁可失败 / 限流，也不自动产生费用；
- 可直接使用 `workers.dev` 等免费地址，域名购买属于可选额外成本；
- 前后端同源，避免额外 CORS 复杂度。

## Q164 — AlbumRobot Sync V1 运行方式
**用户选择：A**
**状态：LOCKED / SUPERSEDES BOT**

- QQ Bot 暂不使用；
- 新增独立本地工具 **AlbumRobot Sync**；
- V1 纯手动：打开工具 → 点击“立即同步” → 同步结束后可关闭；
- 不后台常驻、不定时、不自启；
- 架构允许未来升级，但 V1 不实现。

## Q165 — QQ 数据入口路径
**用户选择：D**
**状态：LOCKED**

双路径：

- 主路径：AlbumRobot Sync 通过 QQ Adapter 直接获取数据；
- 备用路径：结构化 JSON 导入；
- 两者尽快统一为 `RawQQMessage[]`，后续识别 / 去重 / 同步共用。

## Q166 — Adapter 依赖封装
**用户选择：D**
**状态：LOCKED**

- 目标是一键同步，但不硬绑死无法稳定分发的第三方组件；
- 能合理内置 / 自动管理的就封装；
- 否则自动检测环境并给清晰配置提示；
- Adapter 不可用时保留 JSON 备用入口。

## Q167 — 目标群选择 + Adapter 锁定
**用户选择：A；并明确“adapter 使用 QCE”**
**状态：LOCKED**

- QQ Adapter 正式锁定为 **QCE**；
- 首次连接 QCE 后读取可访问群列表；
- 用户选择一次目标群并保存稳定群标识；
- 之后默认锁定该群，不每次重新选择；
- V1 仍只服务当前一个群。

## Q168 — Sync 如何从 QCE 取数据
**用户选择：C**
**状态：LOCKED**

`QceAdapter` 双 Provider：

- `DirectApiProvider`：优先使用 QCE 本地 API 直接取消息；
- `JsonExportProvider`：Direct API 不可用时自动降级为 QCE JSON 导出；
- 用户仍然只需要一次“立即同步”；
- 两条路径统一输出 `RawQQMessage[]`。

## Q169 — 首次 / 日常同步确认
**用户选择：B**
**状态：LOCKED**

- 首次历史全量扫描：必须预览识别结果 / 异常，人工确认后才上传；
- 后续日常增量：一键自动上传有效 Share；
- 解析失败单独报告，不阻塞其他记录。

## Q170 — 增量游标策略
**用户选择：C**
**状态：LOCKED**

- 优先 QCE 稳定 message ID / sequence；
- 每次从游标前方保留一小段重叠扫描窗口；
- 允许重复读取，不允许重复入库；
- 云端用 `group_id + source_message_id` 等稳定来源键幂等去重；
- 若真实 QCE 样本没有可靠 message ID，再基于样本设计 fallback key，不现在猜。

## Q171 — 部分失败时游标提交
**用户选择：C**
**状态：LOCKED**

- Sync 把一批标准化 Share 通过 `/api/sync/batch` 提交；
- Cloudflare 明确完整处理整个批次后返回 batch result；
- 只有收到明确回执，客户端才原子提交新本地游标；
- 网络中断 / 超时 / 无法确认时不推进游标；
- 下次可重传，依靠云端幂等消除重复。

## Q172 — 本地状态丢失恢复
**用户选择：C**
**状态：LOCKED**

- 本地 `SyncState` 保存目标群、QCE adapter state、last message/seq、last success 等完整状态；
- Cloudflare 保存最小 `SyncCheckpoint`；
- 正常运行以本地状态为主；
- 本地状态丢失时从云端 checkpoint 前方安全重叠扫描，依靠幂等恢复并重建本地状态。

## Q173 — 网易云识别位置
**用户选择：B**
**状态：LOCKED / PRIVACY BOUNDARY**

- QCE 完整群消息只在 AlbumRobot Sync 本地处理；
- `NeteaseAlbumDetector` 本地完成全部识别；
- Cloudflare 只接收已确认的 `ShareCandidate`；
- 普通聊天文本、无关图片、群聊上下文等绝不上传云端。

## Q174 — 本地专辑识别分级
**用户选择：B**
**状态：LOCKED**

识别优先级：

1. QCE 明确结构化网易云专辑卡片；
2. 卡片 / 消息中可靠的网易云 Album URL / album ID；
3. 网易云短链 → 本地解析跳转 → 确认 Album；
4. 只剩标题 / 文本、没有可靠 URL / ID → 不自动收录，记录解析异常。

V1 不使用 AI / 模糊文本匹配猜专辑。

## Q175 — Album 元数据来源
**用户选择：B**
**状态：LOCKED**

- QCE 卡片已提供的 title / artist / cover 等优先直接使用；
- 字段缺失时，才根据已经确认的 `netease_album_id` 由本地 Metadata Resolver 补全；
- 网易云补全不参与模糊身份判断；
- 同一 album_id 应使用本地缓存避免重复请求。

## Q176 — 本地 Metadata Cache
**用户选择：B**
**状态：LOCKED**

缓存优先 + 按需刷新：

- 已有完整缓存直接复用；
- 仅在必要字段缺失、cover 已确认失效、缓存 schema 升级、管理员明确修复等情况下重新获取；
- 不设置固定 30 天 / 90 天周期刷新。

## Q177 — 解析失败项处理
**用户选择：B**
**状态：LOCKED**

- 失败项只保留在 AlbumRobot Sync 本地“解析异常”列表；
- 可保留排错所需的本地原消息结构；
- 不阻塞正常 Share；
- 不上传 Cloudflare；
- 此决策覆盖旧 Q100～Q102 的“网页显示历史导入错误摘要”方案。

## Q178 — 解析失败项人工修复
**用户选择：B**
**状态：LOCKED**

- 管理员只需粘贴一个可靠的网易云专辑链接；
- Sync 解析 album ID → Metadata Resolver 补全 → 结合原消息发送者 / 日期 / source_message_id → 生成正常 `ShareCandidate`；
- 不提供手工填写整套 Album 字段的重型后台表单。

## Q179 — 人工修复后的上传时机
**用户选择：B**
**状态：LOCKED**

- 修复成功后进入本地“待同步”队列；
- 不立即单独上传；
- 下一次点击“立即同步”时，与普通新增 Share 一起走统一 `/api/sync/batch`；
- 自动识别与人工修复最终只有一条云端写入路径。

## Q180 — 待同步来源展示
**用户选择：B**
**状态：LOCKED**

Sync UI 以待同步总数为主，同时轻量显示来源构成，例如：

```text
待同步 7 条
5 条新增
2 条已修复

[立即同步]
```

同步完成后可按“新增 / 已修复 / duplicate / invalid”等给轻量结果摘要，但不拆成多个同步按钮。

---

# A2. Grill Me 决策登记（Q181～Q219）

> 本节补齐最后一轮实现前讨论。除非后续用户明确推翻，否则全部按 **LOCKED** 处理。

## Q181 — Sync 本地持久化形式
**选择：B · LOCKED** 采用 **SQLite + 极少量配置文件**。SQLite 保存 SyncState、Pending Queue、Parse Errors、Metadata Cache、ignored/repaired 最小状态；`config.json` 仅保存纯本机 / UI 配置。

## Q182 — Sync 本地数据目录
**选择：B · LOCKED** 使用 Windows 标准用户数据目录：
```text
%LOCALAPPDATA%\AlbumRobot\
├─ albumrobot.db
├─ config.json
└─ logs\
```
程序与数据分离；更新 / 重装不应主动删数据；设置中提供“打开数据文件夹”。

## Q183 — Parse Error Raw Payload 生命周期
**选择：B · LOCKED** 未处理异常可在本机保留必要 Raw Payload；修复成功或明确忽略后立即删除，只留最小结果记录。

## Q184 — ignored 行为
**选择：D · LOCKED** ignored 后正常扫描永久跳过；不保留 Raw Payload；提供“重新检查已忽略项”入口。

## Q185 — SQLite 备份
**选择：D · LOCKED** V1 不做常规定期备份；仅 Schema Migration 前临时备份，迁移成功后删除。

## Q186 — Sync 日志 / 诊断
**选择：D · LOCKED** 默认结构化日志，不记录普通聊天正文、完整 Raw Payload、群聊上下文、密码 / Session、完整 Batch 请求体；针对单条异常可主动导出诊断包。

## Q187 — 诊断包隐私边界
**选择：B · LOCKED** 默认脱敏，只保留消息类型、卡片字段、URL/app/meta、source message id、必要时间、detector result、QCE/Sync 版本；移除群名、群号、成员稳定 ID、昵称、普通正文、认证信息、无关本机路径。

## Q188 — 日志保留
**选择：C · LOCKED** 文件轮转 + 数量上限；具体单文件大小 / 保留数量为实现参数。

## Q189 — 本地敏感字段加密
**选择：B · LOCKED** SQLite 整体不加密，仅加密如 `parse_errors.raw_payload` 等敏感字段。

## Q190 — 加密密钥
**选择：A · LOCKED** 固定密钥随程序提供。安全定位仅为防止直接打开 SQLite 即看到 QQ 原始内容，**不得描述为高强度设备级加密**。

## Q191 — Pending Queue 状态机
**选择：C · LOCKED**
```text
pending → submitting
            ├─ accepted  → 移除
            ├─ duplicate → 移除
            ├─ invalid   → 本地异常
            └─ unknown   → 回到 pending
```
unknown 不推进游标。

## Q192 — Pending 本地幂等
**选择：C · LOCKED** 以稳定来源键唯一并 Upsert，概念约束 `UNIQUE(group_id, source_message_id)`；人工修复覆盖原 Candidate 并标记 `origin=repaired`；accepted/duplicate 不重建；ignored 跳过。

## Q193 — submitting 崩溃恢复
**选择：C · LOCKED** 启动时遗留 `submitting` 一律恢复为 `pending`，下次安全重传，依赖云端幂等收敛。

## Q194 — Batch 中有 invalid 时的游标
**选择：B · LOCKED** 只要每个 Item 都有明确终态（accepted / duplicate / invalid），Batch 视为完整处理并可推进游标；unknown / 响应缺失 / 无法确认则不推进。

## Q195 — `/api/sync/batch` 回执
**选择：B · LOCKED** 必须包含 Batch 总状态 + 每条 Item 明确结果；客户端据此删除 accepted/duplicate、转移 invalid、判断 complete、提交 checkpoint。

## Q196 — Sync 技术栈
**选择：A · LOCKED** **C# / .NET Windows 桌面应用**。WPF / WinUI / Avalonia 等由 Codex 自行选择。

## Q197 — QCE 与 Sync 关系
**选择：B · LOCKED** Sync 尽量自动管理 QCE 外部运行时依赖：自动发现、状态检测、必要启动、兼容性检查、故障提示；Token/端口/进程/版本属于实现细节。

## Q198 — PWA 技术栈
**选择：A · LOCKED** **React + Vite + TypeScript**，SPA/PWA，不引入不必要 SSR。

## Q199 — Sync 首次运行体验
**选择：A · LOCKED** 极简自动引导：检查 QQ/QCE → 连接 → 选择群 → 首次历史扫描 → 预览 → 确认首次同步。日常首页只突出上次同步、待同步、立即同步、异常数。

## Q200 — Android APK
**选择：A · LOCKED** V1 只发布 Web/PWA，不同时发布 APK；V1.x 再按实际需求评估。

## Q201 — 首次群密码呈现
**选择：B · LOCKED** 直接呈现 App Shell，群共享密码以轻量覆盖层出现；认证成功后覆盖层自然消失，不做 Login Page→Home 硬切。

## Q202 — 密码错误反馈
**选择：B · LOCKED** 输入区域原位反馈；不 Dialog、不叠 Toast、不夸张 Shake，保持焦点并可立即重试。

## Q203 — 正式访问域名
**选择：B · LOCKED** `https://album.rocknrollliberty.dpdns.org`。根域名 `rocknrollliberty.dpdns.org` 保留其他用途。DNS/TLS/大陆可达性仍需部署实测。

## Q204 — 首次认证后的 Onboarding
**选择：A · LOCKED** 不做欢迎页、教程或品牌引导，认证成功后直接进入专辑库。

## Q205 — 修改群共享密码
**选择：A · LOCKED** 修改后所有普通浏览 Session 立即失效；Admin Authorization 与普通 Session 分离。

## Q206 — 普通 Session 生命周期
**选择：A · LOCKED** 长期有效，不设 30/90 天人为到期；密码变更 / 本地数据清除 / 服务端撤销时重新认证。

## Q207 — PWA 离线能力
**选择：B · LOCKED** 最近内容只读离线缓存；允许最近专辑、动态、统计、必要封面；离线必须明确提示，不允许管理写操作，不假装数据最新；联网后静默重验。

## Q208 — Session 失效与离线缓存
**选择：A · LOCKED** 普通 Session 失效时，业务离线缓存立即清除。

## Q209 — 封面离线缓存
**选择：B · LOCKED** 只受控缓存用户最近实际成功加载过的封面；未缓存离线使用占位；不预下载整个专辑库。

## Q210 — 普通用户最后同步时间
**选择：B · LOCKED** 主页面不常驻显示；仅在全局 `···` 菜单中轻量显示“数据更新于……”，不做警告 Banner。

## Q211 — Admin 同步状态
**选择：B · LOCKED** 管理工具提供轻量只读 Sync Status：最近同步、完成状态、新增/duplicate/invalid 摘要、来源；禁止网页远程启动 Sync、查看 QQ Raw、查看本地 Parse Errors 或复杂 Batch Dashboard。

## Q212 — 修改密码入口
**选择：B · LOCKED** Admin Mode → 管理工具 → 访问与安全，可改群共享密码和管理员密码；不做用户列表、设备管理、多管理员、RBAC。

## Q213 — 修改管理员密码
**选择：A · LOCKED** 所有设备已有 Admin Authorization 立即失效，当前设备退出 Admin Mode；普通用户 Session 不受影响。

## Q214 — 当前昵称刷新
**选择：B · LOCKED** 每次 Sync 顺便刷新目标群成员信息并更新 Member 当前昵称；QCE 若无法可靠提供完整成员列表可做技术降级，但产品目标不变。

## Q215 — 已退群成员
**选择：A · LOCKED** Member、历史 Share、历史统计保留，显示最后一次已知昵称，不显示“已退群”标签。

## Q216 — JSON Provider 入口
**选择：B · LOCKED** 默认隐藏在高级 / 故障处理；Direct API 不可用时主动提供重试 / JSON 导入；高级设置保留手动入口。

## Q217 — 首次历史预览
**选择：B · LOCKED** 汇总 + 可浏览识别结果；支持抽查，不要求逐条勾选全部历史记录。

## Q218 — 首次预览误识别
**选择：B · LOCKED** 可对单条执行“忽略这条分享”；写 ignored、清 Raw Payload、后续重叠扫描跳过，可通过“重新检查已忽略项”再次尝试。

## Q219 — Album 实体作用域
**选择：A · LOCKED** Album 为全局实体，`netease_album_id` 全局唯一；Group 隔离 Member / Share / Stats / Auth 上下文。

# A3. 当前架构覆盖关系

以下历史方案已经被后续 LOCKED 决策覆盖：

1. **QQ Bot 采集 → SUPERSEDED**：当前是 AlbumRobot Sync + QCE。
2. **Tencent CloudBase → SUPERSEDED**：当前是 Cloudflare Free。
3. **一次性历史 Import CLI → SUPERSEDED**：当前由 Sync 统一承担首次全量和后续增量。
4. **Web Import Job / Parse Error 列表 → SUPERSEDED**：Parse Error 只留本地；网页只保留轻量只读 Sync Status。
5. **品牌 Splash → SUPERSEDED**：当前直接进入 App Shell。
6. **继续细粒度 Grill → SUPERSEDED**：Q219 后默认直接开发。

# B. 开发前最终确认

用户已明确同意以下三项，Codex 可直接执行：

1. **继续使用现有 AlbumRobot GitHub 仓库作为唯一仓库。** 推荐 monorepo，不拆成多个 GitHub 仓库。
2. **QCE 技术探针可以使用目标 QQ 群真实小样本。** Raw QQ 数据不得提交 GitHub / Cloudflare；需要 fixture 时只提交脱敏最小结构。
3. **先完成本地最小端到端链路，再首次部署公网。** 至少完成普通访问密码机制后再部署；首个线上域名为 `album.rocknrollliberty.dpdns.org`。

# C. 第一开发里程碑：Vertical Slice

```text
真实 QCE 小样本
        ↓
C# QceAdapter
        ↓
识别至少 1 条真实网易云专辑分享
        ↓
本地 SQLite / Pending Queue
        ↓
POST /api/sync/batch
        ↓
Cloudflare Worker + D1
        ↓
React PWA
        ↓
专辑页成功展示这张真实专辑
```

## C1. 验收条件
1. QCE Direct API 可连接；
2. 可读取目标群；
3. 可取得至少一条真实网易云专辑分享；
4. 能确认真实 group/member/message 稳定字段或明确记录不可用；
5. `NeteaseAlbumDetector` 能从真实结构得到可靠 album id / URL；
6. 缺 metadata 时 Resolver 能补全或明确失败；
7. Pending Queue 本地幂等工作；
8. Batch API 可 accepted / duplicate 收敛；
9. D1 中 Album / Member / Share 关系正确；
10. React PWA 通过 API 成功读取并显示 Album。

Vertical Slice 完成前，不优先做完整 Admin、完整 Motion、完整历史导入、或大量假数据 UI。

# D. 推荐 Monorepo 结构

```text
AlbumRobot/
├─ apps/
│  ├─ web/                 # React + Vite + TypeScript PWA
│  └─ sync/                # C#/.NET AlbumRobot Sync
├─ worker/                 # Cloudflare Worker API
│  ├─ src/
│  └─ migrations/          # D1 migrations
├─ packages/
│  └─ contracts/           # 可选：API contracts / schemas
├─ docs/
│  ├─ PROJECT_PROGRESS_CODEX.md
│  ├─ architecture/
│  └─ decisions/
├─ fixtures/
│  └─ sanitized/           # 只允许脱敏最小 QCE fixture
├─ .gitignore
└─ README.md
```

Raw QQ fixture、本地 SQLite、日志、QCE token、密码 / secret 必须进入 `.gitignore`。

# E. 开发阶段顺序

## Phase 0 — 环境与真实技术探针
QCE Direct API、群列表、消息分页、成员、稳定 ID/seq、网易云卡片结构、JSON 降级格式、网易云短链/Metadata、Cloudflare Free/D1 最小验证。

## Phase 1 — Vertical Slice
完成 C 节整条链路。

## Phase 2 — Sync V1
首次自动引导、目标群选择、QCE 自动发现/连接、首次历史全量预览、日常增量、SyncState、Pending、Parse Errors、Manual Repair、Metadata Cache、日志、脱敏诊断包、JSON Provider 降级。

## Phase 3 — Cloud API + Data Model
Auth、Album/Share/Member、Browse API、Search、Stats、Batch Sync、SyncCheckpoint、Admin mutations、AdminAction、访问与安全、轻量 Sync Status。

## Phase 4 — PWA Functional
专辑、动态、统计、Search Mode、Bottom Sheet 基础逻辑、Auth overlay、Admin Mode、recent-read offline cache、PWA manifest、状态恢复。

## Phase 5 — Apple Music Visual + Motion Polish
Theme Tokens、Typography、Large Title、Material Tab Bar、Shared Element、Gesture Ownership Lock、Bottom Sheet spring/rubber-band、Tab Transition、Reorder Motion、Pull to Refresh、Reduced Motion、性能降级、真实设备 QA。

## Phase 6 — Deploy + Real Group Trial
Cloudflare Static Assets/Worker/D1、绑定 `album.rocknrollliberty.dpdns.org`、国内网络实测、QQ 内置浏览器/iOS Safari/Android Chrome、群内试用；仅在实测失败后考虑迁移部署方案。

# F. UNVERIFIED 技术探针清单

## QCE
- Direct API 真实 base URL / auth；
- 群列表 schema、群稳定 ID；
- Member 稳定 ID / nickname；
- Message ID / seq / pagination；
- 历史消息覆盖范围 / 离线补消息；
- 网易云专辑卡片真实结构；
- JSON Export schema；
- Direct / JSON 字段差异。

## 网易云
- 短链当前解析方式；
- album URL 规范化；
- metadata 来源；
- cover URL 稳定性 / 防盗链；
- 地区 / UA / cookie 限制。

## Cloudflare
- Free Plan 当前额度；
- Static Assets + Worker 同源配置；
- D1 transaction / constraint 行为；
- Custom Domain 绑定；
- `album.rocknrollliberty.dpdns.org` DNS / TLS；
- 中国大陆不同运营商可达性。

## PWA / Browser
- QQ 内置浏览器；iOS Safari；Android Chrome；
- PWA install；从网易云返回生命周期；
- View Transition / Shared Element；Pointer / Touch / overscroll；
- backdrop blur；Safe Area；Keyboard；History/Back；Cache Storage quota。

# G. 当前核心数据语义

## Album
全局实体，核心身份 `netease_album_id`。同 ID = 同 Album；不同 ID 默认不同版本，管理员可 Merge。

## Member
Group-scoped；必须基于 QCE 可验证稳定身份，不依赖昵称。历史展示当前昵称；每次 Sync 刷新；退群保留最后已知昵称；Legacy Member 可存在并后续 Merge。

## Share
一次 QQ 专辑分享事件。所有 Share 保留；重复分享动态显示“再次分享了”。云端幂等优先依赖 `group_id + source_message_id`；若真实 QCE 没可靠 message id，再基于样本设计 fallback。

# H. 当前统计口径

业务时区基线 `UTC+8`。

群总数：`COUNT(DISTINCT album_id)`。

成员排行：`GROUP BY member_id + COUNT(DISTINCT album_id)`。

“分享最多”专辑排序：`COUNT(DISTINCT member_id)`，不是 Share 总次数。

# I. 隐私与安全边界

1. 完整 QQ 群聊不离开本机；
2. Cloudflare 只接收标准化业务数据；
3. Parse Error Raw Payload 仅 Sync 本地短期存在；
4. repaired / ignored 后立即删除 Raw Payload；
5. 日志不保存聊天原文；
6. 诊断包默认脱敏；
7. Raw fixture 不提交 Git；
8. 密码、Token、Session、QCE Token、Cloudflare secrets 不提交 Git；
9. AdminAction 不记录完整请求 Payload / 密码 / QQ 原文；
10. 普通 Session 失效时离线业务缓存立即清除。

# J. V1 明确禁止擅自扩展

未经用户重新确认，不要新增：QQ Bot 主采集、CloudBase、VPS、Paid Cloudflare、Android APK、iOS App、个人账号、收藏/待听、评分、评论、个人主页、歌单、AI 文本识别、歌曲/歌单分享采集、WebSocket/SSE、多群 UI、复杂 Admin Dashboard、Batch Audit Dashboard、回收站、Event Sourcing、Undo、多管理员/RBAC、完整 Offline-first、自动定时/常驻 Sync、高成本品牌动画。

# K. Implementation-ready 状态

```text
product_design_complete = true
architecture_baseline_complete = true
grill_closed = true
implementation_started = false
next_step = phase_0_probe_then_vertical_slice
```

当 Codex 开始实际修改代码后，将 `implementation_started = true` 写入后续进度文档。

Codex 第一条工作指令：

> **先检查现有 AlbumRobot 仓库状态；然后建立最小 monorepo 基线和技术探针，不要先做完整 UI。**
