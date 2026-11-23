import { useQuery } from "@tanstack/react-query";
import { MenuItem, TextField } from "@mui/material";
import { api } from "../api/client";
import { displayValue } from "./ui";
import type { ApiRow } from "../types";

export function DeviceSelect({
  onChange,
  name = "device",
}: {
  onChange?: (value: string) => void;
  name?: string;
}) {
  const query = useQuery({
    queryKey: ["/v1/devices"],
    queryFn: () => api<ApiRow[]>("/v1/devices"),
  });
  return (
    <TextField
      fullWidth
      select
      name={name}
      label="Device"
      defaultValue=""
      required
      onChange={(event) => onChange?.(event.target.value)}
    >
      <MenuItem value="" disabled>
        Select device
      </MenuItem>
      {query.data?.map((device) => (
        <MenuItem key={displayValue(device.id)} value={displayValue(device.id)}>
          {displayValue(device.name)}
        </MenuItem>
      ))}
    </TextField>
  );
}
