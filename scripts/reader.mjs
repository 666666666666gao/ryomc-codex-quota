import {readFile} from 'node:fs/promises';
import {homedir} from 'node:os';
import {join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {readCodexConnection} from './codex-config.mjs';
export async function readSharedQuota({shared=false,connection,fetchImpl=fetch}={}) {
  let c=connection;
  if(!c && shared) {
    const legacy=JSON.parse(await readFile(join(homedir(),'.config','ryomc-codex-quota','reader.json'),'utf8'));
    c={endpoint:legacy.endpoint,apiKey:legacy.readToken};
  }
  if(!c) c=await readCodexConnection();
  const url=new URL(c.endpoint);
  if(url.protocol!=='https:' || url.username || url.password || url.search || url.hash || !c.apiKey) throw Error('Invalid reader configuration');
  const headers={Authorization:'Bearer '+c.apiKey};
  if(c.model) headers['X-Quota-Model']=c.model;
  const response=await fetchImpl(url,{headers,redirect:'error',signal:AbortSignal.timeout(35000)});
  if(!response.ok) throw Error(({401:'API 密钥无效或已禁用',403:'此密钥不允许查询该模型线路',404:'该中转站未提供额度接口',409:'无法唯一确认上游账号',503:'上游额度查询暂不可用'})[response.status] || 'Query HTTP '+response.status);
  const q=await response.json();
  if(q.windowSeconds!==604800 || typeof q.remainingPercent!=='number' || !Number.isFinite(q.remainingPercent) || q.remainingPercent<0 || q.remainingPercent>100 || !Number.isFinite(Date.parse(q.queriedAt)) || (q.resetAt!==null && !Number.isFinite(Date.parse(q.resetAt)))) throw Error('Invalid quota data');
  return {remainingPercent:q.remainingPercent,resetAt:q.resetAt,queriedAt:q.queriedAt,windowSeconds:604800};
}
if(process.argv[1] && import.meta.url===pathToFileURL(process.argv[1]).href) {
  try {console.log(JSON.stringify(await readSharedQuota({shared:process.argv.includes('--shared')}),null,2));}
  catch {console.error('额度查询失败：请检查当前 Codex 中转配置、API 密钥及站点是否支持 /v1/quota/weekly。官方 OAuth 登录不能自动追溯中转上游。');process.exitCode=1;}
}
