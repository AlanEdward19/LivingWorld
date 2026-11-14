// Cenários longos começam primeiro; os testes rápidos enchem a cauda da suíte (só a ordem de partida muda).
[assembly: TestCollectionOrderer("LivingWorld.Tests.Shared.Parallelism.ScenarioFirstCollectionOrderer", "LivingWorld.Tests.Shared")]
