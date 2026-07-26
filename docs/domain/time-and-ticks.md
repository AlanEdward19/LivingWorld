# Tempo e Ticks

Modelo de tempo do Living World: como o mundo mede sua própria passagem e em que
frequência cada sistema tem o direito de rodar.

## Relógio próprio

O mundo tem um `WorldDate` — ano, mês, dia e hora **do cenário**. O relógio da máquina
nunca é fonte da verdade: ele só decide *quando* o próximo tick acontece em tempo real,
nunca *que horas são* no mundo.

O calendário é dado do cenário (quantos meses no ano, dias no mês, horas no dia). Um
cenário pode ter 12 meses de 30 dias; outro, 8 meses de 40. Nada no motor assume o
calendário gregoriano.

## Escala de ticks

Cada escala é uma frequência de execução; um tick maior acontece "por cima" dos menores.

| Escala | Passo | O que roda aqui |
|---|---|---|
| Hourly | 1 hora | Deslocamento, trabalho, sono, alimentação, eventos imediatos |
| Daily | 1 dia | Necessidades, produção, consumo, relações, saúde, aprendizado |
| Monthly | 1 mês | Salários, preços, migração, empregos, gravidez, comércio, criminalidade |
| Yearly | 1 ano | Envelhecimento, nascimento, morte, educação, evolução de atributos, política, tecnologia |
| Historic | décadas/séculos | Só agregado: dinastias, ascensão e queda, deslocamentos de povos |

**Nem todo sistema roda em todo tick.** Cada sistema se registra na frequência mais barata
que ainda produz o comportamento desejado. Preço de pão não precisa recalcular de hora em
hora; fome precisa. Monthly custa ~1/720 de Hourly — é a principal alavanca do motor.

## Agendamento de eventos

Coisa rara e datável **não vira varredura por tick**. Em vez de perguntar a cada dia "esta
mulher deu à luz?" para toda a população, o evento é agendado quando a causa acontece.

```
gravidez inicia  -> agenda NascimentoEvent(alvo = WorldDate + ~9 meses)
tick(WorldDate)  -> processa fila[WorldDate]
```

Isso troca **O(população) por O(eventos)**. Vale para parto, colheita, morte agendada,
chegada de caravana, fim de contrato. A fila é indexada por tick alvo; ticks vazios custam
zero.

## Controle da simulação

| Controle | Efeito |
|---|---|
| Pausa | Nenhum tick avança; o mundo congela num `WorldDate` exato |
| Velocidade | Multiplicador de ticks por segundo real |
| Avanço rápido | Roda N ticks o mais rápido possível, sem esperar tempo real |
| Snapshot | Estado completo num `WorldDate`, para salvar, ramificar ou depurar |

## Determinismo

Duas garantias, ambas obrigatórias:

1. **Ordem dos sistemas** dentro de um tick é fixa e declarada. Não depende de ordem de
   registro, de hash de dicionário nem de paralelismo oportunista.
2. **Ordem dos eventos** no mesmo tick desempata por ID. Dois nascimentos no mesmo tick
   sempre são processados na mesma sequência.

Sem isso, snapshot, replay e reprodução de bug deixam de funcionar.

## Ver também
- [simulation-lod.md](simulation-lod.md) — quanto detalhe simular por região
- [economy.md](economy.md) — quais sistemas econômicos rodam em cada escala
- [history.md](history.md) — como eventos processados viram linha do tempo
- [behavior.md](behavior.md) — decisão de NPC dentro do tick Hourly/Daily
