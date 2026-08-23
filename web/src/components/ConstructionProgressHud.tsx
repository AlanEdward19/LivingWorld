import { constructionAccessibleLabel } from "../map-engine/constructionSite";
import type { ProcessVisual } from "../data/contracts";

export function ConstructionProgressHud({ processes }: { processes: Iterable<ProcessVisual> }) {
  const jobs = [...processes].filter((process) => process.kind === "construction");
  if (jobs.length === 0) {
    return null;
  }

  return (
    <section aria-label="Obras em andamento">
      {jobs.map((job) => (
        <p key={job.id} role="status">
          {constructionAccessibleLabel(job.progress)}
          {` · tipo ${job.targetId}`}
        </p>
      ))}
    </section>
  );
}
