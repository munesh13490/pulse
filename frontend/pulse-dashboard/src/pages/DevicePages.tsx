import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogContent,
  DialogTitle,
  MenuItem,
  Paper,
  TextField,
  Typography,
} from "@mui/material";
import {
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { api } from "../api/client";
import { DeviceSelect } from "../components/DeviceSelect";
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

export function DevicesPage({ user }: { user: CurrentUser }) {
  const operate = user.roles.some(
    (role) => role === "tenant-admin" || role === "operator",
  );
  const [device, setDevice] = useState<ApiRow | null>(null);
  const query = useQuery({
    queryKey: ["/v1/devices"],
    queryFn: () => api<ApiRow[]>("/v1/devices"),
  });
  const state = query.isPending || query.error ? <LoadingOrError loading={query.isPending} error={query.error} /> : null;
  return (
    <>
      <PageTitle
        title="Devices"
        subtitle="Register sensors and manage their credentials"
      />
      {operate && (
        <FormCard
          label="Register device"
          onSave={(data) => post("/v1/devices", { name: value(data, "name") })}
        >
          <RequiredField name="name" label="Device name" />
        </FormCard>
      )}
      {state ?? (
        <DataTable
          rows={query.data ?? []}
          columns={["id", "name", "enabled", "created_at"]}
          action={
            operate
              ? (row) => <Button onClick={() => setDevice(row)}>Manage</Button>
              : undefined
          }
        />
      )}
      <Dialog
        open={!!device}
        onClose={() => setDevice(null)}
        fullWidth
        maxWidth="md"
      >
        <DialogTitle>
          {displayValue(device?.name)} · device management
        </DialogTitle>
        <DialogContent>
          {device && (
            <DeviceDetail key={displayValue(device.id)} device={device} />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

function DeviceDetail({ device }: { device: ApiRow }) {
  const [key, setKey] = useState("");
  const [error, setError] = useState("");
  const cache = useQueryClient();
  const id = String(device.id);
  const keys = useQuery({
    queryKey: ["device-keys", id],
    queryFn: () => api<ApiRow[]>(`/v1/devices/${id}/keys`),
  });
  const state = keys.isPending || keys.error ? <LoadingOrError loading={keys.isPending} error={keys.error} /> : null;
  return (
    <>
      <FormCard
        label="Update device"
        onSave={(data) =>
          api(`/v1/devices/${id}`, {
            method: "PATCH",
            body: JSON.stringify({
              name: value(data, "name"),
              enabled: value(data, "enabled") === "true",
            }),
          })
        }
      >
        <RequiredField
          name="name"
          label="Name"
          value={displayValue(device.name)}
        />
        <TextField
          select
          name="enabled"
          label="Enabled"
          defaultValue={String(device.enabled)}
        >
          <MenuItem value="true">Enabled</MenuItem>
          <MenuItem value="false">Disabled</MenuItem>
        </TextField>
      </FormCard>
      <FormCard
        label="Issue / rotate key"
        onSave={async (data) => {
          const result = await api<{ apiKey: string }>(
            `/v1/devices/${id}/keys`,
            {
              method: "POST",
              body: JSON.stringify({
                expiresAt: new Date(value(data, "expiry")).toISOString(),
              }),
            },
          );
          setKey(result.apiKey);
        }}
      >
        <RequiredField
          name="expiry"
          label="Key expiry"
          type="datetime-local"
          value={new Date(Date.now() + 86400000 * 30)
            .toISOString()
            .slice(0, 16)}
        />
        <Typography color="text.secondary">
          Rotation immediately revokes the previous key.
        </Typography>
      </FormCard>
      {key && (
        <Alert severity="warning" onClose={() => setKey("")}>
          Copy this key now. It will not be shown again.
          <Box
            component="code"
            sx={{ display: "block", overflowWrap: "anywhere", mt: 1 }}
          >
            {key}
          </Box>
        </Alert>
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {state ?? (
        <DataTable
          rows={keys.data ?? []}
          columns={["id", "expires_at", "revoked_at"]}
          action={(row) => (
            <Button
              disabled={!!row.revoked_at}
              onClick={async () => {
                try {
                  await post(`/v1/devices/${id}/keys/${row.id}/revoke`, {});
                  await cache.invalidateQueries();
                } catch (exception) {
                  setError(String(exception));
                }
              }}
            >
              Revoke
            </Button>
          )}
        />
      )}
    </>
  );
}

export function TelemetryPage() {
  const [device, setDevice] = useState("");
  const query = useQuery({
    queryKey: ["telemetry", device],
    queryFn: () => api<ApiRow[]>(`/v1/telemetry?deviceId=${device}`),
    enabled: !!device,
  });
  return (
    <>
      <PageTitle title="Telemetry" subtitle="Latest 500 events per device" />
      <DeviceSelect onChange={setDevice} />
      {query.isFetching && <LoadingOrError loading error={null} />}{" "}
      {query.error && <Alert severity="error">{query.error.message}</Alert>}
      {query.data && (
        <>
          <Paper sx={{ p: 3, my: 3, height: 320 }}>
            <ResponsiveContainer>
              <LineChart data={[...query.data].reverse()}>
                <XAxis dataKey="occurred_at" hide />
                <YAxis />
                <Tooltip />
                <Line dataKey="value" stroke="#65dfc2" dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </Paper>
          <DataTable
            rows={query.data}
            columns={["event_id", "metric", "value", "occurred_at"]}
          />
        </>
      )}
    </>
  );
}
