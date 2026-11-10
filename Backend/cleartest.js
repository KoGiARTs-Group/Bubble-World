require('dotenv').config();
const { db } = require('./dist/db');
(async () => {
  const r = await db.query('DELETE FROM client_world_states');
  console.log('cleared rows:', r.rowCount);
  process.exit(0);
})().catch((e) => { console.error(e.message); process.exit(1); });
