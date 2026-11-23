import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
} from "@mui/material";
import { api, stream } from "../api/client";
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

export function RulesPage({ user }: { user: CurrentUser }) {
  const operate = user.roles.some(
    (role) => role === "tenant-admin" || role === "operator",
  );
  const [versionRule, setVersionRule] = useState<ApiRow | null>(null);
  const [history, setHistory] = useState<string | null>(null);
  const rules = useQuery({
    queryKey: ["/v1/rules"],
    queryFn: () => api<ApiRow[]>("/v1/rules"),
  });
  const state = rules.isPending || rules.error ? <LoadingOrError loading={rules.isPending} error={rules.error} /> : null;
  const form = (existing?: ApiRow) => (
    <FormCard
      label={existing ? "Append immutable version" : "Create rule"}
      onSave={(data) =>
        post(existing ? `/v1/rules/${existing.id}/versions` : "/v1/rules", {
          deviceId: existing ? existing.device_id : value(data, "device"),
          name: existing ? existing.name : value(data, "name"),
          kind: value(data, "kind"),
          metric: value(data, "metric"),
          threshold: Number(value(data, "threshold")),
          windowSeconds: Number(value(data, "window")),
          suppressionSeconds: Number(value(data, "suppression")),
        })
      }
    >
      {!existing && (
        <>
          <RequiredField name="name" label="Rule name" />
          <DeviceSelect />
        </>
      )}
      <TextField
        select
        label="Rule type"
        name="kind"
        defaultValue={existing ? displayValue(existing.kind) : "threshold"}
      >
        <MenuItem value="threshold">Window average threshold</MenuItem>
        <MenuItem value="rate-of-change">
          Rate per second (last − first) / seconds
        </MenuItem>
      </TextField>
      <RequiredField
        name="metric"
        label="Metric"
        value={existing ? displayValue(existing.metric) : "temperature"}
      />
      <RequiredField
        name="threshold"
        label="Threshold"
        type="number"
        value={existing ? displayValue(existing.threshold) : "50"}
      />
      <RequiredField
        name="window"
        label="Window seconds"
        type="number"
        value={existing ? displayValue(existing.window_seconds) : "300"}
      />
      <RequiredField
        name="suppression"
        label="Suppression seconds (creation only)"
        type="number"
        value="300"
      />
    </FormCard>
  );
  return (
    <>
      <PageTitle
        title="Rules & versions"
        subtitle="Immutable event-time evaluation policies"
      />
      {operate && form()}
      {state ?? (
        <DataTable
          rows={rules.data ?? []}
          columns={["name", "kind", "metric", "threshold", "version"]}
          action={(row) => (
            <>
              <Button onClick={() => setHistory(displayValue(row.id))}>
                Versions
              </Button>
              {operate && (
                <Button onClick={() => setVersionRule(row)}>New version</Button>
              )}
            </>
          )}
        />
      )}
      <Dialog
        open={!!versionRule}
        onClose={() => setVersionRule(null)}
        fullWidth
      >
        <DialogTitle>Append rule version</DialogTitle>
        <DialogContent>{versionRule && form(versionRule)}</DialogContent>
      </Dialog>
      <Dialog
        open={!!history}
        onClose={() => setHistory(null)}
        fullWidth
        maxWidth="md"
      >
        <DialogTitle>Version history</DialogTitle>
        <DialogContent>
          {history && (
            <RecordList
              path={`/v1/rules/${history}/versions`}
              columns={[
                "version",
                "kind",
                "metric",
                "threshold",
                "window_seconds",
                "created_at",
              ]}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

export function AlertsPage({ user }: { user: CurrentUser }) {
  const [state, setState] = useState("");
  const [cursor, setCursor] = useState("");
  const [history, setHistory] = useState<string | null>(null);
  const [error, setError] = useState("");
  const cache = useQueryClient();
  const operate = user.roles.some(
    (role) => role === "tenant-admin" || role === "operator",
  );
  const query = useQuery({
    queryKey: ["alerts", state, cursor],
    queryFn: () =>
      api<{
        items: ApiRow[];
        next: { before: string; beforeId: string } | null;
      }>(`/v1/alerts?state=${state}${cursor}`),
  });
  return (
    <>
      <PageTitle
        title="Alerts"
        subtitle="Review and transition finalized alerts"
      />
      <TextField
        select
        label="State"
        value={state}
        onChange={(event) => {
          setState(event.target.value);
          setCursor("");
        }}
        sx={{ minWidth: 220, mb: 3 }}
      >
        {["", "OPEN", "ACKNOWLEDGED", "RESOLVED"].map((option) => (
          <MenuItem key={option} value={option}>
            {option || "All states"}
          </MenuItem>
        ))}
      </TextField>
      {error && <Alert severity="error">{error}</Alert>}
      {query.isPending ? (
        <LoadingOrError loading error={null} />
      ) : query.error ? (
        <Alert severity="error">{query.error.message}</Alert>
      ) : (
        <>
          <DataTable
            rows={query.data.items}
            columns={["id", "state", "message", "created_at"]}
            action={(row) => (
              <>
                <Button onClick={() => setHistory(displayValue(row.id))}>
                  History
                </Button>
                {operate &&
                  ["ACKNOWLEDGED", "RESOLVED"]
                    .filter(
                      (nextState) =>
                        row.state === "OPEN" ||
                        (row.state === "ACKNOWLEDGED" &&
                          nextState === "RESOLVED"),
                    )
                    .map((nextState) => (
                      <Button
                        key={nextState}
                        onClick={async () => {
                          try {
                            await post(`/v1/alerts/${row.id}/transitions`, {
                              state: nextState,
                            });
                            await cache.invalidateQueries();
                          } catch (exception) {
                            setError(String(exception));
                          }
                        }}
                      >
                        {nextState === "RESOLVED" ? "Resolve" : "Acknowledge"}
                      </Button>
                    ))}
              </>
            )}
          />
          <Stack direction="row" spacing={2} sx={{ mt: 2 }}>
            <Button onClick={() => setCursor("")}>Latest</Button>
            <Button
              disabled={!query.data.next}
              onClick={() => {
                const next = query.data.next!;
                setCursor(
                  `&before=${encodeURIComponent(next.before)}&beforeId=${next.beforeId}`,
                );
              }}
            >
              Older alerts
            </Button>
          </Stack>
        </>
      )}
      <Dialog
        open={!!history}
        onClose={() => setHistory(null)}
        fullWidth
        maxWidth="md"
      >
        <DialogTitle>Alert transitions</DialogTitle>
        <DialogContent>
          {history && (
            <RecordList
              path={`/v1/alerts/${history}/transitions`}
              columns={["from_state", "to_state", "user_id", "created_at"]}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

export function LivePage() {
  const [rows, setRows] = useState<ApiRow[]>([]);
  const [error, setError] = useState("");
  useEffect(() => {
    const abort = new AbortController();
    stream(abort.signal, (data) => setRows(data as ApiRow[])).catch(
      (exception) => {
        if (!abort.signal.aborted) setError(String(exception));
      },
    );
    return () => abort.abort();
  }, []);
  return (
    <>
      <PageTitle
        title="Live feed"
        subtitle="Replica-wide Redis notifications with database resynchronization"
      />
      {error ? (
        <Alert severity="error">{error} Reopen this page to reconnect.</Alert>
      ) : (
        <Chip
          color="primary"
          label="Connected · resynchronizing every 15 seconds"
          sx={{ mb: 3 }}
        />
      )}
      <DataTable
        rows={rows}
        columns={["id", "state", "message", "updated_at"]}
      />
    </>
  );
}

function RecordList({ path, columns }: { path: string; columns: string[] }) {
  const query = useQuery({
    queryKey: [path],
    queryFn: () => api<ApiRow[]>(path),
  });
  if (query.isPending || query.error)
    return <LoadingOrError loading={query.isPending} error={query.error} />;
  return <DataTable rows={query.data ?? []} columns={columns} />;
}
