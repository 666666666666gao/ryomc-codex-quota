import test from 'node:test';
import assert from 'node:assert/strict';
import {extractWeekly,queryQuota,validateConfig,safeError} from '../scripts/core.mjs';
import {startDashboard} from '../scripts/quota.mjs';

const window={limit_window_seconds:604800,used_percent:5,reset_at:1800000000};
const payload={rate_limit:{primary_window:{limit_window_seconds:18000,used_percent:80},secondary_window:window}};
const config={baseUrl:'https://cpa.example.com',managementKey:'test-only-not-a-real-key'};
test('weekly remaining and timestamp are exact',()=>{
  assert.deepEqual(extractWeekly(payload,0),{remainingPercent:95,resetAt:new Date(1800000000000).toISOString(),queriedAt:'1970-01-01T00:00:00.000Z',windowSeconds:604800});
});
test('weekly in primary is supported by duration, not position',()=>{
  assert.equal(extractWeekly({rate_limit:{primary_window:window}}).remainingPercent,95);
});
test('missing, monthly, review-only and duplicate windows are rejected',()=>{
  for(const p of [{},{rate_limit:{secondary_window:{...window,limit_window_seconds:2592000}}},{code_review_rate_limit:{secondary_window:window}},{rate_limit:{primary_window:window,secondary_window:window}}]) assert.throws(()=>extractWeekly(p));
});
test('missing or invalid percent is not zero',()=>{
  for(const used_percent of [undefined,null,'5',NaN,Infinity,-1,101]) assert.throws(()=>extractWeekly({rate_limit:{secondary_window:{...window,used_percent}}}));
});
test('zero and full remaining preserved',()=>{
  for(const used_percent of [0,100]) assert.equal(extractWeekly({rate_limit:{secondary_window:{...window,used_percent}}}).remainingPercent,100-used_percent);
});
test('relative reset time supported; missing reset is unknown',()=>{
  assert.equal(extractWeekly({rate_limit:{secondary_window:{...window,reset_at:null,reset_after_seconds:60}}},0).resetAt,'1970-01-01T00:01:00.000Z');
  assert.equal(extractWeekly({rate_limit:{secondary_window:{...window,reset_at:null}}}).resetAt,null);
});
test('only secure remote base URLs accepted',()=>{
  for(const baseUrl of ['http://example.com','https://user:secret@example.com','https://example.com/v1','https://example.com?token=x']) assert.throws(()=>validateConfig({...config,baseUrl}));
  assert.equal(validateConfig({...config,baseUrl:'http://127.0.0.1:8317'}).baseUrl,'http://127.0.0.1:8317');
});
test('CPA request uses token placeholder; result excludes private fields',async()=>{
  const calls=[];
  const q=await queryQuota(config,async(url,options)=>{
    calls.push({url,options});
    return Response.json(calls.length===1?{files:[{type:'codex',auth_index:'a',id_token:{chatgpt_account_id:'private-id'},email:'private-email'}]}:{status_code:200,body:JSON.stringify(payload)});
  });
  const sent=JSON.parse(calls[1].options.body);
  assert.equal(sent.method,'GET');assert.equal(sent.url,'https://chatgpt.com/backend-api/wham/usage');
  assert.equal(sent.header.Authorization,'Bearer $TOKEN$');assert.equal(sent.header['Chatgpt-Account-Id'],'private-id');
  assert.equal(q.remainingPercent,95);assert.doesNotMatch(JSON.stringify(q),/private|key|auth/);
});
test('multiple accounts require explicit selection',async()=>{
  await assert.rejects(queryQuota(config,async()=>Response.json({files:[{type:'codex',auth_index:'a'},{type:'codex',auth_index:'b'}]})),/唯一/);
});
test('management and upstream failures never disclose raw bodies',async()=>{
  await assert.rejects(queryQuota(config,async()=>new Response('secret-token',{status:401})),e=>!e.message.includes('secret-token')&&e.message.includes('401'));
  let n=0;
  await assert.rejects(queryQuota(config,async()=>Response.json(++n===1?{files:[{type:'codex',auth_index:'a'}]}:{status_code:429,body:'secret-token'})),e=>e.message.includes('429')&&!e.message.includes('secret-token'));
  assert.doesNotMatch(safeError(new Error('secret-token')),/secret-token/);
});
test('dashboard requires token, rejects cross-origin, and returns sanitized quota',async()=>{
  const {server,url}=await startDashboard({query:async()=>extractWeekly(payload),save:async()=>{}});
  try {
    const u=new URL(url),origin=u.origin,headers={'X-Quota-Token':u.hash.slice(1)};
    assert.equal((await fetch(origin+'/api/quota',{method:'POST'})).status,403);
    assert.equal((await fetch(origin+'/api/quota',{method:'POST',headers:{...headers,Origin:'https://evil.example'}})).status,403);
    const r=await fetch(origin+'/api/quota',{method:'POST',headers});
    assert.equal(r.status,200);assert.equal((await r.json()).remainingPercent,95);
    const bad=await fetch(origin+'/api/config',{method:'POST',headers,body:'{'});
    assert.equal(bad.status,400);
    assert.equal((await fetch(origin+'/api/config',{method:'GET',headers})).status,404);
    const page=await fetch(origin);assert.equal(page.status,200);assert.equal(page.headers.get('cache-control'),'no-store');
  } finally {await new Promise(resolve=>server.close(resolve));}
});
