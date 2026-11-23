import { useState, type FormEvent, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Box,
  Button,
  LinearProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import type { ApiRow } from "../types";

export const displayValue = (value: unknown) =>
  Array.isArray(value)
    ? value.join(", ")
    : value === null
      ? "—"
      : typeof value === "boolean"
        ? value
          ? "Yes"
          : "No"
        : String(value ?? "—");

export function PageTitle({
  title,
  subtitle,
}: {
  title: string;
  subtitle?: string;
}) {
  return (
    <Box sx={{ mb: 4 }}>
      <Typography variant="h4">
        {title}
      </Typography>
      {subtitle && (
      <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 680 }}>
          {subtitle}
        </Typography>
      )}
    </Box>
  );
}

export function DataTable({
  rows,
  columns,
  action,
}: {
  rows: ApiRow[];
  columns: string[];
  action?: (row: ApiRow) => ReactNode;
}) {
  if (!rows.length) return <Alert severity="info">No records yet.</Alert>;
  return (
    <Paper sx={{ overflow: "hidden" }}>
      <Table size="small" sx={{ minWidth: 600 }}>
        <TableHead>
          <TableRow sx={{ bgcolor: "action.hover" }}>
            {columns.map((column) => (
              <TableCell key={column} sx={{ py: 1.5, fontWeight: 700, color: "text.secondary", textTransform: "capitalize" }}>{column.replaceAll("_", " ")}</TableCell>
            ))}
            {action && <TableCell>Actions</TableCell>}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row, index) => (
            <TableRow key={displayValue(row.id ?? index)} hover sx={{ "&:last-child td": { borderBottom: 0 } }}>
              {columns.map((column) => (
                <TableCell
                  key={column}
                  sx={{ py: 1.65, maxWidth: 300, overflowWrap: "anywhere" }}
                >
                  {displayValue(row[column])}
                </TableCell>
              ))}
              {action && <TableCell>{action(row)}</TableCell>}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Paper>
  );
}

export function FormCard({
  children,
  onSave,
  label = "Save",
}: {
  children: ReactNode;
  onSave: (data: FormData) => Promise<unknown>;
  label?: string;
}) {
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const cache = useQueryClient();
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setBusy(true);
    setSaved(false);
    setError("");
    try {
      await onSave(new FormData(form));
      await cache.invalidateQueries();
      setSaved(true);
    } catch (exception) {
      setError(String(exception));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Paper sx={{ p: { xs: 2.5, md: 3 }, mb: 3 }}>
      <Box component="form" onSubmit={submit}>
        <Stack spacing={2}>
          {children}
          {error && <Alert severity="error">{error}</Alert>}
          {saved && <Alert severity="success">Saved.</Alert>}
          <Button
            variant="contained"
            type="submit"
            disabled={busy}
            sx={{ alignSelf: "flex-start" }}
          >
            {busy ? "Saving…" : label}
          </Button>
        </Stack>
      </Box>
    </Paper>
  );
}

export function RequiredField({
  name,
  label,
  type = "text",
  value,
}: {
  name: string;
  label: string;
  type?: string;
  value?: string;
}) {
  return (
    <TextField
      name={name}
      label={label}
      type={type}
      defaultValue={value}
      required
      fullWidth
    />
  );
}

export function LoadingOrError({
  loading,
  error,
}: {
  loading: boolean;
  error: Error | null;
}) {
  if (loading) return <LinearProgress />;
  return error ? <Alert severity="error">{error.message}</Alert> : null;
}
