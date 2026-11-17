The executable migration is [Initial.cs](../../backend/Pulse.Infrastructure/Migrations/Initial.cs).
It embeds scripts 002–006 verbatim; a unit test checks all five resources exist in deployment order.
Use the API --migrate command. Do not apply standalone scripts on an already migrated schema.
