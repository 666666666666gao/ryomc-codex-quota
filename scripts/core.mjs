import {readFile} from 'node:fs/promises';
import {homedir} from 'node:os';
import {join} from 'node:path';

export const configPath = () => join(homedir(), '.config', 'ryomc-codex-quota', 'config.json');
export class QuotaError extends Error {}
export function validateConfig(c) {
  let u;
  try { u = new URL(c.baseUrl); } catch { throw new QuotaError('请填写有效的 CPA 管理地址'); }
  if (u.username || u.password || u.search || u.hash || (u.pathname !== '/' && u.pathname !== '')) throw new QuotaError('管理地址只包含协议、主机和端口，不含路径或凭据');
  if (u.protocol !== 'https:' && !(u.protocol === 'http:' && ['localhost','127.0.0.1','[::1]'].includes(u.hostname))) throw new QuotaError('远程 CPA 必须使用 HTTPS；HTTP 只允许本机');
  if (typeof c.managementKey !== 'string' || !c.managementKey.trim()) throw new QuotaError('需要 CPA 管理密钥，不是模型 API 密钥');
  return {baseUrl:u.origin, managementKey:c.managementKey.trim(), authIndex:String(c.authIndex || '').trim(), accountId:String(c.accountId || '').trim()};
}
export async function loadConfig() {
  let text;
  try { text = await readFile(configPath(), 'utf8'); }
  catch { throw new QuotaError('尚未配置：请打开额度面板，在本机填写 CPA 管理地址和管理密钥'); }
  try { return validateConfig(JSON.parse(text)); }
  catch (e) { if (e instanceof QuotaError) throw e; throw new QuotaError('本机配置不是有效 JSON'); }
}
export function extractWeekly(payload, now = Date.now()) {
  const r = payload?.rate_limit;
  const windows = [r?.primary_window, r?.secondary_window].filter(w=>w?.limit_window_seconds === 604800);
  if (windows.length !== 1) throw new QuotaError('上游未返回唯一的 7 天周额度窗口，未使用月额度或代码审查额度代替');
  const w = windows[0];
  const n = w.used_percent;
  if (typeof n !== 'number' || !Number.isFinite(n) || n < 0 || n > 100) throw new QuotaError('上游周额度使用比例缺失或无效');
  let reset = null;
  if (typeof w.reset_at === 'number' && Number.isFinite(w.reset_at) && w.reset_at > 0) reset = w.reset_at * 1000;
  else if (typeof w.reset_after_seconds === 'number' && Number.isFinite(w.reset_after_seconds) && w.reset_after_seconds >= 0) reset = now + w.reset_after_seconds * 1000;
  if (reset !== null && !Number.isFinite(new Date(reset).getTime())) throw new QuotaError('上游重置时间无效');
  return {remainingPercent:Math.round((100-n)*100)/100, resetAt:reset === null ? null : new Date(reset).toISOString(), queriedAt:new Date(now).toISOString(), windowSeconds:604800};
}
export async function queryQuota(config, request = fetch) {
  const c = validateConfig(config);
  const call = async (path, body) => {
    let res;
    try { res = await request(c.baseUrl + '/v0/management/' + path, {method:body ? 'POST' : 'GET', redirect:'error', headers:{Authorization:'Bearer '+c.managementKey, 'Content-Type':'application/json'}, ...(body ? {body:JSON.stringify(body)} : {}), signal:AbortSignal.timeout(25000)}); }
    catch { throw new QuotaError('无法连接 CPA 管理接口：请检查管理地址、网络和证书'); }
    if (!res.ok) throw new QuotaError('CPA 管理接口 HTTP '+res.status+'；401/403 请检查管理密钥和管理访问权限');
    try { return await res.json(); } catch { throw new QuotaError('CPA 管理接口没有返回 JSON'); }
  };
  const roster = await call('auth-files');
  const files = (Array.isArray(roster.files) ? roster.files : []).filter(f=>f.type === 'codex' || f.provider === 'codex');
  const selected = c.authIndex ? files.filter(f=>String(f.auth_index) === c.authIndex) : files;
  if (selected.length !== 1) throw new QuotaError('需要唯一 Codex 账号：请在设置中填写目标账号的 auth_index');
  const account = selected[0];
  if (account.disabled) throw new QuotaError('目标 CPA 账号已禁用；未查询额度');
  if (!account.auth_index) throw new QuotaError('CPA 未返回账号 auth_index');
  const headers = {Authorization:'Bearer $TOKEN$', 'Content-Type':'application/json', 'User-Agent':'codex-tui/0.149.1'};
  const aid = c.accountId || account.id_token?.chatgpt_account_id;
  if (aid) headers['Chatgpt-Account-Id'] = aid;
  const result = await call('api-call', {authIndex:String(account.auth_index), method:'GET', url:'https://chatgpt.com/backend-api/wham/usage', header:headers});
  if (result.status_code !== 200) throw new QuotaError('上游额度查询 HTTP '+(Number(result.status_code)||'未知')+'；这不等同于余额为零或账号被封');
  let payload = result.body;
  if (typeof payload === 'string') {
    try { payload = JSON.parse(payload); } catch { throw new QuotaError('上游额度响应不是 JSON'); }
  }
  return extractWeekly(payload);
}
export function safeError(e) { return e instanceof QuotaError ? e.message : '操作失败，请检查本机配置和服务状态（未输出原始错误以保护凭据）'; }
