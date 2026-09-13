import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,writeFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {resolveProvider,readCodexConnection} from '../scripts/codex-config.mjs';
import {readSharedQuota} from '../scripts/reader.mjs';

const toml=`model_provider = "custom"
model = "gpt-6-astra"
[model_providers.custom]
base_url = 'https://example.test/v1' # comment
`;
test('saved provider, quoted tables, profile and endpoint prefix',()=>{
  assert.equal(resolveProvider(toml).endpoint,'https://example.test/v1/quota/weekly');
  const c=resolveProvider('profile="work"\n'+toml+'\n[profiles.work]\nmodel="gpt-5.6-sol"');
  assert.equal(c.model,'gpt-5.6-sol');
  assert.equal(resolveProvider(toml.replace('[model_providers.custom]','[model_providers."custom"]')).model,'gpt-6-astra');
  assert.equal(resolveProvider(toml.replace('/v1','/tenant/v1/')).endpoint,'https://example.test/tenant/v1/quota/weekly');
});
test('official OAuth mode and unsafe URLs do not get a default endpoint',()=>{
  assert.throws(()=>resolveProvider('model="gpt-6-astra"'));
  for(const url of ['http://example.test/v1','https://user:pass@example.test/v1','https://example.test/v1?key=secret','https://example.test/v1#fragment'])
    assert.throws(()=>resolveProvider(toml.replace('https://example.test/v1',url)));
});
test('reads current CODEX_HOME auth key and env_key without changing files',async()=>{
  const root=await mkdtemp(join(tmpdir(),'quota-config-'));
  try {
    await writeFile(join(root,'config.toml'),toml);
    await writeFile(join(root,'auth.json'),JSON.stringify({OPENAI_API_KEY:'fixture-api-key',tokens:{access_token:'never-use-oauth'}}));
    assert.equal((await readCodexConnection({env:{CODEX_HOME:root}})).apiKey,'fixture-api-key');
    await writeFile(join(root,'config.toml'),toml+'env_key="MY_KEY"');
    await assert.rejects(readCodexConnection({env:{CODEX_HOME:root}}));
    assert.equal((await readCodexConnection({env:{CODEX_HOME:root,MY_KEY:'environment-key'}})).apiKey,'environment-key');
    await writeFile(join(root,'config.toml'),toml);
    await writeFile(join(root,'auth.json'),JSON.stringify({tokens:{access_token:'never-use-oauth'}}));
    await assert.rejects(readCodexConnection({env:{CODEX_HOME:root}}));
  } finally {await rm(root,{recursive:true,force:true});}
});
test('sends API key only to configured same-origin endpoint, sanitized data only',async()=>{
  const result=await readSharedQuota({connection:{endpoint:'https://example.test/v1/quota/weekly',apiKey:'fixture-api-key',model:'gpt-6-astra'},
    fetchImpl:async(url,opts)=>{
      assert.equal(url.origin,'https://example.test');
      assert.equal(opts.headers.Authorization,'Bearer fixture-api-key');
      assert.equal(opts.headers['X-Quota-Model'],'gpt-6-astra');
      assert.equal(opts.redirect,'error');
      return Response.json({remainingPercent:93,resetAt:null,queriedAt:'2026-09-13T12:00:00Z',windowSeconds:604800,accountEmail:'private@example.test'});
    }});
  assert.deepEqual(Object.keys(result).sort(),['queriedAt','remainingPercent','resetAt','windowSeconds']);
});
test('unsupported station error never exposes raw response',async()=>{
  await assert.rejects(readSharedQuota({connection:{endpoint:'https://example.test/v1/quota/weekly',apiKey:'fixture'},fetchImpl:async()=>new Response('private raw data',{status:404})}),/未提供额度接口/);
});
