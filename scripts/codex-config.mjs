import {readFile} from 'node:fs/promises';
import {homedir} from 'node:os';
import {join} from 'node:path';
import {parse} from 'smol-toml';

// Resolve saved user configuration, not a running thread's command-line overrides.
export function resolveProvider(toml) {
  const config=parse(toml);
  const profile=config.profile ? config.profiles?.[config.profile] : {};
  if(config.profile && !profile) throw Error('配置中的 profile 不存在');
  const name=profile?.model_provider ?? config.model_provider;
  const model=profile?.model ?? config.model;
  const provider=config.model_providers?.[name];
  if(!provider?.base_url) throw Error('当前配置不是带 Base URL 的中转 API 模式');
  const url=new URL(provider.base_url);
  if(url.protocol!=='https:' || url.username || url.password || url.search || url.hash) throw Error('Base URL 必须是无凭据、无查询参数的 HTTPS 地址');
  if(typeof model!=='string' || !model) throw Error('当前配置没有模型名称');
  const endpoint=url.href.replace(/\/$/,'')+'/quota/weekly';
  return {endpoint,model,envKey:provider.env_key};
}

export async function readCodexConnection({env=process.env,home=homedir()}={}) {
  const root=env.CODEX_HOME || join(home,'.codex');
  const resolved=resolveProvider(await readFile(join(root,'config.toml'),'utf8'));
  // An explicit env_key is authoritative; never use another account if it is absent.
  const apiKey=resolved.envKey ? env[resolved.envKey] : JSON.parse(await readFile(join(root,'auth.json'),'utf8')).OPENAI_API_KEY;
  if(typeof apiKey!=='string' || !apiKey.trim()) throw Error('没有找到当前提供方的 API 密钥；不使用 ChatGPT OAuth 凭据');
  return {endpoint:resolved.endpoint,model:resolved.model,apiKey:apiKey.trim()};
}
