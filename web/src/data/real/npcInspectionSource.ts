import { fetchNpcInspection } from "../../api";
import type { NpcInspectionSource } from "../sources";

// T50: GET nunca materializa (por design — G9) e, desde T50, também nunca falha pra um id
// pendente de verdade — devolve Lod.Pooled com os dados mínimos. O fallback automático que
// existia aqui (materializar sozinho quando o GET não achava nada) virou ação explícita do
// usuário (botão "Materializar" em NpcInspector.tsx), não mais um efeito colateral escondido de
// simplesmente abrir o inspetor.
export class RealNpcInspectionSource implements NpcInspectionSource {
  load(npcId: number) {
    return fetchNpcInspection(npcId);
  }
}
