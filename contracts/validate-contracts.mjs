// Contract checker: intentionally a limited OpenAPI/JSON Schema subset.
// No network, dependencies, writes, or claims of full OpenAPI certification.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const names = ['identity','statistics','questionbank','lobby','gameplay','notification'];
const expected = {
 identity: ['get /api/users/me','put /api/users/me','get /api/users/me/picture','put /api/users/me/picture','post /api/auth/register','post /api/auth/login','get /api/users/{id}','get /health'],
 statistics: ['post /api/results','get /api/players/{userId}/stats','get /api/players/{userId}/matches','get /api/leaderboard','get /health'],
 questionbank: ['get /api/questions/generate','post /internal/question-sets','get /health'],
 lobby: ['post /api/lobbies','get /api/lobbies/{joinCode}','post /api/lobbies/{joinCode}/join','post /api/lobbies/{joinCode}/lock','get /internal/lobbies/{joinCode}','get /health'],
 gameplay: ['post /matches','get /matches/{matchId}','get /matches/{matchId}/next-question','post /matches/{matchId}/answers','get /health'],
 notification: ['post /events','get /events','get /users/{userId}/preferences','put /users/{userId}/preferences','get /health']
};
// Independent normative expectations: changing a document's metadata together
// must not turn a protected route public or replace its required response body.
// Tuple: actor, exact policy, request schema (null = none), outcome schemas.
// 'image' means raw binary image media (IMAGE_MEDIA) instead of a JSON schema.
const routeContracts = {
 identity: {
  'get /api/users/me': ['user','profiles.read',null,{200:'Profile'}],
  'put /api/users/me': ['user','profiles.write','UpdateProfile',{200:'Profile'}],
  'get /api/users/me/picture': ['user','profiles.read',null,{200:'image'}],
  'put /api/users/me/picture': ['user','profiles.write','image',{200:'Profile'}],
  'post /api/auth/register': ['public','public',null,{410:'Problem'}],
  'post /api/auth/login': ['public','public',null,{410:'Problem'}],
  'get /api/users/{id}': ['public','public',null,{410:'Problem'}]
 },
 statistics: {
  'post /api/results': ['machine','results.write','RecordResult',{200:null}],
  'get /api/players/{userId}/stats': ['user','stats.read',null,{200:'Stats'}],
  'get /api/players/{userId}/matches': ['user','stats.read',null,{200:'History[]'}],
  'get /api/leaderboard': ['user','leaderboard.read',null,{200:'Leader[]'}]
 },
 questionbank: {
  'get /api/questions/generate': ['user','questions.read',null,{200:'Question[]'}],
  'post /internal/question-sets': ['machine','questions.play','QuestionSetRequest',{200:'QuestionSnapshot[]'}]
 },
 lobby: {
  'post /api/lobbies': ['user','lobbies.write','EmptyRequest',{201:'Lobby'}],
  'get /api/lobbies/{joinCode}': ['user','lobbies.read',null,{200:'Lobby'}],
  'post /api/lobbies/{joinCode}/join': ['user','lobbies.write','EmptyRequest',{200:'Lobby'}],
  'post /api/lobbies/{joinCode}/lock': ['user','lobbies.write',null,{200:'Lobby'}],
  'get /internal/lobbies/{joinCode}': ['machine','lobbies.verify',null,{200:'Provision'}]
 },
 gameplay: {
  'post /matches': ['user','matches.write','CreateMatch',{200:'Match',201:'Match'}],
  'get /matches/{matchId}': ['user','matches.read',null,{200:'Match'}],
  'get /matches/{matchId}/next-question': ['user','matches.read',null,{200:'CurrentQuestion'}],
  'post /matches/{matchId}/answers': ['user','matches.write','SubmitAnswer',{200:'AnswerOutcome'}]
 },
 notification: {
  'post /events': ['machine','events.write','CreateEvent',{202:'Event'}],
  'get /events': ['user','events.read',null,{200:'Event[]'}],
  'get /users/{userId}/preferences': ['user','preferences.read',null,{200:'Preferences'}],
  'put /users/{userId}/preferences': ['user','preferences.write','UpdatePreferences',{200:'Preferences'}]
 }
};
// Accepted picture media (SEC-02). Exact set: adding SVG, GIF or anything else fails.
const IMAGE_MEDIA = ['image/png','image/jpeg','image/webp'];
// Operations that are documented but not implemented yet. Only these may carry
// x-status: proposed, and each must name the issue that implements it.
const proposedOps = {
 identity: {'get /api/users/me/picture':'#144','put /api/users/me/picture':'#144'}
};
// Error outcomes a route must declare beyond the shared 401/403 rule.
const requiredOutcomes = {
 identity: {'get /api/users/me/picture':[404,503],'put /api/users/me/picture':[400,404,409,413,415,503]}
};
// Response headers a route must declare, with their exact example value (SEC-08).
const requiredHeaders = {
 identity: {'get /api/users/me/picture':{200:{'Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff'}}}
};
function matchesSchema(s,name) {
 if(name.endsWith('[]'))return s?.type==='array'&&s.items?.$ref==='#/components/schemas/'+name.slice(0,-2);
 return s?.$ref==='#/components/schemas/'+name;
}
const allowed = new Set(['$ref','type','format','nullable','enum','required','additionalProperties','properties','items','minItems','maxItems','uniqueItems','minimum','maximum','minLength','maxLength','pattern','default','description','x-status','x-implemented-by']);
const has = (o,k) => Object.hasOwn(o,k);
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const fail = (ok,message) => { if (!ok) throw new Error(message); };
// Raw image content: exactly IMAGE_MEDIA, each a binary string with an example.
// Kept apart from schema() so format: binary never becomes valid inside JSON schemas.
function imageContent(content,label) {
 fail(content&&typeof content==='object','Missing image content '+label);
 assert.deepEqual(Object.keys(content).sort(),[...IMAGE_MEDIA].sort(),'Exact image media '+label);
 for(const [m,media]of Object.entries(content)){
  fail(Object.keys(media.schema||{}).length===2&&media.schema.type==='string'&&media.schema.format==='binary','Image media must be a binary string '+label+' '+m);
  fail(has(media,'example')&&typeof media.example==='string','Missing image example '+label+' '+m);
 }
 return IMAGE_MEDIA.length;
}
// x-status/x-implemented-by: returns true for a correctly marked proposed item.
function marker(o,label) {
 if(has(o,'x-implemented-by'))fail(has(o,'x-status'),'x-implemented-by without x-status '+label);
 if(!has(o,'x-status'))return false;
 fail(o['x-status']==='proposed','Unsupported x-status '+label);
 fail(typeof o['x-implemented-by']==='string'&&/^#\d+$/.test(o['x-implemented-by']),'Proposed item needs x-implemented-by issue '+label);
 return true;
}
function resolve(d,s) {
 if (!s.$ref) return s;
 fail(/^#\/components\/schemas\/[^/]+$/.test(s.$ref),'Only local schema refs supported: '+s.$ref);
 const target=d.components.schemas[s.$ref.split('/').at(-1)];
 fail(target,'Dangling ref '+s.$ref);
 return target;
}
function schema(d,s,seen=new Set()) {
 fail(s && typeof s==='object','Missing schema');
 for(const k of Object.keys(s)) fail(allowed.has(k),'Unsupported schema keyword '+k);
 if(s.$ref) { fail(Object.keys(s).length===1,'Unsupported ref siblings'); const r=resolve(d,s); if(!seen.has(r)){seen.add(r);schema(d,r,seen);} return; }
 fail(['object','array','string','integer','number','boolean'].includes(s.type),'Unsupported/missing type');
 if(s.format) fail(['uuid','date-time','uri'].includes(s.format),'Unsupported format');
 if(s.type==='object') {
  fail(s.properties && s.additionalProperties===false,'Object must explicitly prohibit extra fields');
  for(const n of s.required||[])fail(has(s.properties,n),'Required property not declared '+n);
  // A proposed property stays optional until its implementing issue removes the marker.
  for(const [n,p]of Object.entries(s.properties))if(marker(p,'property '+n))fail(!(s.required||[]).includes(n),'Proposed property must not be required '+n);
  for(const p of Object.values(s.properties))schema(d,p,seen);
 }
 if(s.type==='array')schema(d,s.items,seen);
 if(has(s,'default')) value(d,s,s.default,'default');
}
function value(d,s,v,label) {
 s=resolve(d,s);
 if(v===null){fail(s.nullable===true,label+': null forbidden');return;}
 if(s.enum)fail(s.enum.includes(v),label+': enum');
 if(s.type==='object'){
  fail(typeof v==='object'&&!Array.isArray(v),label+': object expected');
  for(const n of s.required||[])fail(has(v,n),label+': missing '+n);
  for(const [n,x] of Object.entries(v)){fail(has(s.properties,n),label+': unexpected '+n);value(d,s.properties[n],x,label+'.'+n);}
 }else if(s.type==='array'){
  fail(Array.isArray(v),label+': array expected');
  if(has(s,'minItems'))fail(v.length>=s.minItems,label+': minItems');
  if(has(s,'maxItems'))fail(v.length<=s.maxItems,label+': maxItems');
  if(s.uniqueItems)fail(new Set(v.map(x=>JSON.stringify(x))).size===v.length,label+': uniqueItems');
  v.forEach((x,i)=>value(d,s.items,x,label+'['+i+']'));
 }else if(s.type==='string'){
  fail(typeof v==='string',label+': string expected');
  if(has(s,'minLength'))fail(v.length>=s.minLength,label+': minLength');
  if(has(s,'maxLength'))fail(v.length<=s.maxLength,label+': maxLength');
  if(s.pattern)fail(new RegExp(s.pattern).test(v),label+': pattern');
  if(s.format==='uuid')fail(uuid.test(v),label+': uuid');
  if(s.format==='date-time')fail(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$/.test(v)&&!Number.isNaN(Date.parse(v)),label+': UTC date-time');
  if(s.format==='uri'){try{new URL(v);}catch{throw new Error(label+': URI');}}
 }else if(s.type==='boolean')fail(typeof v==='boolean',label+': boolean');
 else{
  fail(typeof v==='number'&&Number.isFinite(v),label+': finite number');
  if(s.type==='integer')fail(Number.isInteger(v),label+': integer');
  if(has(s,'minimum'))fail(v>=s.minimum,label+': minimum');
  if(has(s,'maximum'))fail(v<=s.maximum,label+': maximum');
 }
}
function publicSafe(d,s,seen=new Set()){
 s=resolve(d,s);if(seen.has(s))return;seen.add(s);
 for(const [key,p]of Object.entries(s.properties||{})){
  fail(!/correctanswer|answerkey|password|hash|secret/i.test(key),'Public sensitive field '+key);
  publicSafe(d,p,seen);
 }
 if(s.items)publicSafe(d,s.items,seen);
}
function validate(all){
 let operations=0,examples=0;const ids=new Set();const perService={};
 for(const name of names){
  const d=all[name];fail(d?.openapi==='3.0.3','Missing document/version '+name);
  fail(d.info?.version&&d.servers?.length,'Missing info/server '+name);
  const flows=d.components.securitySchemes.oauth2.flows;
  for(const [flow,actor]of [['authorizationCode','user'],['clientCredentials','machine']]){
   const expectedScopes=[...new Set(Object.values(routeContracts[name]).filter(c=>c[0]===actor).map(c=>c[1]))].sort();
   assert.deepEqual(Object.keys(flows[flow]?.scopes||{}).sort(),expectedScopes,'Exact '+flow+' scope separation for '+name);
  }
  for(const s of Object.values(d.components.schemas))schema(d,s);
  const actual=[];
  for(const [path,methods]of Object.entries(d.paths))for(const [method,o]of Object.entries(methods)){
   actual.push(method+' '+path);operations++;
   const contract = path==='/health'&&method==='get'
    ? ['public','public',null,{200:'Health'}] : routeContracts[name]?.[method+' '+path];
   fail(contract,'Unexpected route contract '+name+' '+method+' '+path);
   const [actor,policy,request,outcomes] = contract;
   const key=method+' '+path;
   fail(o['x-actor']===actor&&o['x-policy']===policy,'Normative actor/policy mismatch '+name+' '+key);
   // Only listed operations may be proposed; a listed one must stay marked until its issue lands.
   const proposedBy=proposedOps[name]?.[key];
   fail(marker(o,'operation '+name+' '+key)===Boolean(proposedBy),'Proposed marker mismatch '+name+' '+key);
   if(proposedBy)fail(o['x-implemented-by']===proposedBy,'Proposed operation must name '+proposedBy+' '+key);
   for(const code of requiredOutcomes[name]?.[key]||[])fail(o.responses?.[code],'Missing required outcome '+code+' '+key);
   for(const [code,headers]of Object.entries(requiredHeaders[name]?.[key]||{}))for(const [h,v]of Object.entries(headers)){
    const header=o.responses?.[code]?.headers?.[h];
    fail(header?.schema?.type==='string'&&header.example===v,'Missing required header '+h+': '+v+' on '+code+' '+key);
   }
   if(request==='image'){
    fail(o.requestBody?.required===true,'Missing/incorrect normative request schema');imageContent(o.requestBody.content,key+':request');
   }else if(request){
    fail(o.requestBody?.required===true&&matchesSchema(o.requestBody.content?.['application/json']?.schema,request),'Missing/incorrect normative request schema');
   }else fail(!o.requestBody,'Unexpected request body');
   for(const [code,body]of Object.entries(outcomes)){
    const response=o.responses?.[code];fail(response,'Missing normative outcome '+code);
    if(body===null)fail(!response.content,'Expected empty response body');
    else if(body==='image')imageContent(response.content,key+':'+code);
    else fail(matchesSchema(response.content?.[Number(code)>=400?'application/problem+json':'application/json']?.schema,body),'Missing/incorrect normative response schema '+code);
   }
   for(const code of Object.keys(o.responses||{}))if(Number(code)<400)fail(has(outcomes,code),'Unexpected successful outcome '+code);
   fail(o.operationId&&!ids.has(o.operationId),'Missing/duplicate operationId');ids.add(o.operationId);
   fail(['public','user','machine'].includes(o['x-actor']),'Missing actor');
   fail(Array.isArray(o.security),'Missing security');
   if(o['x-actor']==='public'){fail(o.security.length===0&&o['x-policy']==='public','Public security mismatch');}
   else{
    const scope=o['x-policy'];
    fail(o.security.length===1&&o.security[0].oauth2?.length===1&&o.security[0].oauth2[0]===scope,'Scope/security mismatch');
    fail(o['x-audience']==='quizarena-'+name,'Audience mismatch');
    const flow=o['x-actor']==='machine'?'clientCredentials':'authorizationCode';
    fail(has(d.components.securitySchemes.oauth2.flows[flow].scopes,scope),'Undeclared scope');
    fail(o.responses[401]&&o.responses[403],'Protected route lacks auth errors');
   }
   const params=o.parameters||[];const pathNames=[...path.matchAll(/\{([^}]+)\}/g)].map(x=>x[1]);
   fail(new Set(params.map(p=>p.in+':'+p.name)).size===params.length,'Duplicate parameter');
   for(const n of pathNames)fail(params.some(p=>p.in==='path'&&p.name===n&&p.required===true),'Undeclared path parameter '+n);
   for(const p of params){fail(p.in!=='path'||pathNames.includes(p.name),'Extra path parameter');schema(d,p.schema);if(has(p,'example')){value(d,p.schema,p.example,o.operationId+'.'+p.name);examples++;}}
   fail(Object.keys(o.responses).some(c=>/^2\d\d$/.test(c)||c==='410'),'No outcome response');
   for(const [code,r]of Object.entries(o.responses)){
    fail(r.description,'Missing response description');
    if(Number(code)>=400)fail(r.content?.['application/problem+json'],'Missing error schema');
    if(outcomes[code]==='image'){examples+=imageContent(r.content,key+':'+code);continue;}
    for(const media of Object.values(r.content||{})){
     schema(d,media.schema);fail(has(media,'example'),'Missing response example');value(d,media.schema,media.example,o.operationId+':'+code);examples++;
     if(o['x-actor']!=='machine')publicSafe(d,media.schema);
    }
   }
   if(o.requestBody&&request==='image')examples+=imageContent(o.requestBody.content,key+':request');
   else if(o.requestBody){
    const media=o.requestBody.content?.['application/json'];fail(media,'Missing JSON request');schema(d,media.schema);
    value(d,media.schema,media.example,o.operationId+':request');examples++;
    if(o['x-actor']==='user'){
     const s=resolve(d,media.schema);
     for(const k of ['userId','hostId','hostUserId','playerId','playerAId','playerBId','matchId'])fail(!has(s.properties,k),'Client-selected actor/id '+k);
    }
   }
  }
  assert.deepEqual(actual.sort(),[...expected[name]].sort(),'Operation inventory '+name);
  perService[name]=actual.length;
 }
 fail(operations===32,'Expected 32 operations (30 implemented + 2 proposed)');
 const qs=all.questionbank.paths['/internal/question-sets'].post.responses[200].content['application/json'].example;
 fail(new Set(qs.map(q=>q.id)).size===5,'Snapshot needs distinct IDs');
 for(const q of qs)fail(q.options.filter(a=>a===q.correctAnswer).length===1,'Snapshot answer must match one option');
 const n=all.notification;
 for(const [path,methods]of Object.entries(n.paths))for(const o of Object.values(methods)){
  const samples=[o.requestBody?.content?.['application/json']?.example,...Object.values(o.responses).map(r=>r.content?.['application/json']?.example)].filter(Boolean).flat();
  for(const e of samples)if(e.type==='MATCH_CREATED'||e.type==='MATCH_COMPLETED'){
   if(e.type==='MATCH_CREATED')fail(Object.keys(e.payload).length===1,'Created payload fields');
   else fail(has(e.payload,'draw')&&has(e.payload,'winnerUserId')&&e.payload.draw===(e.payload.winnerUserId===null),'Completed outcome consistency');
  }
 }
 return {operations,examples,perService};
}
const documents=Object.fromEntries(names.map(n=>[n,JSON.parse(readFileSync(fileURLToPath(new URL('./openapi/'+n+'.yaml',import.meta.url)),'utf8'))]));
const result=validate(documents);
const probes=[
 ['dangling ref',d=>{d.identity.paths['/api/users/me'].get.responses[200].content['application/json'].schema={$ref:'#/components/schemas/Missing'};}],
 ['missing security',d=>{delete d.gameplay.paths['/matches'].post.security;}],
 ['invalid example',d=>{d.statistics.paths['/api/results'].post.requestBody.content['application/json'].example.score=6;}],
 ['public answer key',d=>{d.questionbank.components.schemas.Question.properties.correctAnswer={type:'string'};}],
 ['actor spoofing',d=>{d.identity.paths['/api/users/me'].put.requestBody.content['application/json'].example.userId='victim';}],
 ['snapshot answer mismatch',d=>{d.questionbank.paths['/internal/question-sets'].post.responses[200].content['application/json'].example[0].correctAnswer='Pluto';}],
 ['missing success content',d=>{delete d.identity.paths['/api/users/me'].get.responses[200].content;}],
 ['machine endpoint made public',d=>{const o=d.statistics.paths['/api/results'].post;o['x-actor']='public';o['x-policy']='public';o.security=[];}],
 ['write scope changed to read',d=>{const o=d.identity.paths['/api/users/me'].put;o['x-policy']='profiles.read';o.security=[{oauth2:['profiles.read']}];}],
 ['machine scope in browser flow',d=>{d.statistics.components.securitySchemes.oauth2.flows.authorizationCode.scopes['results.write']='results.write';}],
 ['user scope in machine flow',d=>{d.lobby.components.securitySchemes.oauth2.flows.clientCredentials.scopes['lobbies.write']='lobbies.write';}],
 ['required machine scope removed',d=>{delete d.notification.components.securitySchemes.oauth2.flows.clientCredentials.scopes['events.write'];}],
 ['proposed marker removed',d=>{const o=d.identity.paths['/api/users/me/picture'].put;delete o['x-status'];delete o['x-implemented-by'];},/Proposed marker mismatch/],
 ['proposed status removed but issue kept',d=>{delete d.identity.paths['/api/users/me/picture'].put['x-status'];},/x-implemented-by without x-status/],
 ['picture cache-control header removed',d=>{delete d.identity.paths['/api/users/me/picture'].get.responses[200].headers['Cache-Control'];},/Missing required header Cache-Control/],
 ['picture nosniff header weakened',d=>{d.identity.paths['/api/users/me/picture'].get.responses[200].headers['X-Content-Type-Options'].example='sniff';},/Missing required header X-Content-Type-Options/],
 ['picture read outage outcome removed',d=>{delete d.identity.paths['/api/users/me/picture'].get.responses[503];},/Missing required outcome 503/],
 ['svg picture accepted',d=>{d.identity.paths['/api/users/me/picture'].put.requestBody.content['image/svg+xml']={schema:{type:'string',format:'binary'},example:'<svg/>'};}],
 ['picture read made public',d=>{const o=d.identity.paths['/api/users/me/picture'].get;o['x-actor']='public';o['x-policy']='public';o.security=[];}],
 ['picture size limit outcome removed',d=>{delete d.identity.paths['/api/users/me/picture'].put.responses[413];}],
 ['implemented operation marked proposed',d=>{Object.assign(d.identity.paths['/api/users/me'].get,{'x-status':'proposed','x-implemented-by':'#144'});}],
 ['proposed property made required',d=>{d.identity.components.schemas.Profile.required.push('picture');}]
];
// Optional third element: the failure reason the probe must produce.
for(const [name,mutate,reason]of probes){const copy=structuredClone(documents);mutate(copy);assert.throws(()=>validate(copy),reason?{message:reason}:undefined,'Failed corruption probe '+name);}
console.log(JSON.stringify({status:'PASS',...result,corruptionProbes:probes.length,limitation:'Limited used-keyword schema checks and contract invariants; not full OpenAPI certification or runtime API tests.'},null,2));
