import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { WorldRepository } from "./repository/WorldRepository";
import type { DraftRepository } from "./repository/DraftRepository";
import type { WorldGenerationService } from "./repository/WorldGenerationService";
import { MockWorldRepository } from "./repository/MockWorldRepository";
import { LocalStorageDraftRepository } from "./repository/LocalStorageDraftRepository";
import { MockWorldGenerationService } from "./repository/MockWorldGenerationService";

export type EntryServices = {
  worlds: WorldRepository;
  drafts: DraftRepository;
  generation: WorldGenerationService;
};

const EntryServicesContext = createContext<EntryServices | null>(null);

/** Doc §7/§95 — components never touch a mock/backend implementation directly, only this context. */
export function EntryServicesProvider({ services, children }: { services?: EntryServices; children: ReactNode }) {
  const value = useMemo<EntryServices>(
    () =>
      services ?? {
        worlds: new MockWorldRepository(),
        drafts: new LocalStorageDraftRepository(),
        generation: new MockWorldGenerationService(),
      },
    [services],
  );
  return <EntryServicesContext.Provider value={value}>{children}</EntryServicesContext.Provider>;
}

export function useEntryServices(): EntryServices {
  const services = useContext(EntryServicesContext);
  if (!services) throw new Error("useEntryServices must be used within EntryServicesProvider");
  return services;
}
