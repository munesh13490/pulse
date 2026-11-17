-- Run only as cluster administrator against an empty local cluster.
-- Passwords are supplied via psql variables, never literals in this file.
CREATE ROLE pulse_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS PASSWORD :'app_password';
CREATE ROLE pulse_auth LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS PASSWORD :'auth_password';
CREATE DATABASE pulse;
-- The Compose bootstrap script performs this separately using environment secrets.
