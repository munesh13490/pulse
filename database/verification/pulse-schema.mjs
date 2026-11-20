import {PGlite} from '@electric-sql/pglite';
import {pgcrypto} from '@electric-sql/pglite/contrib/pgcrypto';
import {readdir, readFile} from 'node:fs/promises';
import assert from 'node:assert/strict';

const pg = new PGlite({extensions: {pgcrypto}});
try {
  await pg.transaction(async tx => {
    for (const name of ['008_pulse_schema', '009_pulse_runtime']) {
      await tx.exec(await readFile(new URL(`../scripts/${name}.sql`, import.meta.url), 'utf8'));
    }
    const {rows} = await tx.query('SELECT role_code FROM roles ORDER BY role_code');
    assert.deepEqual(rows.map(row => row.role_code), ['ADMIN', 'OPERATOR', 'PLATFORM_ADMIN', 'VIEWER']);

    let count = 0;
    const infrastructure = new URL('../../backend/Pulse.Infrastructure/', import.meta.url);
    for (const folder of ['Repositories', 'Processing', 'Identity']) {
      const directory = new URL(`${folder}/`, infrastructure);
      for (const file of await readdir(directory)) {
        if (!file.endsWith('.cs') || file === 'RetentionRepository.cs') continue;
        const code = await readFile(new URL(file, directory), 'utf8');
        for (const query of code.matchAll(/Database\.(?:Query|Execute)\(\w+,\s*(?:"""\s*([\s\S]*?)\s*"""|"([^"\n]*)")\s*,/g)) {
          const parameters = new Map();
          const sql = (query[1] ?? query[2]).replace(/@(\w+)/g, (_, name) => {
            if (!parameters.has(name)) parameters.set(name, parameters.size + 1);
            return '$' + parameters.get(name);
          });
          try {
            await tx.exec(`PREPARE pulse_query_${++count} AS ${sql}`);
          } catch (error) {
            throw new Error(`${file}: ${error.message}\n${sql}`, {cause: error});
          }
        }
      }
    }
    assert.ok(count >= 40, `Expected to verify the runtime query set, checked ${count}`);
    console.log(`Pulse schema: ${count} runtime SQL statements prepared successfully.`);
  });
} finally {
  await pg.close();
}
