#!/usr/bin/env python3
"""Taxas da rodada de referência e razão entre dois braços pareados por seed (AD-163, `a-medir` item 86).

Lê as linhas `medida seed=...` que `demography-report` imprime (vários arquivos por braço, separados por vírgula).
Um braço: taxa somada e intervalo de 90% por bootstrap de seeds. Dois braços: razão B/A das taxas somadas, reamostrando a
seed inteira (o par) com reposição; CV entre seeds e correlação entre braços da taxa de fome (para a conta de quantas seeds).
Uso: python3 scripts/paired-rates.py A1.txt,A2.txt [B1.txt,B2.txt] [--boot 10000]
"""
import random
import statistics
import sys

BANDS = ["0", "1-4", "5-14", "15-39", "40-59", "60+"]


def read(paths):
    seeds = {}
    for path in paths.split(","):
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                if not line.startswith(("medida ", "aflicao ")):
                    continue
                kv = dict(tok.split("=", 1) for tok in line.split()[1:] if "=" in tok)
                if line.startswith("aflicao "):
                    seed = int(kv.pop("seed"))
                    seeds.setdefault(seed, {}).update({"af_" + k: float(v) for k, v in kv.items()})
                    continue
                row = {}
                for k, v in kv.items():
                    if "/" in v:
                        row[k] = [float(x) for x in v.split("/")]
                    else:
                        try:
                            row[k] = float(v)
                        except ValueError:
                            pass
                seeds.setdefault(int(kv["seed"]), {}).update(row)
    return seeds


def g(r, key, band=None):
    v = r.get(key, 0.0)
    if isinstance(v, list):
        return sum(v) if band is None else sum(v[b] for b in band)
    return v


def deaths(r, band=None):
    return sum(g(r, k, band) for k in r if k.startswith("d."))


def mem(r, kinds=None):
    return sum(v for k, v in r.items() if k.startswith("mem.") and (kinds is None or k[4:] in kinds))


