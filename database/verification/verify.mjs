import {PGlite} from '@electric-sql/pglite';
import {readFile} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import assert from 'node:assert/strict';
const pg=new PGlite();
try {
 await pg.exec('CREATE ROLE pulse_app NOSUPERUSER NOBYPASSRLS; CREATE ROLE pulse_auth NOSUPERUSER NOBYPASSRLS;');
 for(const script of ['002_create_tables','003_indexes_constraints','004_rls','005_functions','006_partitions']){
  await pg.exec(await readFile(new URL('../scripts/'+script+'.sql',import.meta.url),'utf8'));
  console.log(script+': PASS');
 }
 const userId = randomUUID();
 await pg.transaction(async tx => {
  await tx.query("INSERT INTO app_users(id,user_name,normalized_user_name,password_hash) VALUES ($1,'legacy','LEGACY','test-hash')", [userId]);
  await tx.query("INSERT INTO app_user_roles(id,user_id,role_id) SELECT $1,$2,id FROM app_roles WHERE name='viewer'", [randomUUID(), userId]);
  await tx.exec(await readFile(new URL('../scripts/007_single_user_role.sql',import.meta.url),'utf8'));
  const {rows} = await tx.query('SELECT u.email, r.name, r.role_name, r.is_active FROM app_users u JOIN app_roles r ON r.id=u.role_id WHERE u.id=$1', [userId]);
  assert.deepEqual(rows, [{email:'', name:'viewer', role_name:'Viewer', is_active:true}]);
  await tx.query('DELETE FROM app_users WHERE id=$1', [userId]);
 });
 console.log('007_single_user_role (existing user backfill): PASS');
 for(const script of ['schema_checks','tenant_isolation_checks','concurrency_checks']){
  await pg.exec(await readFile(new URL(script+'.sql',import.meta.url),'utf8'));
  console.log(script+': PASS');
 }
 const tenantA=randomUUID(),tenantB=randomUUID(),device=randomUUID(),event=randomUUID();
 await pg.query('INSERT INTO tenants(id,name) VALUES($1,\'A\'),($2,\'B\')',[tenantA,tenantB]);
 await pg.query('INSERT INTO devices(id,tenant_id,name) VALUES($1,$2,\'sensor\')',[device,tenantA]);
 await pg.transaction(async tx=>{
  await tx.exec('SET LOCAL ROLE pulse_app');
  await tx.query("SELECT set_config('pulse.tenant_id',$1,true)",[tenantB]);
  assert.equal((await tx.query('SELECT * FROM devices')).rows.length,0);
 });
 await assert.rejects(pg.transaction(async tx=>{
  await tx.exec('SET LOCAL ROLE pulse_app');
  await tx.query("SELECT set_config('pulse.tenant_id',$1,true)",[tenantB]);
  await tx.query("INSERT INTO devices(id,tenant_id,name) VALUES($1,$2,'forbidden')",[randomUUID(),tenantA]);
 }),/row-level security/);
 await pg.transaction(async tx=>{
  await tx.exec('SET LOCAL ROLE pulse_app');
  assert.equal((await tx.query('SELECT * FROM devices')).rows.length,0);
  await tx.query("SELECT set_config('pulse.tenant_id',$1,true)",[tenantA]);
  assert.equal((await tx.query('SELECT * FROM devices')).rows.length,1);
  const insert="INSERT INTO telemetry_idempotency(id,tenant_id,device_id,event_id) VALUES($1,$2,$3,$4) ON CONFLICT(tenant_id,device_id,event_id) DO NOTHING RETURNING id";
  assert.equal((await tx.query(insert,[randomUUID(),tenantA,device,event])).rows.length,1);
  assert.equal((await tx.query(insert,[randomUUID(),tenantA,device,event])).rows.length,0);
  await tx.query("INSERT INTO telemetry_events(id,tenant_id,device_id,event_id,metric,value,occurred_at) VALUES($1,$2,$3,$4,'temp',42,now())",[randomUUID(),tenantA,device,event]);
 });
 const rule=randomUUID(),version=randomUUID(),alert=randomUUID();
 await pg.query("INSERT INTO alert_rules(id,tenant_id,device_id,name) VALUES($1,$2,$3,'temperature')",[rule,tenantA,device]);
 await pg.query("INSERT INTO rule_versions(id,tenant_id,rule_id,version,kind,metric,threshold,window_seconds) VALUES($1,$2,$3,1,'threshold','temp',40,300)",[version,tenantA,rule]);
 await assert.rejects(pg.query('UPDATE rule_versions SET threshold=50 WHERE id=$1',[version]),/immutable/);
 await pg.query("INSERT INTO alerts(id,tenant_id,device_id,rule_version_id,event_id,message) VALUES($1,$2,$3,$4,$5,'hot')",[alert,tenantA,device,version,event]);
 await pg.query("UPDATE alerts SET state='ACKNOWLEDGED' WHERE id=$1",[alert]);
 await assert.rejects(pg.query("UPDATE alerts SET state='OPEN' WHERE id=$1",[alert]),/Invalid alert transition/);
 await pg.query("UPDATE alerts SET state='RESOLVED' WHERE id=$1",[alert]);
 console.log('Seeded tenant isolation, context reset, RLS write rejection, duplicate ledger, partition routing, immutable versions, alert transitions: PASS');
} finally {await pg.close();}
