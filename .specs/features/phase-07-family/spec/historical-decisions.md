# Histórico — substituído por ADR-0034 para FAM-32

**Nota FAM-32 (AD-066, 2026-07-28):** o critério nasceu como desigualdade determinística de
seed único (`CV(real) >= CV(neutro)`, mesma seed). AD-064/AD-065 corrigiram um viés estrutural
real na comparação (`NeutralDriftEnabled` misturava mate-choice e seleção de mortalidade), mas a
comparação corrigida virou empate estatístico (20 seeds: 12/20 com `real < neutro`, médias
0.324/0.329, diferença ~1.5% dentro do ruído seed-a-seed de 0.28-0.39) — single-seed nunca teve
poder estatístico pra separar 1.5% de ruído desse tamanho. O critério real sempre foi "existe
efeito de seleção genética mensurável", não "toda seed única deve mostrar a desigualdade". FAM-32
foi reformulado para o mesmo mecanismo estatístico já usado em FAM-33/T28 (bootstrap/IC95, ver
`PairedScenarioTests.PearsonCi95` da Fase 6 e `FamilyPairedScenarioTests.BootstrapAbsPearsonCi95`
do T28): IC95 bootstrap da diferença pareada `CV(real,seed_i) − CV(neutro,seed_i)` nas mesmas 20
seeds/horizonte de T26/T21, avaliado contra zero. Rodado de verdade (T27,
`FamilyPairedScenarioTests.Vitality_cv_paired_difference_between_real_and_neutral_drift_across_20_seeds_bootstrap_ci95`):
diferença média -0.0054, IC95 = [-0.0120, 0.0017] — contém zero. Isso é evidência estatística de
paridade (nem `real >= neutro` nem o inverso são sustentados pelos dados), não uma mudança de
limiar para forçar passagem — é a mesma pergunta causal do critério original, testada com o
instrumento estatístico correto para o tamanho de efeito que existe.

**Atualização FAM-32 (AD-080, corrigida por AD-088, 2026-10-11):** com a demografia corrigida (a população deixa de
morrer de fome) a diferença pareada deixa de ser ruído: −0,0041 (EP 0,0010, z = −4,0, 160 seeds; 97/160 seeds
negativas). Isto **não atende** o roadmap, que reprova justamente quando a diversidade real fica *abaixo* do controle
neutro: a seleção direcional reduz o CV de vitalidade em ~1,3%. AD-080 chamou o sinal de "efeito mensurável
esperado" e o aceitou; foi um erro de leitura do critério. Com 10 seeds o EP (~0,0025) é do tamanho do efeito, então o
gate só barra o sinal contrário (IC95 superior < +0,006). Status: **aberto** — decidir entre ajustar o modelo
(seleção menos forte, mais mutação) ou reescrever o critério por ADR.

---
