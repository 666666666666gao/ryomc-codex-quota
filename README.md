# Ryomc 上游额度 · Codex Plugin

在 Codex 对话内查询 **CPA 上游 Codex 账号的周额度剩余比例、重置时间**，也可在 Codex 右侧浏览器面板打开本机额度页。

这是一个 **skills + Node.js 脚本** 插件，无第三方 npm 依赖，不依赖 MCP 服务器。另附 Windows 独立悬浮条和服务器只读接口。它不是 Chrome 扩展，不替换 Codex 原生状态栏，也不显示 New API 钱包余额。

## 普通用户：右下角悬浮条（推荐）

**普通用户不需要 CPA 管理密钥。** 管理员部署 `server/quota_service.py` 后，向用户提供 HTTPS 额度接口与专用只读令牌。用户将二者保存到用户目录 `.config/ryomc-codex-quota/reader.json`，然后运行 `RyomcQuota.exe`。

悬浮条贴近 Codex 窗口右下角显示共享线路周额度，每两分钟刷新；支持拖动、手动刷新和关闭。不是个人账户钱包，不是嵌入式原生状态栏。查询失败不继续显示旧额度。

详见 [部署、配置、构建及回滚说明](server/DEPLOYMENT.md)。下方原始管理地址/管理密钥配置仅适用于管理员自用面板。

## 功能

- 从 CPA 管理 API 查询真实上游数据，而非截图或推算余额。
- 严格识别 604800 秒（7 天）窗口，不将月额度、代码审查额度或其他模型额度冒充周额度。
- 中文界面、进度条、北京时间重置时间、查询时间、手动刷新。
- 查询失败清除旧显示；未提供重置时间显示未知。
- 本机配置入口：密钥不进入 Git 仓库，不进入查询结果，不回传浏览器。
- 只读查询：不修改 CPA 配置，不重置额度，不生成模型探测请求。

## 安装

需要 Node.js 22+、支持本地插件的 Codex，以及你自己 CPA 的管理访问权限。

把本仓库克隆到 `~/plugins/ryomc-codex-quota`（Windows 对应 `%USERPROFILE%\plugins\ryomc-codex-quota`）。
在 Codex 中让代理使用内置 plugin-creator 的个人市场流程注册这个**已有插件**，保留源代码，不用 `--force` 覆盖。
如果已由代理创建个人市场条目，直接运行：

```sh
codex plugin add ryomc-codex-quota@personal
```

个人市场可能不是 `personal`，请使用实际市场名。安装后新建一个 Codex 对话，以加载插件技能。

## 使用

在 Codex 中选择本插件并说：

> 打开 Ryomc 额度面板

首次在面板“连接设置”里输入：

1. CPA 管理地址：例如 `https://cpa.example.com`，不是 New API 的 `/v1`，不含 `/management.html`。
2. CPA 管理密钥：不是 New API 令牌，也不是 CPA 的模型调用密钥。
3. 如果有多个 Codex 账号，填目标账号 `auth_index`；只有一个时留空。
4. 如果管理元数据没有提供 ChatGPT Account ID，填写目标账号的 Account ID。

点击“保存到本机并查询”。后续可以直接说：

> 查看我的 CPA 上游周额度

也可独立运行：

```sh
node scripts/quota.mjs query
node scripts/quota.mjs serve
```

`serve` 输出随机端口的本机 URL，带临时访问凭据，仅在服务运行期间有效。不要公开分享该 URL。服务仅绑定 `127.0.0.1`，不会开放公网端口。关闭进程即停止面板；再次启动用新的 URL。

## 安全与数据流

```text
Codex 技能 / 本机面板 → Node.js 查询客户端 → CPA 管理 API → 固定的上游 usage GET
```

配置写在用户目录 `.config/ryomc-codex-quota/config.json`，**不在仓库中**。Windows 设置目录当前用户 ACL；POSIX 新建目录 0700、文件 0600。配置为磁盘明文，不是保险库；有当前用户权限的软件仍可能读取它。请只在自己的管理员电脑使用，不分发管理密钥给客户。

管理密钥拥有 CPA 管理权限，本插件仅使用其中两项接口：`GET /v0/management/auth-files` 与 `POST /v0/management/api-call`。后者的请求目标固定为 `GET https://chatgpt.com/backend-api/wham/usage`。不会读取本机 Codex 的 auth.json，也不会将 OAuth 凭据返回给页面。

远程 CPA 必须 HTTPS（证书正常验证、禁止重定向）。本机 HTTP 仅允许 loopback。面板拒绝跨源 API 请求，API 需要启动时的随机访问凭据，无 CORS、无外部脚本、无统计服务。

本工具依赖 CPA 管理协议及上游非公开 usage 接口；上游调整后可能需要更新。读取成功不等于模型请求健康，不承诺账号授权或长期可用性。

## 故障说明

- 管理接口 401/403：检查管理密钥和远程管理权限，不要换成模型调用密钥。
- 上游 401：检查 CPA 账号授权状态。
- 上游 429：额度查询被限流；不自动认定周额度耗尽。
- 无唯一 7 天窗口：不猜测，不用其他窗口代替。
- 多个账号：明确填写目标账号的 auth_index。
- 网页访问凭据失效：从插件重新打开本机面板。

## 测试

```sh
npm test
```

自动化测试使用合成数据与本机测试服务，**不是实时额度证据**。真实账号端到端验证需要用户在本机填写自己的 CPA 管理密钥。

不包含微信机器人或每日定时发送，不修改任何已有网站或 CPA 计费设置。