# (nome, numerador, denominador, escala)
METRICS = [
    ("natalidade_por_mil_pa", lambda r: g(r, "nasc"), lambda r: g(r, "pa"), 1000),
    ("mortalidade_por_mil_pa", lambda r: deaths(r), lambda r: g(r, "pa"), 1000),
    ("fome_por_mil_pa", lambda r: g(r, "d.Starvation"), lambda r: g(r, "pa"), 1000),
    ("fome_<5_por_mil_pa", lambda r: g(r, "d.Starvation", [0, 1]), lambda r: g(r, "pa", [0, 1]), 1000),
    ("fome_15+_por_mil_pa", lambda r: g(r, "d.Starvation", [3, 4, 5]), lambda r: g(r, "pa", [3, 4, 5]), 1000),
    ("sede_por_mil_pa", lambda r: g(r, "d.Dehydration"), lambda r: g(r, "pa"), 1000),
    ("homicidio_por_mil_pa", lambda r: g(r, "d.Homicide"), lambda r: g(r, "pa"), 1000),
    ("mortes_sem_causa_%", lambda r: g(r, "d.Death"), lambda r: deaths(r), 100),
    ("sobra_nomeada_%", lambda r: sum(g(r, k) for k in r if k.startswith("d.Death.")), lambda r: deaths(r), 100),
    ("doenca_1-4_por_mil_pa", lambda r: g(r, "d.Disease", [1]), lambda r: g(r, "pa", [1]), 1000),
    ("mortes_1-4_por_mil_pa", lambda r: deaths(r, [1]), lambda r: g(r, "pa", [1]), 1000),
    ("1q0_coorte_por_mil", lambda r: g(r, "mortes1"), lambda r: g(r, "coorte1"), 1000),
    ("razao_sexo_nascer", lambda r: g(r, "nasc_m"), lambda r: g(r, "nasc_f"), 100),
    ("natimortos_por_mil_partos", lambda r: g(r, "natimortos"), lambda r: g(r, "nasc") + g(r, "natimortos"), 1000),
    ("construcoes_falhas_por_boa", lambda r: g(r, "constr_falhas"), lambda r: g(r, "constr_boas"), 1),
    ("lembrancas_de_ajuda_%", lambda r: mem(r, {"AidReceived", "AidGiven"}), lambda r: mem(r), 100),
    ("esposas_agredidas_por_ano_%", lambda r: g(r, "esposas_agredidas"), lambda r: g(r, "anos_esposa"), 100),
    ("oficio_segue_%", lambda r: g(r, "oficio_segue"), lambda r: g(r, "oficio_n"), 100),
    ("oficio_por_acaso_%", lambda r: g(r, "oficio_acaso"), lambda r: g(r, "oficio_n"), 100),
    ("oficio_pressao_alta_%", lambda r: g(r, "oficio_alta"), lambda r: g(r, "oficio_alta_n"), 100),
    ("oficio_pressao_baixa_%", lambda r: g(r, "oficio_baixa"), lambda r: g(r, "oficio_baixa_n"), 100),
]
# aflição (AD-157): horas com fome/sede crítica por pessoa-ano, ponderadas pelas pessoas-ano da seed
METRICS.append(("aflicao_fome_crit_h_por_pa", lambda r: g(r, "af_fome_crit") * g(r, "af_pa"), lambda r: g(r, "af_pa"), 1))
METRICS.append(("aflicao_sede_crit_h_por_pa", lambda r: g(r, "af_sede_crit") * g(r, "af_pa"), lambda r: g(r, "af_pa"), 1))
METRICS.append(("trabalho_h_dia_adulto", lambda r: g(r, "af_trab_h_dia") * g(r, "af_pa"), lambda r: g(r, "af_pa"), 1))
# AD-164: sonda da doméstica (`DomesticHungerProbe`): fome crítica por grupo e o antes/depois da agressão na casa
for grp in ("vitima", "perda", "separacao", "casa", "casa_antiga", "outro", "sep_todos"):  # sep_todos: AD-165
    METRICS.append((f"dv_fome_crit_{grp}_h_por_pa", lambda r, k="dvg." + grp: r.get(k, [0] * 4)[0], lambda r, k="dvg." + grp: r.get(k, [0] * 4)[1], 1))
    METRICS.append((f"dv_fome_crit_{grp}_<5_h_por_pa", lambda r, k="dvg." + grp: r.get(k, [0] * 4)[2], lambda r, k="dvg." + grp: r.get(k, [0] * 4)[3], 1))
METRICS += [
    ("dv_casa_depois/antes", lambda r: g(r, "dv_depois_h"), lambda r: g(r, "dv_antes_h"), 1),
    ("dv_casa_<5_depois/antes", lambda r: g(r, "dv_depois5_h"), lambda r: g(r, "dv_antes5_h"), 1),
    ("dv_vitima_adulta_trabalho_%", lambda r: g(r, "dv_vit_trab_h"), lambda r: g(r, "dv_vit_h"), 100),
    ("dv_mortos_por_agressao_%", lambda r: g(r, "dv_mortos"), lambda r: g(r, "dv_agressoes"), 100),
    ("dv_agressor_tirado_por_agressao_%", lambda r: g(r, "dv_agressor_tirado"), lambda r: g(r, "dv_agressoes"), 100),
    ("dv_separacao_por_agressao_%", lambda r: g(r, "dv_separacoes"), lambda r: g(r, "dv_agressoes"), 100),
    ("dv_motivo_fome_%", lambda r: g(r, "dv_motivo_fome"), lambda r: g(r, "dv_agressoes"), 100),
    ("sustento_pagamentos_por_mil_pa", lambda r: g(r, "dv_sustento"), lambda r: g(r, "pa"), 1000),  # AD-165
]
for i in range(10):
    METRICS.append((f"fome_dec{i * 10}_por_mil_pa", lambda r, i=i: r.get("fome_dec", [0] * 10)[i],
                    lambda r, i=i: r.get("pa_dec", [0] * 10)[i], 1000))


