# Terra, braços e ceifa (AD-149)

A vila vive da lavoura da fazenda comum ([economy.md](economy.md), ano agrícola do AD-135). Até o AD-148 a terra não tinha
fim: a fazenda semeava 150 por morador, até 500 por empregado, e o único limite eram as 160 vagas escritas da fazenda. A vila
crescia ~2% ao ano como uma fronteira de colonização e só freava quando as vagas acabavam, perto de 500 pessoas.

## A terra é finita
- **Demarcada na fundação** (`CropSystem.SeedFirstAgriculturalYear`): o campo recebe `CropBatch.ArableSeed`, a semente
  que a terra toma num ano = fundadores × `SownHectaresPerFounder` (1,4 ha semeados por fundador, o dobro do que eles
  semeiam) × `SeedPerHectare` (220). Estado do mundo (snapshot); 0 = sem limite (mundo antigo, cenário JSON).
- A semeadura nunca passa dela (`SeedTarget`), por mais bocas que haja.
- **Rendimento decrescente por braço**: com a terra toda semeada (`HandsForLand` = terra ÷ 500 por braço), braço a mais só
  capina, esterca e lavra de novo — o rendimento por semente sobe até `HandIntensityGain` (25%), cada vez menos
  (`LandIntensity` = 1 + 0,25 × (1 − braços necessários ÷ braços)).
- **A fazenda só toma a mão que a terra paga** (`CropSystem.LandNeedsHands`, no `EmploymentSystem`): mais um braço se o que
  ele acrescenta à colheita do ano ≥ o que come (`HandKeepPerYear` 320). Com 1,4 ha por fundador: ~62 braços semeiam
  tudo, a terra paga até ~77.

## Quem não tem lugar vai embora — de verdade
A emigração do AD-145 só olhava se havia **alguma** vaga aberta na cidade. A forja tem 40 vagas e contrata só pela
encomenda, então sempre havia "vaga": 0 emigrantes em 20 × 100 anos. Agora a vaga que segura é a que contrataria a pessoa
(`EmploymentSystem.WorkplaceThatHires`: ofício dela, folha paga, a encomenda ou a terra pedem a mão); a mesma pergunta
escolhe a cidade de destino.

## A ceifa é da vila toda
Antes só ceifavam os empregados no campo na passada diária e a janela de 40 dias deixava 22–26% em pé. Com a lavoura madura
a fazenda toma todo adulto do ofício sem trabalho (`AgricultureRules.WholeVillageReaps`), cada um ceifa
`ReapPerWorkerDay` (100) por dia de foice e leva para casa a parte em espécie; passada a ceifa
(`CropSystem.HandsPastTheHarvest`) a fazenda solta, das últimas contratadas para as primeiras, as mãos que a terra não paga.
Com a vila na ceifa, a fazenda passou a creditar só quem está trabalhando (`EconomyRules.PresenceNeedsWork`, E1, ligado no
cenário de código): antes a fila do mercado, que fica na fazenda, contava como lavoura.

## Números (precisam de conferência)
- ~135 kg de trigo semeados por hectare (≈ 2 alqueires por acre, Campbell, *English Seigniorial Agriculture*), a ração ≈ 0,6 kg.
- ~1 ha arável por pessoa na Inglaterra cheia de c.1300 (Campbell; Broadberry et al.); o dobro em 1086.
- Ganho da capina e do esterco 25% e a mão que paga o próprio pão: calibração do modelo, sem fonte.

## Medido
Ver `docs/decisions-log.md` (AD-149) e a linha `por_decada` do `demography-report` (`DecadeTally`). Medir sem a terra:
`LW_AG=noland`; sem a ceifa da vila: `LW_AG=noreap`; com E1: `LW_AG=presence`.

## Ver também
- [population-pressure.md](population-pressure.md) — freios preventivos que a terra cheia aciona
- [household-provisioning.md](household-provisioning.md) — como a casa se abastece
