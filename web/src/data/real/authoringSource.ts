import {
  breakNpcRelationships, fetchPowerCatalog, forceNpcAction, grantNpcPower,
  invokeNpcPower, revokeNpcPower, rewriteNpcPersonality,
} from "../../api";
import type { AuthoringSource, PersonalityValues } from "../sources";

export class RealAuthoringSource implements AuthoringSource {
  powerCatalog = fetchPowerCatalog;
  grantPower = grantNpcPower;
  revokePower = revokeNpcPower;
  invokePower = invokeNpcPower;
  rewritePersonality = (npcId: number, value: PersonalityValues) => rewriteNpcPersonality(npcId, value);
  breakRelationships = breakNpcRelationships;
  forceAction = forceNpcAction;
}
