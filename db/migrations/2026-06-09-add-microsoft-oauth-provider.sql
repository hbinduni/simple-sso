-- Migration: allow 'microsoft' as an OAuth provider (Entra ID sign-in).
-- schema.sql already includes this for fresh installs; run this on existing databases.
-- Idempotent: safe to run more than once.

ALTER TABLE oauth_accounts DROP CONSTRAINT IF EXISTS oauth_accounts_provider_check;
ALTER TABLE oauth_accounts
  ADD CONSTRAINT oauth_accounts_provider_check
  CHECK (provider IN ('google', 'facebook', 'twitter', 'microsoft'));
