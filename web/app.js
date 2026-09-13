const token=location.hash.slice(1);
const $=id=>document.getElementById(id);
const format=value=>value?new Intl.DateTimeFormat('zh-CN',{timeZone:'Asia/Shanghai',year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',second:'2-digit',hour12:false}).format(new Date(value)):'未知';
async function api(path,body) {
  const r=await fetch(path,{method:'POST',headers:{'Content-Type':'application/json','X-Quota-Token':token},body:JSON.stringify(body||{})});
  const data=await r.json();
  if(!r.ok) throw new Error(data.error||'查询失败');
  return data;
}
async function refresh() {
  $('refresh').disabled=true;$('error').textContent='';$('status').textContent='正在查询上游周额度…';
  $('remaining').textContent='—';$('bar').value=0;$('reset').textContent='—';$('time').textContent='—';
  try {const q=await api('/api/quota');$('remaining').textContent=q.remainingPercent;$('bar').value=q.remainingPercent;$('reset').textContent=format(q.resetAt);$('time').textContent=format(q.queriedAt);$('status').textContent='已读取真实上游额度 · 手动刷新快照';}
  catch(e){$('status').textContent='本次查询未成功，未显示旧值';$('error').textContent=e.message;if(e.message.includes('尚未配置')) $('settings').open=true;}
  finally{$('refresh').disabled=false;}
}
$('refresh').addEventListener('click',refresh);
$('config').addEventListener('submit',async e=>{
  e.preventDefault();const button=e.submitter;button.disabled=true;$('error').textContent='';
  try {await api('/api/config',Object.fromEntries(new FormData(e.target)));e.target.elements.managementKey.value='';$('settings').open=false;await refresh();}
  catch(e){$('error').textContent=e.message;}
  finally{button.disabled=false;}
});
refresh();
