import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Box,
  Button,
  Divider,
  Grid,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { api } from "../api/client";
import {
  DataTable,
  FormCard,
  LoadingOrError,
  PageTitle,
  RequiredField,
  displayValue,
} from "../components/ui";
import type { ApiRow, CurrentUser } from "../types";

const value = (data: FormData, name: string) => String(data.get(name) ?? "");
const post = (path: string, body: unknown) =>
  api(path, { method: "POST", body: JSON.stringify(body) });

export function TenantsPage() {
  return (
    <>
      <PageTitle title="Tenants" subtitle="Provision isolated workspaces and platform access." />
      <Grid container spacing={3} sx={{ mb: 1 }}>
        <Grid size={{ xs: 12, lg: 5 }}>
          <FormCard label="Create tenant" onSave={(data) => post("/v1/admin/tenants", { name: value(data, "name").trim() })}>
            <Typography variant="h6">New tenant workspace</Typography>
            <Typography variant="body2" color="text.secondary">Creates an isolated workspace. Create a tenant administrator after provisioning.</Typography>
            <RequiredField name="name" label="Tenant name" />
          </FormCard>
        </Grid>
        <Grid size={{ xs: 12, lg: 7 }}>
          <Paper sx={{ p: 3, height: "calc(100% - 24px)" }}>
            <Typography variant="h6">Provisioning sequence</Typography>
            <Stack spacing={1.5} sx={{ mt: 2 }}>
              <ProvisioningStep number="1" title="Create workspace" description="A tenant becomes the security and data boundary for devices, users, and alerts." />
              <ProvisioningStep number="2" title="Assign an administrator" description="Choose the new tenant while creating a tenant-admin user." />
              <ProvisioningStep number="3" title="Register devices" description="Sign in as the tenant administrator to configure devices and API credentials." />
            </Stack>
          </Paper>
        </Grid>
      </Grid>
      <RecordList
        path="/v1/admin/tenants"
        columns={["id", "name", "created_at"]}
      />
    </>
  );
}

export function UsersPage({ user }: { user: CurrentUser }) {
  const platform = user.roles.includes("platform-admin");
  const [role, setRole] = useState("viewer");
  const [tenantId, setTenantId] = useState("");
  const tenants = useQuery({
    queryKey: ["/v1/admin/tenants"],
    queryFn: () => api<ApiRow[]>("/v1/admin/tenants"),
    enabled: platform,
  });
  const cache = useQueryClient();
  const [error, setError] = useState("");
  const selectedRoleIsPlatformAdmin = role === "platform-admin";
  const availableRoles = [
    ...(platform ? ["platform-admin"] : []),
    "tenant-admin",
    "operator",
    "viewer",
  ];
  return (
    <>
      <PageTitle
        title="User administration"
        subtitle="Create users and assign tenant roles"
      />
      <FormCard label="Create user" onSave={(data) => {
        const resolvedTenantId = platform ? (selectedRoleIsPlatformAdmin ? null : tenantId) : user.tenantId;
        if (!resolvedTenantId && !selectedRoleIsPlatformAdmin) throw new Error("Select a tenant for every tenant-scoped user.");
        return post("/v1/admin/users", { username: value(data, "username").trim(), password: value(data, "password"), role, tenantId: resolvedTenantId });
      }}>
        <Typography variant="h6">Invite a local user</Typography>
        <Typography variant="body2" color="text.secondary">The API creates credentials for the selected role and tenant boundary.</Typography>
        <RequiredField name="username" label="Username" />
        <RequiredField name="password" label="Initial password" type="password" />
        <TextField select name="role" label="Role" value={role} onChange={(event) => { setRole(event.target.value); if (event.target.value === "platform-admin") setTenantId(""); }} helperText={selectedRoleIsPlatformAdmin ? "Platform administrators do not belong to a tenant." : "Tenant-scoped users must belong to one tenant."}>
          {availableRoles.map((availableRole) => <MenuItem key={availableRole} value={availableRole}>{availableRole}</MenuItem>)}
        </TextField>
        {platform && !selectedRoleIsPlatformAdmin && <TextField select required name="tenant" label="Tenant" value={tenantId} onChange={(event) => setTenantId(event.target.value)} helperText={tenants.isPending ? "Loading tenants…" : "Required for tenant-admin, operator, and viewer roles."}>
          <MenuItem value="" disabled>Select tenant</MenuItem>
          {tenants.data?.map((tenant) => <MenuItem key={displayValue(tenant.id)} value={displayValue(tenant.id)}>{displayValue(tenant.name)}</MenuItem>)}
        </TextField>}
        {!platform && <Alert severity="info">This user will be assigned to your current tenant.</Alert>}
      </FormCard>
      {error && <Alert severity="error">{error}</Alert>}
      <RecordList
        path="/v1/admin/users"
        columns={["id", "user_name", "roles", "disabled"]}
        action={(row) => (
          <Button
            disabled={Boolean(row.disabled) || row.id === user.id}
            onClick={async () => {
              try {
                await post(`/v1/admin/users/${row.id}/disable`, {});
                await cache.invalidateQueries();
              } catch (exception) {
                setError(String(exception));
              }
            }}
          >
            Disable
          </Button>
        )}
      />
    </>
  );
}

function ProvisioningStep({ number, title, description }: { number: string; title: string; description: string }) {
  return <Stack direction="row" spacing={1.5} alignItems="flex-start"><Box sx={{ width: 26, height: 26, borderRadius: "50%", bgcolor: "primary.main", color: "primary.contrastText", display: "grid", placeItems: "center", fontWeight: 800, flexShrink: 0 }}>{number}</Box><Box><Typography fontWeight={700}>{title}</Typography><Typography variant="body2" color="text.secondary">{description}</Typography></Box></Stack>;
}

export function SettingsPage() {
  const query = useQuery({
    queryKey: ["limits"],
    queryFn: () => api<ApiRow[]>("/v1/admin/tenant-rate-limits"),
  });
  if (query.isPending || query.error)
    return (
      <>
        <PageTitle
          title="Tenant settings"
          subtitle="Shared Redis quotas across API replicas"
        />
        <LoadingOrError loading={query.isPending} error={query.error} />
      </>
    );
  return (
    <>
      <PageTitle
        title="Tenant settings"
        subtitle="Shared Redis quotas across API replicas"
      />
      <FormCard
        onSave={(data) =>
          api("/v1/admin/tenant-rate-limits", {
            method: "PATCH",
            body: JSON.stringify({
              requestsPerMinute: Number(value(data, "requests")),
              eventsPerMinute: Number(value(data, "events")),
            }),
          })
        }
      >
        <RequiredField
          name="requests"
          label="Requests per 60 seconds"
          type="number"
          value={displayValue(query.data?.[0]?.requests_per_minute ?? 100)}
        />
        <RequiredField
          name="events"
          label="Events per 60 seconds"
          type="number"
          value={displayValue(query.data?.[0]?.events_per_minute ?? 10000)}
        />
        <Divider />
        <Typography color="text.secondary">
          Every structurally valid item counts toward the event quota, including
          duplicates and rejected events.
        </Typography>
      </FormCard>
    </>
  );
}

function RecordList({
  path,
  columns,
  action,
}: {
  path: string;
  columns: string[];
  action?: (row: ApiRow) => React.ReactNode;
}) {
  const query = useQuery({
    queryKey: [path],
    queryFn: () => api<ApiRow[]>(path),
  });
  if (query.isPending || query.error)
    return <LoadingOrError loading={query.isPending} error={query.error} />;
  return <DataTable rows={query.data ?? []} columns={columns} action={action} />;
}
