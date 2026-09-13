# Ryomc 上游额度 · Codex Plugin

读取 **Codex 已保存的中转配置**，显示对应共享线路上游 Codex 账号的 **周额度剩余比例、重置时间**。附 Windows 右下角悬浮条。

**普通用户不需要 CPA 管理地址、管理密钥，也不需要额外的只读令牌。** 默认使用本机已有的 Base URL 和模型 API 密钥。不会修改 `config.toml` / `auth.json`，不会把密钥写入插件目录。

## Windows：下载后直接运行

1. 在 [Releases](https://github.com/666666666666gao/ryomc-codex-quota/releases/latest) 下载 `ryomc-quota-windows-v0.3.0.zip`，解压全部文件。
2. 确认 Codex 已配置你的中转站，例如 `https://api.ryomc.top/v1`，并在 `auth.json` 中保存该站创建的 API 密钥。
3. 双击 `RyomcQuota.exe`，将 Codex 切换到前台。

不要只复制 EXE：同目录的 `Tommy.dll` 是 TOML 配置解析依赖。程序未进行代码签名。

悬浮条是独立窗口，**不是 Codex 原生状态栏**。靠近 Codex 右下角显示，每两分钟刷新；可拖动文字调整位置，点 ↻ 刷新、× 关闭。不添加开机启动，不修改 Windows 安全设置。查询失败不会继续显示旧额度。

## 自动读取规则

- 配置目录：当前进程的 `CODEX_HOME`；未设置时使用 `~/.codex`（Windows：`%USERPROFILE%\.codex`）。
- 读取 `config.toml` 中的 `model_provider`、`model`，以及 `[model_providers.<名称>]` 的 `base_url`。
- 如果配置了默认 `profile`，使用该 `[profiles.<名称>]` 中的提供方和模型覆盖。
- 提供方设置了 `env_key` 时，只读取指定环境变量；否则读取同目录 `auth.json` 的 `OPENAI_API_KEY`。
- 每次刷新重新读取这些文件，因此保存配置/切换 CC Switch 提供方后，下一次刷新会跟随。
- 请求发送到 **当前 Base URL 末尾追加 `/quota/weekly`**。例如 `https://api.ryomc.top/v1/quota/weekly`，使用 `Authorization: Bearer <现有 API 密钥>` 与 `X-Quota-Model`。
- 只向配置的同一 HTTPS 站点发送密钥，禁止重定向，不发送 CPA 管理密钥、OAuth 凭据或整个配置文件。

读取的是**已保存的用户级配置**，不是运行中会话的 `-c`/`--profile` 临时覆盖、项目级配置覆盖或内存中的 UI 选择。只使用系统凭据库且 `auth.json` 没有 API 密钥的配置不在自动读取范围内。

配置字段说明：[Codex 官方配置参考](https://developers.openai.com/codex/config-reference/)。

## 什么叫“追溯上游”

```text
本机 Codex 配置 → 该站点额度接口 → 验证 New API 密钥和模型路由 → CPA 唯一上游账号 → 周额度摘要
```

**URL 和普通 API 密钥本身不能揭示任意网站背后的账号。** 中转站管理员必须部署配套接口。Ryomc 站点支持该接口；其他站点没有部署时会显示不支持查询，不会改用 Ryomc 的数据。

当前服务器适配器支持：New API PostgreSQL + 一个指定 CPA 渠道 + 一个启用的 Codex OAuth 账号。它读取当前令牌所属分组和模型的启用路由；不匹配指定 CPA 渠道、存在其他候选渠道、自动分组或跨组重试时拒绝猜测。CPA 账号不唯一时也不显示任意一个账号的额度。它不是任意多层中转/账号池的通用溯源工具。

显示的是**共享上游账号周额度，不是个人 New API 钱包余额，也不是为个人预留的额度**。不返回邮箱、账号 ID、OAuth Token 或渠道密钥。成功读取额度不代表模型请求一定健康。

## Codex 插件 / macOS / Linux 命令行

安装 Node.js 22+，克隆仓库并安装锁定依赖：

```sh
git clone https://github.com/666666666666gao/ryomc-codex-quota.git
cd ryomc-codex-quota
npm ci
node scripts/reader.mjs
```

Windows/macOS/Linux 均可使用命令行查询；悬浮条仅适用于 Windows。

要在 Codex 中使用技能，让 Codex 使用内置 plugin-creator 将这个**已有插件目录**注册到个人市场，不要覆盖源代码。市场注册后运行 `codex plugin add ryomc-codex-quota@personal`（以实际市场名为准），然后新建任务加载技能。安装后的插件目录也需要 `npm ci`，它仅安装锁定的 TOML 解析依赖。

可对 Codex 说：“查看线路周额度”或“启动 Windows 额度悬浮条”。

## 管理员原有模式

已配置专用只读令牌的旧用户，可显式使用：

```sh
node scripts/reader.mjs --shared
```

Windows：`RyomcQuota.exe --shared`。这种模式才读取 `~/.config/ryomc-codex-quota/reader.json`，适合本机 Codex 使用官方 OAuth、但管理员想查看自己中转线路的情况。默认自动模式不会回退到它。旧配置工具 `windows/configure.ps1` 只用于该模式。

原有管理员直接连接 CPA 的网页面板仍可通过 `node scripts/quota.mjs serve` 打开，CLI 使用 `node scripts/quota.mjs query`。它读取单独的管理员配置 `~/.config/ryomc-codex-quota/config.json`，需要 CPA 管理权限；不要分发该配置或面板的临时 URL。

## 常见情况

| 提示 | 含义 |
|---|---|
| 配置或连接不可用 | 未找到有效中转配置/API 密钥、官方 OAuth 登录、网络问题或响应格式异常；不是自动判定额度耗尽 |
| 401 | 密钥无效、禁用或用户不可用 |
| 403 | 此密钥不能查询该模型线路；当前适配器不支持带 IP 白名单的令牌 |
| 404 | 此站没有配套额度接口，或者 Base URL 不正确 |
| 409 | 路由无法唯一对应到指定上游（多个渠道、自动分组、跨组重试等） |
| 503 | 服务器查询路由或上游额度失败，或 CPA 账号不唯一；不能笼统归因于用户网络 |

本工具依赖上游非公开 usage 接口，上游修改后可能需要更新。没有微信机器人、每日推送、余额重置或模型探测请求。

## 构建、测试与服务部署

见 [服务端部署说明](server/DEPLOYMENT.md)。Windows 用 `windows/build.ps1` 构建，首次下载校验过的 Tommy 3.1.2 解析器。Node 使用锁定版本 smol-toml；Windows 包含 Tommy 的 MIT 许可证。

```sh
npm test
python -m unittest discover -s server -v
```

测试使用合成数据，不代表实时额度。真实联调应验证 HTTPS 接口、合法/非法 API 密钥及对应路由。不要将真实配置或密钥放入源码、Issue 或截图。
