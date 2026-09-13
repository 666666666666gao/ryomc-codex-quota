import {readFile} from 'node:fs/promises';
import {homedir} from 'node:os';
import {join} from 'node:path';
export async function readSharedQuota() {
  const c=JSON.parse(await readFile(join(homedir(),'.config','ryomc-codex-quota','reader.json'),'utf8'));
  const url=new URL(c.endpoint);
  if(url.protocol!=='https:' || url.username || url.password || url.search || url.hash || !c.readToken) throw Error('Invalid reader configuration');
  const response=await fetch(url,{headers:{Authorization:'Bearer '+c.readToken},redirect:'error',signal:AbortSignal.timeout(35000)});
  if(!response.ok) throw Error('Query HTTP '+response.status);
  const q=await response.json();
  if(q.windowSeconds!==604800 || typeof q.remainingPercent!=='number' || !Number.isFinite(q.remainingPercent) || q.remainingPercent<0 || q.remainingPercent>100 || !Number.isFinite(Date.parse(q.queriedAt)) || (q.resetAt!==null && !Number.isFinite(Date.parse(q.resetAt)))) throw Error('Invalid quota data');
  return {remainingPercent:q.remainingPercent,resetAt:q.resetAt,queriedAt:q.queriedAt,windowSeconds:604800};
}
try {console.log(JSON.stringify(await readSharedQuota(),null,2));}
catch {console.error('只读额度查询失败：请检查 reader.json 中的 HTTPS 地址、只读令牌及服务状态。');process.exitCode=1;}
