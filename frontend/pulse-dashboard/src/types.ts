export type CurrentUser = {
 id: string;
 tenantId: string | null;
 name: string;
 roles: string[];
};

export type ApiRow = Record<string, unknown>;
