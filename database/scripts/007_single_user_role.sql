LOCK TABLE app_users, app_user_roles IN ACCESS EXCLUSIVE MODE;

DO $$
BEGIN
    IF EXISTS (
        SELECT u.id FROM app_users u LEFT JOIN app_user_roles ur ON ur.user_id = u.id
        GROUP BY u.id HAVING count(ur.id) <> 1
    ) THEN
        RAISE EXCEPTION 'Every existing user must have exactly one role before migration.';
    END IF;
END $$;

ALTER TABLE app_roles
    ADD COLUMN role_name varchar(256),
    ADD COLUMN description text,
    ADD COLUMN is_system_role boolean NOT NULL DEFAULT true,
    ADD COLUMN is_active boolean NOT NULL DEFAULT true;
UPDATE app_roles SET role_name = CASE name
    WHEN 'platform-admin' THEN 'Platform Administrator'
    WHEN 'tenant-admin' THEN 'Tenant Administrator'
    WHEN 'operator' THEN 'Operator'
    WHEN 'viewer' THEN 'Viewer'
    ELSE name
END;
ALTER TABLE app_roles ALTER COLUMN role_name SET NOT NULL;

ALTER TABLE app_users ADD COLUMN role_id uuid REFERENCES app_roles(id) ON DELETE RESTRICT;
UPDATE app_users u SET role_id = ur.role_id FROM app_user_roles ur WHERE ur.user_id = u.id;
ALTER TABLE app_users ALTER COLUMN role_id SET NOT NULL;
CREATE INDEX ix_app_users_role_id ON app_users(role_id);
UPDATE app_users SET email = '' WHERE email IS NULL;
ALTER TABLE app_users ALTER COLUMN email SET DEFAULT '', ALTER COLUMN email SET NOT NULL;
