# Poderes por genética (continuação de [06](06-violencia-poderes-e-restantes.md))

Pedido do dono: "Única coisa que deve fugir da realidade são os poderes, mas neles iremos adotar uma abordagem de genética
(como seria na vida real se fossem existir)." Desenho na seção 3 de `relatorios/relatorio-realismo.md`.

## Estado (AD-156; ver `docs/domain/powers-genetics.md`)

Antes: o filho só herdava se os dois pais fossem portadores, com chance 1,0, e num dos caminhos recebia todos os poderes dos dois, que se acumulavam a cada geração.

1. **Genótipo**: um locus por poder, dois alelos por NPC; cada pai transmite um dos dois com 50%; loci independentes. **Feito** (ligação: P1).
2. **Modo de herança por poder**: dominante, recessivo, ligado ao X (recessivo e dominante) ou poligênico com limiar. **Feito.**
3. **Penetrância e expressividade** (intensidade consumida na rolagem do poder). **Feito.**
4. **Idade e gatilho de manifestação**: início agendado pela curva da puberdade; gatilho do ambiente que exige o genótipo. **Feito.**
5. **Mutação nova** por locus por gameta; fundadores em Hardy-Weinberg pela prevalência. **Feito** (pool agregado: P3).
6. **Custo metabólico** basal de fome e sede; fertilidade e vitalidade por poder. **Feito.**
7. **Fim do acúmulo**: só alelos passam; poder de evento não passa. **Feito.**
8. Mesmo motor para as doenças genéticas (02). **A fazer: P2.**
9. Testes de 50% ± 5% (dominante) e 25% ± 5% (recessivo) e poderes por portador sem crescer. **Feito** (unitários com 4.000 filhos; a medida de 20 seeds foi de 100 anos, não 150: P5).

Sobras do AD-156 (nada some):
- P1. Ligação entre loci (recombinação por distância): hoje todos os loci segregam independentes.
- P2. Um só motor para `Genome` (doenças, AD-139) e `PowerGenome` (poderes): hoje são dois com o mesmo desenho.
- P3. Membro do pool agregado recebe só os poderes autossômicos (sem sexo, o X fica vazio) e não paga o custo de fome e sede
  (o materializado não tem a fome configurada; achado ao ligar o custo).
- P4. Poder novo por mutação de efeito: `MixDescriptorBuilder` ficou sem consumidor (a "mistura" saiu da herança).
- P5. Medir poderes por portador em 20 seeds × 150 anos (feito em 100).
- P6. Efeito social da genética: escolha de parceiro pelo poder, esconder o poder dos filhos, a cultura reagindo à linhagem.
- P7. Poligênico sem frequência declarada usa q = 0,25 (não sai da prevalência).