def corr(xs, ys):
    mx, my = statistics.mean(xs), statistics.mean(ys)
    sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    sxx = sum((x - mx) ** 2 for x in xs)
    syy = sum((y - my) ** 2 for y in ys)
    return sxy / (sxx * syy) ** 0.5 if sxx > 0 and syy > 0 else float("nan")


def rate(rows, num, den, scale):
    d = sum(den(r) for r in rows)
    return scale * sum(num(r) for r in rows) / d if d > 0 else float("nan")


def pct(xs, q):
    xs = sorted(x for x in xs if x == x)
    return xs[min(len(xs) - 1, int(q * len(xs)))] if xs else float("nan")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    boot = 10000
    if "--boot" in sys.argv:
        boot = int(sys.argv[sys.argv.index("--boot") + 1])
        args = [a for a in args if a != str(boot)]
    a = read(args[0])
    b = read(args[1]) if len(args) > 1 else None
    keys = sorted(a) if b is None else sorted(set(a) & set(b))
    rng = random.Random(163)  # semente fixa: o mesmo arquivo dá o mesmo intervalo
    draws = [[rng.randrange(len(keys)) for _ in keys] for _ in range(boot)]
    A = [a[k] for k in keys]
    B = [b[k] for k in keys] if b else None
    pops = [r["pop_final"] for r in A]
    print(f"seeds={len(keys)} pop_final media={statistics.mean(pops):.1f} min={min(pops):.0f} extintas={sum(p == 0 for p in pops)}"
          f" pop_min_min={min(r['pop_min'] for r in A):.0f} seeds_abaixo_de_50={sum(r['pop_min'] < 50 for r in A)}"
          f" cv_vit_min media={statistics.mean(r.get('cv_vit_min', 0) for r in A):.3f} min={min(r.get('cv_vit_min', 0) for r in A):.3f}")
    if B:
        pb = [r["pop_final"] for r in B]
        print(f"bracoB pop_final media={statistics.mean(pb):.1f} min={min(pb):.0f} extintas={sum(p == 0 for p in pb)}"
              f" pop_min_min={min(r['pop_min'] for r in B):.0f}")
    for name, num, den, scale in METRICS:
        ra = rate(A, num, den, scale)
        sa = [rate([A[i] for i in d], num, den, scale) for d in draws]
        line = f"{name}: A={ra:.3f} [{pct(sa, .05):.3f}; {pct(sa, .95):.3f}]"
        if B:
            rb = rate(B, num, den, scale)
            ratios = []
            for d in draws:
                x = rate([A[i] for i in d], num, den, scale)
                y = rate([B[i] for i in d], num, den, scale)
                ratios.append(y / x if x else float("nan"))
            line += f" B={rb:.3f} B/A={rb / ra if ra else float('nan'):.3f} IC90=[{pct(ratios, .05):.3f}; {pct(ratios, .95):.3f}]"
        print(line)
    # variação entre seeds da taxa de fome e correlação entre os braços (n ≈ 2(1−ρ)(z.95+z.8)²·CV²/δ²)
    fa = [1000 * g(r, "d.Starvation") / g(r, "pa") for r in A if g(r, "pa") > 0]
    cv = statistics.pstdev(fa) / statistics.mean(fa) if fa and statistics.mean(fa) > 0 else float("nan")
    msg = f"fome_por_seed CV={cv:.2f}"
    if B:
        fb = [1000 * g(r, "d.Starvation") / g(r, "pa") for r in B if g(r, "pa") > 0]
        rho = corr(fa, fb) if len(fa) == len(fb) and len(fa) > 2 else float("nan")
        msg += f" rho={rho:.2f}"
        for delta in (0.25, 0.10):
            msg += f" n(delta={delta:.2f})={2 * (1 - rho) * (1.645 + 0.842) ** 2 * cv ** 2 / delta ** 2:.0f}"
    print(msg)


if __name__ == "__main__":
    main()
