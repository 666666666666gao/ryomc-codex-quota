import {createServer} from 'node:http';
import {readFile, mkdir, writeFile} from 'node:fs/promises';
import {dirname} from 'node:path';
import {randomBytes, timingSafeEqual} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {execFileSync} from 'node:child_process';
import {configPath, loadConfig, queryQuota, validateConfig, safeError, QuotaError} from './core.mjs';

export async function startDashboard({port=0, query=async()=>queryQuota(await loadConfig()), save=saveConfig}={}) {
  const token = randomBytes(24).toString('hex');
  const html = await readFile(new URL('../web/index.html', import.meta.url));
  const js = await readFile(new URL('../web/app.js', import.meta.url));
  let origin;
  let pending;
  const server = createServer(async(req,res)=>{
    res.setHeader('Cache-Control','no-store');
    res.setHeader('Referrer-Policy','no-referrer');
    res.setHeader('X-Content-Type-Options','nosniff');
    res.setHeader('Content-Security-Policy', "default-src 'none'; script-src 'self'; style-src 'unsafe-inline'; connect-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'");
    const reply=(status,body)=>{res.writeHead(status,{'Content-Type':'application/json; charset=utf-8'});res.end(JSON.stringify(body));};
    if (req.headers.host !== new URL(origin).host || (req.headers.origin && req.headers.origin !== origin)) return reply(403,{error:'禁止跨站访问'});
    const url = new URL(req.url,origin);
    if(req.method==='GET' && (url.pathname==='/' || url.pathname==='/app.js')) {res.setHeader('Content-Type',url.pathname==='/'?'text/html; charset=utf-8':'text/javascript; charset=utf-8');return res.end(url.pathname==='/'?html:js);}
    const input=Buffer.from(req.headers['x-quota-token']||'');
    const expected=Buffer.from(token);
    if(input.length!==expected.length || !timingSafeEqual(input,expected)) return reply(403,{error:'页面访问凭据失效，请从插件重新打开'});
    try {
      if(req.method==='POST' && url.pathname==='/api/quota') {
        if(!pending) pending=query().finally(()=>{pending=null;});
        return reply(200,await pending);
      }
      if(req.method==='POST' && url.pathname==='/api/config') {
        let body='';
        for await(const chunk of req) { body+=chunk; if(Buffer.byteLength(body)>16384) return reply(413,{error:'配置过大'}); }
        let c;
        try { c=JSON.parse(body); } catch { throw new QuotaError('配置不是有效 JSON'); }
        await save(validateConfig(c));
        return reply(200,{saved:true});
      }
      return reply(404,{error:'接口不存在'});
    } catch(e) { return reply(400,{error:safeError(e)}); }
  });
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(port,'127.0.0.1',resolve);});
  origin='http://127.0.0.1:'+server.address().port;
  return {server,url:origin+'/#'+token};
}
async function saveConfig(config) {
  const dir=dirname(configPath());
  await mkdir(dir,{recursive:true,mode:0o700});
  if(process.platform==='win32') {
    const user=execFileSync('whoami',[],{encoding:'utf8',windowsHide:true}).trim();
    execFileSync('icacls',[dir,'/inheritance:r','/grant:r',user+':(OI)(CI)F'],{stdio:'pipe',windowsHide:true});
  }
  await writeFile(configPath(),JSON.stringify(config,null,2),{mode:0o600});
}
if(process.argv[1] && fileURLToPath(import.meta.url)===process.argv[1]) {
  try {
    if(process.argv[2]==='query') console.log(JSON.stringify(await queryQuota(await loadConfig()),null,2));
    else if(process.argv[2]==='serve') {const {url}=await startDashboard();console.log(url);}
    else {console.error('Usage: node scripts/quota.mjs query|serve');process.exitCode=2;}
  } catch(e) {console.error(safeError(e));process.exitCode=1;}
}
